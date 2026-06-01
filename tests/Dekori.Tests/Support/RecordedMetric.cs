namespace Dekori.Tests.Support;

/// <summary>A single metric measurement captured in-process by <see cref="TelemetryProbe"/>.</summary>
public sealed record RecordedMetric(string Name, double Value, IReadOnlyDictionary<string, object?> Tags);
