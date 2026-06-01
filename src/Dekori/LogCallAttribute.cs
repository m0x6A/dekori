using Microsoft.Extensions.Logging;

namespace Dekori;

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
