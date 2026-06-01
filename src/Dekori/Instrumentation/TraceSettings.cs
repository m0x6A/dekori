using System.Diagnostics;

namespace Dekori.Instrumentation;

/// <summary>Trace behavior settings resolved for a method.</summary>
internal sealed record TraceSettings(string SpanName, ActivityKind Kind, bool RecordArguments, bool RecordReturnValue);
