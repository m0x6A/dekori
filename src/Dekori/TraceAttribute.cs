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

    /// <summary>The <see cref="ActivityKind"/> of the span. Defaults to <see cref="ActivityKind.Internal"/>.</summary>
    public ActivityKind Kind { get; set; } = ActivityKind.Internal;

    /// <summary>When true, method arguments are recorded as span tags. Off by default (PII safety).</summary>
    public bool RecordArguments { get; set; }

    /// <summary>When true, the return value is recorded as a span tag. Off by default (PII safety).</summary>
    public bool RecordReturnValue { get; set; }
}
