namespace Dekori.Instrumentation;

/// <summary>Metric behavior settings resolved for a method.</summary>
internal sealed record MetricSettings(string BaseName, bool RecordDuration, bool RecordCount);
