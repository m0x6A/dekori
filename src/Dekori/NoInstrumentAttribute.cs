namespace Dekori;

/// <summary>
/// Opt-out escape hatch: excludes a single method from instrumentation even when its declaring type
/// carries <see cref="InstrumentAttribute"/> or class-level telemetry attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class NoInstrumentAttribute : Attribute
{
}
