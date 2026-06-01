using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Dekori;

/// <summary>
/// Owns the shared OpenTelemetry primitives used by the interceptor: a single
/// <see cref="ActivitySource"/> and <see cref="Meter"/>. Metric instruments are created lazily and
/// cached by name. Registered as a singleton.
/// </summary>
public sealed class DekoriTelemetry : IDisposable
{
    private readonly Meter _meter;
    private readonly ConcurrentDictionary<string, Counter<long>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();

    public DekoriTelemetry(DekoriOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ActivitySource = new ActivitySource(options.ActivitySourceName, options.Version);
        _meter = new Meter(options.MeterName, options.Version);
    }

    /// <summary>The activity source spans are started from.</summary>
    public ActivitySource ActivitySource { get; }

    /// <summary>Gets or creates a cached <see cref="Counter{T}"/> for the given instrument name.</summary>
    public Counter<long> Counter(string name) =>
        _counters.GetOrAdd(name, static (n, m) => m.CreateCounter<long>(n), _meter);

    /// <summary>Gets or creates a cached <see cref="Histogram{T}"/> for the given instrument name.</summary>
    public Histogram<double> Histogram(string name, string? unit = null) =>
        _histograms.GetOrAdd(name, n => _meter.CreateHistogram<double>(n, unit));

    public void Dispose()
    {
        ActivitySource.Dispose();
        _meter.Dispose();
    }
}
