# Advanced topics

## Async support

The interceptor is built on `AsyncInterceptorBase`. For `Task`- or `ValueTask`-returning methods,
the span and metric duration cover the **full asynchronous operation** — the span is stopped only
after the returned task completes, not at the first `await`. Exception capture and exit logging
also observe the final outcome of the awaited work.

```csharp
[Trace]
[Metric]
public async Task<string> PlaceOrderAsync(string sku, int quantity)
{
    await Task.Delay(120);  // the span covers this delay
    return $"order-{Guid.NewGuid():N}";
}
```

## PII safety

Argument and return-value capture is **off by default** across all attributes. Enable it
explicitly per method, only where it is safe:

```csharp
// Safe: IDs are not PII.
[Trace(RecordArguments = true)]
public Order GetOrder(int orderId) { ... }

// Safe: log level and log the return value.
[LogCall(LogArguments = true, LogResult = true)]
public Product[] Search(string keyword) { ... }
```

Global capture can be enabled in options, but only when every instrumented method is safe to
export:

```csharp
builder.Services.AddDekori(options =>
{
    options.CaptureArgumentsByDefault   = true;   // applies to every [Trace]
    options.CaptureReturnValueByDefault = true;
});
```

## Root spans

Use `NewRoot = true` to start a fresh trace that is not parented to any ambient span — useful for
background jobs, scheduled tasks, or test entry points:

```csharp
[Trace(NewRoot = true, Name = "scheduler.tick")]
public void ExecuteScheduledTick() { ... }
```

An explicit `[TraceParent]` parameter still wins over `NewRoot = true`.

## Explicit trace parent

When a span must be a child of a specific parent rather than `Activity.Current` — for example
when dispatching work to a background thread or processing a queue message — pass the parent
explicitly:

```csharp
public interface IJobProcessor
{
    Task ProcessAsync([TraceParent] ActivityContext parent, Job payload);
}

// Capture before dispatching:
ActivityContext ctx = Activity.Current?.Context ?? default;
_ = Task.Run(() => processor.ProcessAsync(ctx, payload));
```

## Span links

Links express a causal relationship between spans without creating parent-child nesting — useful
for fan-in aggregations, batch processors, or merge steps:

```csharp
public interface IAggregator
{
    [Trace]
    Task AggregateAsync([TraceLink] IEnumerable<ActivityContext> sources, Report report);
}

await aggregator.AggregateAsync(sourceSpanContexts, report);
```

## Class-level vs method-level precedence

A method-level attribute overrides the class-level one for the same concern:

```csharp
[Trace]       // applies to every method …
[LogCall]     // … as does this
public sealed class ReportService : IReportService
{
    // Overrides the class-level [Trace] for this method only.
    [Trace(Name = "reports.generate", RecordArguments = true)]
    public Report Generate(ReportSpec spec) { ... }

    // Inherits class-level [Trace] and [LogCall] unchanged.
    public void Archive(int reportId) { ... }

    // Opts out of all instrumentation.
    [NoInstrument]
    internal bool IsReady() => _ready;
}
```

## Generic types and methods

Generic classes and generic interface methods work without extra configuration:

```csharp
public sealed class InMemoryRepository<T> : IRepository<T> where T : new()
{
    [Trace(Source = "MyApp.Database")]
    [Metric]
    public T GetById(int id) => new();
}

services.AddInstrumented<IRepository<Widget>, InMemoryRepository<Widget>>();
```

## How it works

1. `AddDekori` registers a Castle `ProxyGenerator` and a `DekoriInterceptor` singleton.
2. `AddInstrumented<TInterface, TImplementation>` registers the implementation, then exposes the
   interface as a factory that wraps it in a Castle interface proxy.
3. On the first call to any method, the interceptor resolves the method's `InstrumentationPlan`
   via reflection and caches it — reflection never runs on the hot path.
4. For every subsequent call the interceptor uses the cached plan: starts a span, calls through
   to the real method (awaiting it for async), records metrics and logs, and captures exceptions.

**Requirements:**

- Services must be registered via `AddInstrumented` — resolving the concrete class directly
  bypasses the proxy.
- For `AddInstrumented<T>` (class proxy), only `virtual`/`abstract` members are intercepted.
  Non-virtual methods are called directly without instrumentation.
