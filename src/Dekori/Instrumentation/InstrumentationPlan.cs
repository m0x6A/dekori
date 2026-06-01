using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dekori.Instrumentation;

/// <summary>Trace behavior settings resolved for a method.</summary>
internal sealed record TraceSettings(string SpanName, ActivityKind Kind, bool RecordArguments, bool RecordReturnValue);

/// <summary>Metric behavior settings resolved for a method.</summary>
internal sealed record MetricSettings(string BaseName, bool RecordDuration, bool RecordCount);

/// <summary>Log behavior settings resolved for a method.</summary>
internal sealed record LogSettings(LogLevel Level, bool LogArguments, bool LogResult);

/// <summary>
/// Immutable, per-method description of which telemetry behaviors apply and with what options.
/// Built once via reflection (see <see cref="InstrumentationPlanCache"/>) and reused on every call,
/// so reflection never runs on the hot path.
/// </summary>
internal sealed class InstrumentationPlan
{
    public static readonly InstrumentationPlan None = new();

    private InstrumentationPlan()
    {
    }

    public InstrumentationPlan(
        string operationName,
        string typeName,
        TraceSettings? trace,
        MetricSettings? metric,
        bool captureException,
        LogSettings? log,
        string errorMetricBaseName)
    {
        OperationName = operationName;
        TypeName = typeName;
        Trace = trace;
        Metric = metric;
        CaptureException = captureException;
        Log = log;
        ErrorMetricBaseName = errorMetricBaseName;
    }

    /// <summary>True when at least one behavior applies and the method should be instrumented.</summary>
    public bool IsInstrumented => Trace is not null || Metric is not null || CaptureException || Log is not null;

    /// <summary><c>{Type}.{Method}</c> — used for default span names, metric tags and log scopes.</summary>
    public string OperationName { get; } = string.Empty;

    /// <summary>Declaring type name, used as the logger category / metric tag.</summary>
    public string TypeName { get; } = string.Empty;

    public TraceSettings? Trace { get; }

    public MetricSettings? Metric { get; }

    public bool CaptureException { get; }

    public LogSettings? Log { get; }

    /// <summary>Base name for the error counter (<c>{base}.errors</c>) emitted when an exception is captured.</summary>
    public string ErrorMetricBaseName { get; } = string.Empty;
}
