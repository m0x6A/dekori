using System.Collections.Concurrent;
using System.Reflection;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;

namespace Dekori.Instrumentation;

/// <summary>
/// Resolves and caches the <see cref="InstrumentationPlan"/> for each intercepted method. Reflection
/// runs once per method; subsequent calls hit the cache.
/// </summary>
internal sealed class InstrumentationPlanCache
{
    private readonly DekoriOptions _options;
    private readonly ConcurrentDictionary<MethodInfo, InstrumentationPlan> _plans = new();

    public InstrumentationPlanCache(DekoriOptions options) => _options = options;

    /// <summary>Returns the cached plan for the method targeted by the invocation.</summary>
    public InstrumentationPlan GetPlan(IInvocation invocation)
    {
        // The implementation method carries the user's attributes; fall back to the interface method.
        MethodInfo method = invocation.MethodInvocationTarget ?? invocation.Method;
        return _plans.GetOrAdd(method, m => Build(m, invocation.TargetType ?? m.DeclaringType));
    }

    private InstrumentationPlan Build(MethodInfo method, Type? targetType)
    {
        if (method.IsDefined(typeof(NoInstrumentAttribute), inherit: true))
        {
            return InstrumentationPlan.None;
        }

        Type declaringType = targetType ?? method.DeclaringType ?? typeof(object);
        string operationName = $"{declaringType.Name}.{method.Name}";

        bool classOptIn = declaringType.IsDefined(typeof(InstrumentAttribute), inherit: true);

        TraceAttribute? traceAttr = Resolve<TraceAttribute>(method, declaringType);
        MetricAttribute? metricAttr = Resolve<MetricAttribute>(method, declaringType);
        CaptureExceptionAttribute? captureAttr = Resolve<CaptureExceptionAttribute>(method, declaringType);
        LogCallAttribute? logAttr = Resolve<LogCallAttribute>(method, declaringType);

        TraceSettings? trace = (traceAttr is not null || classOptIn)
            ? new TraceSettings(
                SpanName: string.IsNullOrWhiteSpace(traceAttr?.Name) ? operationName : traceAttr!.Name!,
                Kind: traceAttr?.Kind ?? System.Diagnostics.ActivityKind.Internal,
                RecordArguments: (traceAttr?.RecordArguments ?? false) || _options.CaptureArgumentsByDefault,
                RecordReturnValue: (traceAttr?.RecordReturnValue ?? false) || _options.CaptureReturnValueByDefault)
            : null;

        MetricSettings? metric = (metricAttr is not null || classOptIn)
            ? new MetricSettings(
                BaseName: string.IsNullOrWhiteSpace(metricAttr?.Name) ? _options.DefaultMetricName : metricAttr!.Name!,
                RecordDuration: metricAttr?.RecordDuration ?? true,
                RecordCount: metricAttr?.RecordCount ?? true)
            : null;

        bool captureException = captureAttr is not null || classOptIn;

        LogSettings? log = logAttr is not null
            ? new LogSettings(
                Level: logAttr.Level == LogLevel.None ? _options.DefaultLogLevel : logAttr.Level,
                LogArguments: logAttr.LogArguments,
                LogResult: logAttr.LogResult)
            : null;

        if (trace is null && metric is null && !captureException && log is null)
        {
            return InstrumentationPlan.None;
        }

        string errorMetricBaseName = metric?.BaseName ?? _options.DefaultMetricName;
        return new InstrumentationPlan(
            operationName, declaringType.Name, trace, metric, captureException, log, errorMetricBaseName);
    }

    /// <summary>Method-level attribute wins; otherwise the class-level attribute applies.</summary>
    private static T? Resolve<T>(MethodInfo method, Type declaringType) where T : Attribute =>
        method.GetCustomAttribute<T>(inherit: true) ?? declaringType.GetCustomAttribute<T>(inherit: true);
}
