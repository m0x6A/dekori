using System.Collections.Concurrent;
using System.Diagnostics;
using Castle.DynamicProxy;
using Dekori.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Dekori;

/// <summary>
/// Castle DynamicProxy interceptor that applies the resolved <see cref="InstrumentationPlan"/> to
/// each call: it starts spans, records metrics, writes entry/exit logs and captures exceptions.
/// </summary>
/// <remarks>
/// Derives from <c>AsyncInterceptorBase</c> so that <see cref="Task"/>/<see cref="ValueTask"/>
/// results are awaited <em>inside</em> the span — span and metric durations cover the whole async
/// operation, not just the synchronous prologue.
/// </remarks>
public sealed class DekoriInterceptor : AsyncInterceptorBase
{
    private readonly InstrumentationPlanCache _planCache;
    private readonly DekoriTelemetry _telemetry;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new();

    internal DekoriInterceptor(InstrumentationPlanCache planCache, DekoriTelemetry telemetry, ILoggerFactory loggerFactory)
    {
        _planCache = planCache;
        _telemetry = telemetry;
        _loggerFactory = loggerFactory;
    }

    protected override async Task InterceptAsync(
        IInvocation invocation,
        IInvocationProceedInfo proceedInfo,
        Func<IInvocation, IInvocationProceedInfo, Task> proceed)
    {
        InstrumentationPlan plan = _planCache.GetPlan(invocation);
        if (!plan.IsInstrumented)
        {
            await proceed(invocation, proceedInfo).ConfigureAwait(false);
            return;
        }

        await ExecuteAsync(invocation, plan, async () =>
        {
            await proceed(invocation, proceedInfo).ConfigureAwait(false);
            return (object?)null; // unifies the void path with the object?-returning pipeline
        }).ConfigureAwait(false);
    }

    protected override async Task<TResult> InterceptAsync<TResult>(
        IInvocation invocation,
        IInvocationProceedInfo proceedInfo,
        Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
    {
        InstrumentationPlan plan = _planCache.GetPlan(invocation);
        if (!plan.IsInstrumented)
        {
            return await proceed(invocation, proceedInfo).ConfigureAwait(false);
        }

        object? result = await ExecuteAsync(invocation, plan, async () =>
            (object?)await proceed(invocation, proceedInfo).ConfigureAwait(false)).ConfigureAwait(false);
        return (TResult)result!;
    }

    private async Task<object?> ExecuteAsync(IInvocation invocation, InstrumentationPlan plan, Func<Task<object?>> proceed)
    {
        ILogger logger = GetLogger(plan.TypeName);
        Activity? activity = plan.Trace is null ? null : StartActivity(invocation, plan);
        long startTimestamp = Stopwatch.GetTimestamp();
        LogEntry(logger, invocation, plan);

        try
        {
            object? result = await proceed().ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            RecordReturnValue(activity, plan, result);
            LogExit(logger, plan, startTimestamp, result);
            return result;
        }
        catch (Exception ex) when (plan.CaptureException)
        {
            CaptureException(activity, plan, logger, ex);
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            RecordCallMetrics(plan, startTimestamp);
            activity?.Dispose();
        }
    }

    private Activity? StartActivity(IInvocation invocation, InstrumentationPlan plan)
    {
        TraceSettings trace = plan.Trace!;
        Activity? activity = _telemetry.ActivitySource.StartActivity(trace.SpanName, trace.Kind);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("code.function", plan.OperationName);
        if (trace.RecordArguments)
        {
            AddArgumentTags(activity, invocation);
        }

        return activity;
    }

    private static void AddArgumentTags(Activity activity, IInvocation invocation)
    {
        var parameters = invocation.Method.GetParameters();
        for (int i = 0; i < parameters.Length && i < invocation.Arguments.Length; i++)
        {
            activity.SetTag($"dekori.arg.{parameters[i].Name}", invocation.Arguments[i]?.ToString());
        }
    }

    private static void RecordReturnValue(Activity? activity, InstrumentationPlan plan, object? result)
    {
        if (activity is not null && plan.Trace!.RecordReturnValue && result is not null)
        {
            activity.SetTag("dekori.return", result.ToString());
        }
    }

    private void RecordCallMetrics(InstrumentationPlan plan, long startTimestamp)
    {
        if (plan.Metric is not { } metric)
        {
            return;
        }

        var tags = new TagList { { "code.function", plan.OperationName }, { "code.namespace", plan.TypeName } };
        if (metric.RecordCount)
        {
            _telemetry.Counter($"{metric.BaseName}.calls").Add(1, tags);
        }

        if (metric.RecordDuration)
        {
            double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            _telemetry.Histogram($"{metric.BaseName}.duration", "ms").Record(elapsedMs, tags);
        }
    }

    private void CaptureException(Activity? activity, InstrumentationPlan plan, ILogger logger, Exception ex)
    {
        // Annotate the span we started, or the ambient one if [CaptureException] is used without [Trace].
        Activity? target = activity ?? Activity.Current;
        target?.AddException(ex);
        target?.SetStatus(ActivityStatusCode.Error, ex.Message);

        var tags = new TagList
        {
            { "code.function", plan.OperationName },
            { "code.namespace", plan.TypeName },
            { "error.type", ex.GetType().FullName },
        };
        _telemetry.Counter($"{plan.ErrorMetricBaseName}.errors").Add(1, tags);

        logger.LogError(ex, "Dekori ✖ {Operation} failed with {ExceptionType}", plan.OperationName, ex.GetType().Name);
    }

    private static void LogEntry(ILogger logger, IInvocation invocation, InstrumentationPlan plan)
    {
        if (plan.Log is not { } log || !logger.IsEnabled(log.Level))
        {
            return;
        }

        if (log.LogArguments)
        {
            logger.Log(log.Level, "Dekori → {Operation}({Arguments})", plan.OperationName, FormatArguments(invocation));
        }
        else
        {
            logger.Log(log.Level, "Dekori → {Operation}", plan.OperationName);
        }
    }

    private static void LogExit(ILogger logger, InstrumentationPlan plan, long startTimestamp, object? result)
    {
        if (plan.Log is not { } log || !logger.IsEnabled(log.Level))
        {
            return;
        }

        double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        if (log.LogResult)
        {
            logger.Log(log.Level, "Dekori ← {Operation} = {Result} ({ElapsedMs:F2} ms)", plan.OperationName, result, elapsedMs);
        }
        else
        {
            logger.Log(log.Level, "Dekori ← {Operation} ({ElapsedMs:F2} ms)", plan.OperationName, elapsedMs);
        }
    }

    private static string FormatArguments(IInvocation invocation)
    {
        if (invocation.Arguments.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", invocation.Arguments.Select(static a => a?.ToString() ?? "null"));
    }

    private ILogger GetLogger(string category) =>
        _loggers.GetOrAdd(category, static (c, factory) => factory.CreateLogger($"Dekori.{c}"), _loggerFactory);
}
