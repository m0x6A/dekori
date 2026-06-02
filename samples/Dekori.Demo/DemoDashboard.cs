using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Dekori.Demo;

/// <summary>
/// Captures Dekori spans, metric measurements and log entries in-process, then renders a formatted
/// summary table to the console. Implements <see cref="ILoggerProvider"/> so it can be registered
/// with the host logging pipeline to intercept <c>Dekori.*</c> log categories.
/// </summary>
internal sealed class DemoDashboard : ILoggerProvider
{
    private sealed record SpanEntry(string Name, TimeSpan Duration, ActivityStatusCode Status, string? ExceptionType);
    private sealed record LogEntry(string Category, LogLevel Level, string Message);

    private readonly List<SpanEntry> _spans = [];
    private readonly Dictionary<string, long> _counters = [];
    private readonly Dictionary<string, List<double>> _histograms = [];
    private readonly List<LogEntry> _logEntries = [];
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly object _lock = new();

    internal DemoDashboard()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("Dekori", StringComparison.Ordinal),
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

    // ── ILoggerProvider ──────────────────────────────────────────────────────────────────────────

    ILogger ILoggerProvider.CreateLogger(string categoryName) => new DekoriLogger(categoryName, this);

    internal void CaptureLog(string category, LogLevel level, string message)
    {
        lock (_lock)
        {
            _logEntries.Add(new LogEntry(category, level, message));
        }
    }

    private sealed class DekoriLogger(string category, DemoDashboard dashboard) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.None || !category.StartsWith("Dekori.", StringComparison.Ordinal))
            {
                return;
            }

            string shortCategory = category["Dekori.".Length..];
            dashboard.CaptureLog(shortCategory, logLevel, formatter(state, exception));
        }
    }

    // ── Listeners ────────────────────────────────────────────────────────────────────────────────

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

    // ── Render ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>Renders the collected spans, metrics and logs to the console.</summary>
    public void Render()
    {
        List<SpanEntry> spans;
        List<(string Instrument, string Operation, string Value)> metricRows;
        List<LogEntry> logEntries;

        lock (_lock)
        {
            spans = [.._spans];
            metricRows = BuildMetricRows();
            logEntries = [.._logEntries];
        }

        RenderSpans(spans);
        RenderMetrics(metricRows);
        RenderLogs(logEntries);
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
    }

    private static void RenderLogs(List<LogEntry> entries)
    {
        const int C = 22;  // category
        const int L = 5;   // level label
        const int M = 55;  // message
        int width = C + L + M + 10;

        Console.WriteLine();
        Console.WriteLine(SectionTitle("LOGS", width));
        Console.WriteLine(TableRow3('┌', '┬', '┐', C, L, M));
        Console.WriteLine($"│ {"Category",-C} │ {"Level",-L} │ {"Message",-M} │");
        Console.WriteLine(TableRow3('├', '┼', '┤', C, L, M));

        foreach (LogEntry entry in entries)
        {
            string levelLabel = LevelLabel(entry.Level);
            string levelPadded = $"{levelLabel,-L}";
            string levelColored = entry.Level >= LogLevel.Error
                ? $"\x1b[31m{levelPadded}\x1b[0m"
                : entry.Level == LogLevel.Warning
                    ? $"\x1b[33m{levelPadded}\x1b[0m"
                    : levelPadded;
            Console.WriteLine($"│ {Clip(entry.Category, C),-C} │ {levelColored} │ {Clip(entry.Message, M),-M} │");
        }

        Console.WriteLine(TableRow3('└', '┴', '┘', C, L, M));
        Console.WriteLine();
    }

    private static string LevelLabel(LogLevel level) => level switch
    {
        LogLevel.Trace       => "TRACE",
        LogLevel.Debug       => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning     => "WARN",
        LogLevel.Error       => "ERROR",
        LogLevel.Critical    => "CRIT",
        _                    => "?",
    };

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
