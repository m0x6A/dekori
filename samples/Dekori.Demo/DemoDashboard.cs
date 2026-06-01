using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Dekori.Demo;

/// <summary>
/// Captures Dekori spans and metric measurements in-process via <see cref="ActivityListener"/> and
/// <see cref="MeterListener"/>, then renders a formatted summary table to the console.
/// </summary>
internal sealed class DemoDashboard : IDisposable
{
    private sealed record SpanEntry(string Name, TimeSpan Duration, ActivityStatusCode Status, string? ExceptionType);

    private readonly List<SpanEntry> _spans = [];
    private readonly Dictionary<string, long> _counters = [];
    private readonly Dictionary<string, List<double>> _histograms = [];
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly object _lock = new();

    internal DemoDashboard()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dekori",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = OnSpanStopped,
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Dekori")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>(OnLongMeasurement);
        _meterListener.SetMeasurementEventCallback<double>(OnDoubleMeasurement);
        _meterListener.Start();
    }

    private void OnSpanStopped(Activity activity)
    {
        string? exType = null;
        foreach (ActivityEvent ev in activity.Events)
        {
            if (ev.Name != "exception")
            {
                continue;
            }

            foreach (KeyValuePair<string, object?> tag in ev.Tags)
            {
                if (tag.Key == "exception.type")
                {
                    exType = tag.Value?.ToString();
                    break;
                }
            }
        }

        lock (_lock)
        {
            _spans.Add(new SpanEntry(activity.DisplayName, activity.Duration, activity.Status, exType));
        }
    }

    private void OnLongMeasurement(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? _)
    {
        string key = MakeKey(instrument.Name, tags);
        lock (_lock)
        {
            _counters[key] = (_counters.TryGetValue(key, out long prev) ? prev : 0) + measurement;
        }
    }

    private void OnDoubleMeasurement(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? _)
    {
        string key = MakeKey(instrument.Name, tags);
        lock (_lock)
        {
            if (!_histograms.TryGetValue(key, out List<double>? list))
            {
                list = [];
                _histograms[key] = list;
            }

            list.Add(measurement);
        }
    }

    private static string MakeKey(string instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (KeyValuePair<string, object?> tag in tags)
        {
            if (tag.Key == "code.function")
            {
                return $"{instrument}|{tag.Value}";
            }
        }

        return $"{instrument}|";
    }

    /// <summary>Renders the collected spans and metrics to the console.</summary>
    public void Render()
    {
        List<SpanEntry> spans;
        List<(string Instrument, string Operation, string Value)> metricRows;

        lock (_lock)
        {
            spans = [.._spans];
            metricRows = BuildMetricRows();
        }

        RenderSpans(spans);
        RenderMetrics(metricRows);
    }

    private List<(string, string, string)> BuildMetricRows()
    {
        var rows = new List<(string, string, string)>();

        foreach (KeyValuePair<string, long> kv in _counters.OrderBy(k => k.Key))
        {
            (string inst, string op) = SplitKey(kv.Key);
            rows.Add((inst, op, kv.Value.ToString()));
        }

        foreach (KeyValuePair<string, List<double>> kv in _histograms.OrderBy(k => k.Key))
        {
            (string inst, string op) = SplitKey(kv.Key);
            double avg = kv.Value.Average();
            double max = kv.Value.Max();
            rows.Add((inst, op, $"avg {avg:F1} ms · max {max:F1} ms · n={kv.Value.Count}"));
        }

        return rows;
    }

    private static (string, string) SplitKey(string key)
    {
        int idx = key.IndexOf('|');
        return idx < 0 ? (key, "") : (key[..idx], key[(idx + 1)..]);
    }

    private static void RenderSpans(List<SpanEntry> spans)
    {
        const int N = 30;  // operation
        const int D = 9;   // duration
        const int S = 7;   // status
        const int E = 33;  // exception
        // Each row: "│ " + N + " │ " + D + " │ " + S + " │ " + E + " │"
        //         = 2 + N + 3 + D + 3 + S + 3 + E + 2 = N+D+S+E+13
        int width = N + D + S + E + 13;

        Console.WriteLine();
        Console.WriteLine(SectionTitle("SPANS", width));
        Console.WriteLine(TableRow('┌', '┬', '┐', N, D, S, E));
        Console.WriteLine($"│ {"Operation",-N} │ {"Duration",D} │ {"Status",S} │ {"Exception",-E} │");
        Console.WriteLine(TableRow('├', '┼', '┤', N, D, S, E));

        foreach (SpanEntry span in spans)
        {
            bool isError = span.Status == ActivityStatusCode.Error;
            string statusPadded = $"{(isError ? "ERROR" : "OK"),S}";
            string statusColored = isError
                ? $"\x1b[31m{statusPadded}\x1b[0m"
                : $"\x1b[32m{statusPadded}\x1b[0m";
            string duration = $"{span.Duration.TotalMilliseconds:F1} ms";
            string ex = span.ExceptionType is null ? "" : ShortTypeName(span.ExceptionType);
            Console.WriteLine($"│ {Clip(span.Name, N),-N} │ {duration,D} │ {statusColored} │ {Clip(ex, E),-E} │");
        }

        Console.WriteLine(TableRow('└', '┴', '┘', N, D, S, E));
    }

    private static void RenderMetrics(List<(string Instrument, string Operation, string Value)> rows)
    {
        const int I = 24;  // instrument
        const int O = 28;  // operation
        const int V = 36;  // value
        // Each row: "│ " + I + " │ " + O + " │ " + V + " │" = I+O+V+10
        int width = I + O + V + 10;

        Console.WriteLine();
        Console.WriteLine(SectionTitle("METRICS", width));
        Console.WriteLine(TableRow3('┌', '┬', '┐', I, O, V));
        Console.WriteLine($"│ {"Instrument",-I} │ {"Operation",-O} │ {"Value",-V} │");
        Console.WriteLine(TableRow3('├', '┼', '┤', I, O, V));

        foreach ((string inst, string op, string val) in rows)
        {
            string instPadded = $"{Clip(inst, I),-I}";
            Console.WriteLine($"│ \x1b[36m{instPadded}\x1b[0m │ {Clip(op, O),-O} │ {Clip(val, V),-V} │");
        }

        Console.WriteLine(TableRow3('└', '┴', '┘', I, O, V));
        Console.WriteLine();
    }

    private static string SectionTitle(string title, int width)
    {
        string label = $" {title} ";
        int totalPad = Math.Max(0, width - label.Length);
        int left = totalPad / 2;
        int right = totalPad - left;
        return $"\x1b[1m{new string('═', left)}{label}{new string('═', right)}\x1b[0m";
    }

    private static string TableRow(char left, char mid, char right, int a, int b, int c, int d)
    {
        return $"{left}{new string('─', a + 2)}{mid}{new string('─', b + 2)}{mid}{new string('─', c + 2)}{mid}{new string('─', d + 2)}{right}";
    }

    private static string TableRow3(char left, char mid, char right, int a, int b, int c)
    {
        return $"{left}{new string('─', a + 2)}{mid}{new string('─', b + 2)}{mid}{new string('─', c + 2)}{right}";
    }

    private static string Clip(string s, int maxLen)
    {
        return s.Length <= maxLen ? s : $"{s[..(maxLen - 1)]}…";
    }

    private static string ShortTypeName(string fullName)
    {
        int dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName[(dot + 1)..] : fullName;
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }
}
