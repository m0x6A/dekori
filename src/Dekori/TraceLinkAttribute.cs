using System.Diagnostics;

namespace Dekori;

/// <summary>
/// Marks a parameter that supplies one or more OpenTelemetry span links for the span started by
/// <see cref="TraceAttribute"/>. Supported parameter types are <see cref="ActivityContext"/>,
/// <see cref="ActivityLink"/>, <see cref="IEnumerable{T}"/> of <see cref="ActivityContext"/> and
/// <see cref="IEnumerable{T}"/> of <see cref="ActivityLink"/>. Multiple parameters may each be marked; their
/// links are combined. <see langword="null"/> or empty values contribute no links.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class TraceLinkAttribute : Attribute
{
}
