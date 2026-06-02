using System.Diagnostics;

namespace Dekori.Instrumentation;

/// <summary>Trace behavior settings resolved for a method.</summary>
/// <param name="SpanName">The span's display name.</param>
/// <param name="Kind">The span's <see cref="ActivityKind"/>.</param>
/// <param name="RecordArguments">Whether to record arguments as span tags.</param>
/// <param name="RecordReturnValue">Whether to record the return value as a span tag.</param>
/// <param name="SourceName">Name of the <see cref="ActivitySource"/> the span is emitted from.</param>
/// <param name="NewRoot">When true, start a root span ignoring the ambient activity (unless an explicit parent is supplied).</param>
/// <param name="ParentParameterIndex">Index of the <c>[TraceParent]</c> parameter, or -1 when none.</param>
/// <param name="LinkParameterIndices">Indices of <c>[TraceLink]</c> parameters; empty when none.</param>
internal sealed record TraceSettings(
    string SpanName,
    ActivityKind Kind,
    bool RecordArguments,
    bool RecordReturnValue,
    string SourceName,
    bool NewRoot,
    int ParentParameterIndex,
    IReadOnlyList<int> LinkParameterIndices);
