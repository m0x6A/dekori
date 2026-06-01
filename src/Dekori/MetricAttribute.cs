namespace Dekori;

/// <summary>
/// Records metrics for each call: an invocation counter, a duration histogram, and (together with
/// <see cref="CaptureExceptionAttribute"/>) an error counter. Valid on a method or a class.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class MetricAttribute : Attribute
{
    /// <summary>
    /// Base instrument name. Produces <c>{Name}.calls</c>, <c>{Name}.duration</c> and
    /// <c>{Name}.errors</c>. Defaults to <see cref="DekoriOptions.DefaultMetricName"/> (<c>dekori.method</c>),
    /// with the operation carried as a low-cardinality tag.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Record a duration histogram (milliseconds). On by default.</summary>
    public bool RecordDuration { get; set; } = true;

    /// <summary>Record an invocation counter. On by default.</summary>
    public bool RecordCount { get; set; } = true;
}
