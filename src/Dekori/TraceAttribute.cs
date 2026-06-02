using System.Diagnostics;

namespace Dekori;

/// <summary>
/// Starts an <see cref="Activity"/> (span) around the decorated method. Valid on a method, or on a
/// class to apply to every method.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class TraceAttribute : Attribute
{
    /// <summary>Explicit span name. Defaults to <c>{Type}.{Method}</c> when not set.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Name of the <see cref="ActivitySource"/> this span is emitted from. When null or empty, the span
    /// uses <see cref="DekoriOptions.ActivitySourceName"/>. Lets different methods emit from different,
    /// independently subscribable sources; declare extra names in
    /// <see cref="DekoriOptions.AdditionalActivitySourceNames"/> so they can be registered with OpenTelemetry.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>The <see cref="ActivityKind"/> of the span. Defaults to <see cref="ActivityKind.Internal"/>.</summary>
    public ActivityKind Kind { get; set; } = ActivityKind.Internal;

    /// <summary>
    /// When true, the span starts a brand-new trace (root span) and ignores the ambient
    /// <see cref="Activity.Current"/>. An explicit <see cref="TraceParentAttribute"/> parameter still wins.
    /// Off by default, so spans nest under the current activity.
    /// </summary>
    public bool NewRoot { get; set; }

    /// <summary>When true, method arguments are recorded as span tags. Off by default (PII safety).</summary>
    public bool RecordArguments { get; set; }

    /// <summary>When true, the return value is recorded as a span tag. Off by default (PII safety).</summary>
    public bool RecordReturnValue { get; set; }
}
