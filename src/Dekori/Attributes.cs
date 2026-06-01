using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dekori;

/// <summary>
/// Class-level opt-in: every public/virtual method of the decorated type is instrumented with the
/// default plan (trace + metrics + exception capture). Method-level attributes refine or override
/// this, and <see cref="NoInstrumentAttribute"/> opts an individual method out.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class InstrumentAttribute : Attribute
{
}

/// <summary>
/// Starts an <see cref="Activity"/> (span) around the decorated method. Valid on a method, or on a
/// class to apply to every method.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class TraceAttribute : Attribute
{
    /// <summary>Explicit span name. Defaults to <c>{Type}.{Method}</c> when not set.</summary>
    public string? Name { get; set; }

    /// <summary>The <see cref="ActivityKind"/> of the span. Defaults to <see cref="ActivityKind.Internal"/>.</summary>
    public ActivityKind Kind { get; set; } = ActivityKind.Internal;

    /// <summary>When true, method arguments are recorded as span tags. Off by default (PII safety).</summary>
    public bool RecordArguments { get; set; }

    /// <summary>When true, the return value is recorded as a span tag. Off by default (PII safety).</summary>
    public bool RecordReturnValue { get; set; }
}

/// <summary>
/// Records metrics for each call: an invocation counter, a duration histogram, and (together with
/// <see cref="CaptureExceptionAttribute"/>) an error counter. Valid on a method or a class.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class MetricAttribute : Attribute
{
    /// <summary>
    /// Base instrument name. Produces <c>{Name}.calls</c>, <c>{Name}.duration</c> and
    /// <c>{Name}.errors</c>. Defaults to <see cref="DekoriOptions.DefaultMetricName"/> (<c>dekori.method</c>),
    /// with the operation carried as a low-cardinality tag.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Record a duration histogram (milliseconds). On by default.</summary>
    public bool RecordDuration { get; set; } = true;

    /// <summary>Record an invocation counter. On by default.</summary>
    public bool RecordCount { get; set; } = true;
}

/// <summary>
/// Captures exceptions thrown by the decorated method: records them on the active span, sets the
/// span status to error, increments an error counter and logs at error level. The exception is
/// always rethrown — instrumentation never swallows it.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class CaptureExceptionAttribute : Attribute
{
}

/// <summary>
/// Emits structured entry and exit logs around the decorated method. Opt-in (not part of the
/// <see cref="InstrumentAttribute"/> default). Valid on a method or a class.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class LogCallAttribute : Attribute
{
    /// <summary>
    /// Level for entry/exit logs. When left as <see cref="LogLevel.None"/> (the default), falls back
    /// to <see cref="DekoriOptions.DefaultLogLevel"/> (<see cref="LogLevel.Debug"/>).
    /// </summary>
    public LogLevel Level { get; set; } = LogLevel.None;

    /// <summary>Include argument values in the entry log. Off by default (PII safety).</summary>
    public bool LogArguments { get; set; }

    /// <summary>Include the return value in the exit log. Off by default (PII safety).</summary>
    public bool LogResult { get; set; }
}

/// <summary>
/// Opt-out escape hatch: excludes a single method from instrumentation even when its declaring type
/// carries <see cref="InstrumentAttribute"/> or class-level telemetry attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class NoInstrumentAttribute : Attribute
{
}
