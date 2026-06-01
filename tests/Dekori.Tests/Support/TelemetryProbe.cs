using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Dekori.Tests.Support;

/// <summary>
/// Captures the <em>real</em> OpenTelemetry signals a test produces by attaching an
/// <see cref="ActivityListener"/> and a <see cref="MeterListener"/> scoped to a single source/meter
/// name. Assertions then run against genuine spans and measurements, not mocks.
/// </summary>
public sealed class TelemetryProbe : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly List<Activity> _activities = new();
    private readonly List<RecordedMetric> _metrics = new();
    private readonly Lock _gate = new();

    public TelemetryProbe(string sourceName, string meterName)
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_gate)
                {
                    _activities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _meterListener.SetMeasurementEventCallback<long>((inst, value, tags, _) => Record(inst, value, tags));
        _meterListener.SetMeasurementEventCallback<double>((inst, value, tags, _) => Record(inst, value, tags));
        _meterListener.Start();
    }

    public IReadOnlyList<Activity> Activities
    {
        get
        {
            lock (_gate)
            {
                return _activities.ToList();
            }
        }
    }

    public IReadOnlyList<RecordedMetric> Metrics
    {
        get
        {
            lock (_gate)
            {
                return _metrics.ToList();
            }
        }
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var tag in tags)
        {
            dict[tag.Key] = tag.Value;
        }

        lock (_gate)
        {
            _metrics.Add(new RecordedMetric(instrument.Name, value, dict));
        }
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _activityListener.Dispose();
    }
}
