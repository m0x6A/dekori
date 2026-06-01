using Microsoft.Extensions.Logging;

namespace Dekori;

/// <summary>
/// Configuration for the Dekori instrumentation library. Configured via
/// <c>services.AddDekori(o =&gt; ...)</c>.
/// </summary>
public sealed class DekoriOptions
{
    /// <summary>
    /// Name of the <see cref="System.Diagnostics.ActivitySource"/> spans are emitted from. This is
    /// the name an OpenTelemetry <c>AddSource(...)</c> call must subscribe to. Defaults to <c>Dekori</c>.
    /// </summary>
    public string ActivitySourceName { get; set; } = "Dekori";

    /// <summary>
    /// Name of the <see cref="System.Diagnostics.Metrics.Meter"/> metrics are emitted from. This is
    /// the name an OpenTelemetry <c>AddMeter(...)</c> call must subscribe to. Defaults to <c>Dekori</c>.
    /// </summary>
    public string MeterName { get; set; } = "Dekori";

    /// <summary>Version reported for the activity source and meter.</summary>
    public string? Version { get; set; }

    /// <summary>
    /// Default base name for metric instruments when <see cref="MetricAttribute.Name"/> is not set.
    /// Produces <c>{name}.calls</c>, <c>{name}.duration</c> and <c>{name}.errors</c>. Defaults to
    /// <c>dekori.method</c>.
    /// </summary>
    public string DefaultMetricName { get; set; } = "dekori.method";

    /// <summary>Default level for entry/exit logs when a <see cref="LogCallAttribute"/> does not specify one.</summary>
    public LogLevel DefaultLogLevel { get; set; } = LogLevel.Debug;

    /// <summary>When true, <c>[Trace]</c> records arguments as span tags unless the attribute overrides it. Off by default.</summary>
    public bool CaptureArgumentsByDefault { get; set; }

    /// <summary>When true, <c>[Trace]</c> records the return value as a span tag unless the attribute overrides it. Off by default.</summary>
    public bool CaptureReturnValueByDefault { get; set; }
}
