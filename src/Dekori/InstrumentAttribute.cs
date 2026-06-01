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
