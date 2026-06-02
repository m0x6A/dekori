using System.Diagnostics;

namespace Dekori;

/// <summary>
/// Marks a parameter that supplies the explicit parent for the span started by <see cref="TraceAttribute"/>.
/// The parameter must be an <see cref="ActivityContext"/> or an <see cref="Activity"/>; its value is read at
/// call time and used as the span's parent instead of the ambient <see cref="Activity.Current"/>. A
/// <see langword="default"/>/<see langword="null"/> value falls back to normal parenting.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class TraceParentAttribute : Attribute
{
}
