namespace Dekori;

/// <summary>
/// Captures exceptions thrown by the decorated method: records them on the active span, sets the
/// span status to error, increments an error counter and logs at error level. The exception is
/// always rethrown — instrumentation never swallows it.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class CaptureExceptionAttribute : Attribute
{
}
