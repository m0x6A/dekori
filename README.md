# Dekori

Attribute-driven instrumentation for .NET. Decorate your classes and methods with attributes and
get OpenTelemetry **traces/spans**, **metrics**, **logs** and **exception capture** automatically —
no manual `ActivitySource`/`Meter` plumbing in your business code.

Dekori uses [Castle DynamicProxy](https://github.com/castleproject/Core) to intercept calls on
services resolved from the DI container, and emits to OpenTelemetry-native primitives
(`System.Diagnostics.ActivitySource`, `System.Diagnostics.Metrics.Meter`,
`Microsoft.Extensions.Logging.ILogger`), so it works with any OpenTelemetry exporter.

---

## Table of contents

- [Installation](#installation)
- [Quick start](#quick-start)
- [Registration](#registration)
- [Attribute reference](#attribute-reference)
  - [\[Instrument\]](#instrument)
  - [\[Trace\]](#trace)
  - [\[Metric\]](#metric)
  - [\[CaptureException\]](#captureexception)
  - [\[LogCall\]](#logcall)
  - [\[NoInstrument\]](#noinstrument)
  - [\[TraceParent\]](#traceparent-parameter)
  - [\[TraceLink\]](#tracelink-parameter)
- [DekoriOptions reference](#dekorioptions-reference)
- [OpenTelemetry wiring](#opentelemetry-wiring)
- [Advanced topics](#advanced-topics)
- [How it works](#how-it-works)
- [Build, test, run](#build-test-run)

---

## Installation

```bash
dotnet add package Dekori
```

Requires .NET 10 or later. Brings in `Castle.Core`, `Castle.Core.AsyncInterceptor`, and
`Microsoft.Extensions.DependencyInjection.Abstractions` automatically.

---

## Quick start

```csharp
// 1. Define an interface and implement it.
public interface IOrderService
{
    Task<string> PlaceOrderAsync(string sku, int quantity);
    void Cancel(string orderId);
}

// 2. Decorate the implementation with attributes.
[Instrument]  // class-level: trace + metrics + exception capture on every method
public sealed class OrderService : IOrderService
{
    // Layer structured logging on top of the class-level plan.
    [LogCall(Level = LogLevel.Information, LogArguments = true, LogResult = true)]
    public async Task<string> PlaceOrderAsync(string sku, int quantity)
    {
        await Task.Delay(120);  // the span covers this awaited work

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return $"order-{Guid.NewGuid():N}";
    }

    public void Cancel(string orderId)
    {
        // Inherits class-level trace + metrics + exception capture.
    }
}

// 3. Register Dekori and wire up OpenTelemetry in your host setup.
builder.Services.AddDekori();
builder.Services.AddInstrumented<IOrderService, OrderService>();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Dekori").AddConsoleExporter())
    .WithMetrics(m => m.AddMeter("Dekori").AddConsoleExporter());
```

Resolve `IOrderService` from the container as normal — every call is now traced, metered, logged,
and its exceptions captured and rethrown.

---

## Registration

### `AddDekori`

Call once during host setup. Registers the proxy generator, the interceptor, and all shared
telemetry primitives.

```csharp
builder.Services.AddDekori();

// or with options:
builder.Services.AddDekori(options =>
{
    options.ActivitySourceName = "MyApp";
    options.MeterName          = "MyApp";
    options.DefaultLogLevel    = LogLevel.Debug;
});
```

### `AddInstrumented<TInterface, TImplementation>` — interface proxy (recommended)

Registers the implementation and exposes the interface as an instrumented proxy wrapping it.
All interface members are interceptable, regardless of whether they are virtual.

```csharp
services.AddInstrumented<IOrderService, OrderService>();

// Specify the lifetime explicitly (Transient is the default):
services.AddInstrumented<IPaymentGateway, StripeGateway>(ServiceLifetime.Singleton);
```

Resolve `IOrderService` from the container — you get the proxy, not the raw implementation.

### `AddInstrumented<T>` — class proxy

Registers the concrete type itself as an instrumented class proxy. Only `virtual` (or `abstract`)
members can be intercepted; non-virtual methods bypass the proxy silently.

```csharp
services.AddInstrumented<ReportGenerator>();
services.AddInstrumented<ReportGenerator>(ServiceLifetime.Scoped);
```

Prefer the interface overload. Use the class overload only when no interface exists or when the
caller must hold the concrete type.

---

## Attribute reference

Attributes are **composable**: stack any combination on one method. A method-level attribute
overrides the class-level attribute for the same concern.

### `[Instrument]`

Class-level convenience attribute. Opts every public/virtual method into the default plan: trace,
metrics, and exception capture. Equivalent to placing `[Trace]`, `[Metric]`, and
`[CaptureException]` on every method.

```csharp
[Instrument]
public sealed class InventoryService : IInventoryService
{
    // All methods are traced, metered, and exception-captured.

    [NoInstrument]
    public int Count() => _items.Count;  // this one is opted out
}
```

### `[Trace]`

Starts an OpenTelemetry span (`Activity`) around the decorated method. The span covers async
continuations in full — it is stopped only after the returned `Task`/`ValueTask` completes.

```csharp
// Minimal — span name defaults to "TypeName.MethodName".
[Trace]
public Order GetOrder(int id) { ... }

// Custom span name.
[Trace(Name = "orders.get")]
public Order GetOrder(int id) { ... }

// Server span kind (useful for inbound HTTP handlers, gRPC, etc.).
[Trace(Kind = ActivityKind.Client)]
public async Task<HttpResponseMessage> CallExternalApiAsync(string url) { ... }

// Emit from a named source instead of the default one.
[Trace(Source = "MyApp.Database")]
public Widget QueryDatabase(int id) { ... }

// Start a fresh root trace, ignoring the ambient span.
[Trace(NewRoot = true)]
public void StartBackgroundJob() { ... }

// Capture method arguments and return value as span tags.
// Off by default — enable per-method or globally via DekoriOptions.
[Trace(RecordArguments = true, RecordReturnValue = true)]
public string GetLabel(int id) { ... }
```

**Span tags set by Dekori:**

| Tag | Value |
|-----|-------|
| `code.function` | `TypeName.MethodName` |
| `dekori.arg.{paramName}` | `param.ToString()` (when `RecordArguments = true`) |
| `dekori.return` | `result.ToString()` (when `RecordReturnValue = true`) |

### `[Metric]`

Records metrics on each call: an invocation counter, a duration histogram, and (together with
`[CaptureException]`) an error counter.

```csharp
// Default instrument names: dekori.method.calls, dekori.method.duration, dekori.method.errors.
[Metric]
public void Enqueue(Job job) { ... }

// Custom base name → orders.calls, orders.duration, orders.errors.
[Metric(Name = "orders")]
public async Task<string> PlaceOrderAsync(string sku, int quantity) { ... }

// Count only, skip duration (e.g., to avoid a histogram allocation).
[Metric(RecordDuration = false)]
public bool IsHealthy() { ... }
```

**Metric instruments:**

| Instrument | Type | Unit | Tags |
|-----------|------|------|------|
| `{name}.calls` | Counter | — | `code.function`, `code.namespace` |
| `{name}.duration` | Histogram | `ms` | `code.function`, `code.namespace` |
| `{name}.errors` | Counter | — | `code.function`, `code.namespace`, `error.type` |

The `.errors` counter is only incremented when `[CaptureException]` is also present. The base
name for the error counter is taken from `[Metric(Name = ...)]` when present, otherwise from
`DekoriOptions.DefaultMetricName`.

### `[CaptureException]`

Captures exceptions thrown by the decorated method:

- Adds the exception as an event on the active span (or the ambient `Activity.Current` if no span
  is started by the method).
- Sets the span status to `Error`.
- Increments the `{name}.errors` counter.
- Logs at `LogLevel.Error` with the exception and type.
- **Always rethrows** — instrumentation never swallows exceptions.

```csharp
[Trace]
[Metric(Name = "payments")]
[CaptureException]
public async Task<Receipt> ChargeAsync(string cardToken, decimal amount) { ... }
```

Can be used without `[Trace]` — in that case it records onto `Activity.Current` (the ambient span
created by upstream middleware) and still increments the error counter and emits the log.

```csharp
// Exception capture + metrics only, no span started by this method.
[Metric]
[CaptureException]
public void UpdateCache(int id) { ... }
```

### `[LogCall]`

Emits structured entry and exit log messages around the decorated method. Not included in
`[Instrument]`'s default plan — opt in explicitly.

```csharp
// Logs at the default level (DekoriOptions.DefaultLogLevel, which is Debug).
[LogCall]
public void Process(Event e) { ... }

// Logs at Information level.
[LogCall(Level = LogLevel.Information)]
public async Task<string> PlaceOrderAsync(string sku, int quantity) { ... }

// Include arguments in the entry log and the return value in the exit log.
// Off by default for PII safety.
[LogCall(Level = LogLevel.Information, LogArguments = true, LogResult = true)]
public async Task<string> PlaceOrderAsync(string sku, int quantity) { ... }
```

**Log messages emitted:**

| Direction | Template | Notes |
|-----------|----------|-------|
| Entry | `Dekori → {Operation}` | Always |
| Entry | `Dekori → {Operation}({Arguments})` | When `LogArguments = true` |
| Exit | `Dekori ← {Operation} ({ElapsedMs:F2} ms)` | Always |
| Exit | `Dekori ← {Operation} = {Result} ({ElapsedMs:F2} ms)` | When `LogResult = true` |

The logger category is `Dekori.{TypeName}`. Exit logs include the elapsed time in milliseconds.

### `[NoInstrument]`

Opt-out escape hatch. Excludes a single method from instrumentation even when the declaring type
carries `[Instrument]` or class-level telemetry attributes.

```csharp
[Instrument]
public sealed class CatalogService : ICatalogService
{
    public async Task<Product[]> SearchAsync(string query) { ... }  // instrumented

    [NoInstrument]
    public int GetVersion() => _version;  // exempt — too cheap/frequent to instrument
}
```

### `[TraceParent]` (parameter)

Marks a method parameter as the explicit parent context for the span. The span uses this context
as its parent instead of the ambient `Activity.Current`. A `default`/`null` value falls back to
normal parenting.

Supported parameter types: `ActivityContext`, `Activity`.

```csharp
public interface IWorkerQueue
{
    Task ProcessAsync([TraceParent] ActivityContext parentContext, Job job);
}

// The worker span is a child of the caller's span, not of whatever happens to be
// ambient in the background thread.
await queue.ProcessAsync(Activity.Current!.Context, job);
```

### `[TraceLink]` (parameter)

Marks a parameter that supplies one or more span links to the span started by `[Trace]`. Links
express a causal relationship between spans without creating a parent-child hierarchy — useful for
fan-in aggregations, batch processors, or whenever one span logically follows from several others.

Supported parameter types: `ActivityContext`, `ActivityLink`,
`IEnumerable<ActivityContext>`, `IEnumerable<ActivityLink>`.

Multiple parameters may each be marked; their links are combined. `null` or empty values
contribute no links.

```csharp
public interface IAggregator
{
    Task AggregateAsync([TraceLink] IEnumerable<ActivityContext> sourceContexts, Report report);
}

// Each source span appears as a link in the aggregation span.
await aggregator.AggregateAsync(sourceSpanContexts, report);
```

---

## DekoriOptions reference

All options are configured via the `AddDekori` callback:

```csharp
builder.Services.AddDekori(options =>
{
    options.ActivitySourceName           = "MyApp";
    options.MeterName                    = "MyApp";
    options.Version                      = "1.0";
    options.DefaultMetricName            = "myapp.method";
    options.DefaultLogLevel              = LogLevel.Debug;
    options.CaptureArgumentsByDefault    = false;
    options.CaptureReturnValueByDefault  = false;
    options.AdditionalActivitySourceNames.Add("MyApp.Database");
});
```

| Option | Default | Description |
|--------|---------|-------------|
| `ActivitySourceName` | `"Dekori"` | Name passed to `AddSource(...)` in OpenTelemetry tracing setup. |
| `MeterName` | `"Dekori"` | Name passed to `AddMeter(...)` in OpenTelemetry metrics setup. |
| `Version` | `null` | Version reported for the `ActivitySource` and `Meter`. |
| `DefaultMetricName` | `"dekori.method"` | Base name for metric instruments when `[Metric]` has no explicit `Name`. |
| `DefaultLogLevel` | `Debug` | Log level used when `[LogCall]` has no explicit `Level`. |
| `CaptureArgumentsByDefault` | `false` | When true, every `[Trace]` records arguments as span tags unless the attribute opts out. |
| `CaptureReturnValueByDefault` | `false` | When true, every `[Trace]` records the return value as a span tag unless the attribute opts out. |
| `AdditionalActivitySourceNames` | `[]` | Extra `ActivitySource` names to pre-create at startup. Use when methods emit from a custom source (`[Trace(Source = "...")]`) — pre-registering ensures the source is enumerable and subscribable via `DekoriTelemetry.SourceNames`. |

---

## OpenTelemetry wiring

Dekori emits to standard .NET OpenTelemetry primitives, so any OTel exporter works. Subscribe
using the names you configured in `DekoriOptions`:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("MyApp"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("MyApp")               // matches ActivitySourceName
            .AddSource("MyApp.Database")      // matches an AdditionalActivitySourceNames entry
            .AddConsoleExporter();            // or AddOtlpExporter(), AddZipkinExporter(), ...
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("MyApp")               // matches MeterName
            .AddConsoleExporter();
    });
```

Logging goes through the standard `ILoggerFactory`, so configure it as you normally would:

```csharp
builder.Logging.AddOpenTelemetry(logging => logging.AddOtlpExporter());
```

### Multiple sources

Dekori supports splitting spans across multiple `ActivitySource` instances so teams can subscribe
independently to different layers (e.g. `"MyApp"` for business logic and `"MyApp.Database"` for
storage):

```csharp
builder.Services.AddDekori(options =>
{
    options.ActivitySourceName = "MyApp";
    options.AdditionalActivitySourceNames.Add("MyApp.Database");
});

// In the service:
[Trace(Source = "MyApp.Database")]  // emits from the database source
public Widget QueryDatabase(int id) { ... }
```

Any source name used in a `[Trace(Source = "...")]` attribute that is *not* in
`AdditionalActivitySourceNames` is still created on first call — it just will not appear in
`DekoriTelemetry.SourceNames` until that first call happens.

---

## Advanced topics

### Combining attributes

Attributes are independent and composable. Combine freely:

```csharp
[Trace(Name = "payments.charge", Kind = ActivityKind.Client, RecordArguments = true)]
[Metric(Name = "payments")]
[CaptureException]
[LogCall(Level = LogLevel.Information)]
public async Task<Receipt> ChargeAsync(string cardToken, decimal amount) { ... }
```

### Class-level vs method-level

A class-level attribute applies to every method unless the method overrides it with its own
attribute for the same concern.

```csharp
[Trace]                // applies to all methods ...
[LogCall]              // ... as does this
public sealed class ReportService : IReportService
{
    // Overrides the class-level [Trace] for this method only.
    [Trace(Name = "reports.generate", RecordArguments = true)]
    public Report Generate(ReportSpec spec) { ... }

    // Inherits the class-level [Trace] and [LogCall] unchanged.
    public void Archive(int reportId) { ... }

    // Opts out of all instrumentation.
    [NoInstrument]
    internal bool IsReady() => _ready;
}
```

### Async support

The interceptor is built on `AsyncInterceptorBase`. For `Task`- or `ValueTask`-returning methods,
the span and metric duration cover the **full asynchronous operation**, not just the synchronous
prologue before the first `await`. Exception capture and exit logging also observe the final
outcome of the awaited work.

### PII safety

Argument and return-value capture is **off by default** across all attributes. Enable it
explicitly where it is safe to do so:

```csharp
// Safe: IDs are not PII.
[Trace(RecordArguments = true)]
public Order GetOrder(int orderId) { ... }

// Safe: a search query is not sensitive here.
[LogCall(LogArguments = true, LogResult = true)]
public Product[] Search(string keyword) { ... }

// Avoid enabling globally unless all method arguments and return values are safe to export.
builder.Services.AddDekori(options =>
{
    options.CaptureArgumentsByDefault   = false;  // default — safer
    options.CaptureReturnValueByDefault = false;  // default — safer
});
```

### Generic types and methods

Generic classes and generic interface methods work without any extra configuration:

```csharp
public interface IRepository<T>
{
    T GetById(int id);
}

public sealed class InMemoryRepository<T> : IRepository<T> where T : new()
{
    [Trace(Source = "MyApp.Database")]
    [Metric]
    public T GetById(int id) => new();
}

services.AddInstrumented<IRepository<Widget>, InMemoryRepository<Widget>>();
```

### Explicit trace parent

When a span must be a child of a specific parent rather than `Activity.Current` — for example
when dispatching work to a background thread or processing a queue message — pass the parent
context explicitly using `[TraceParent]`:

```csharp
public interface IJobProcessor
{
    Task RunAsync([TraceParent] ActivityContext parent, JobPayload payload);
}

// Dispatcher:
ActivityContext ctx = Activity.Current?.Context ?? default;
_ = Task.Run(() => processor.RunAsync(ctx, payload));
```

### Root spans

Use `NewRoot = true` on `[Trace]` to start a brand-new trace that is not parented to any ambient
span — useful for background jobs, scheduled tasks, or integration-test entry points:

```csharp
[Trace(NewRoot = true, Name = "scheduler.tick")]
public void ExecuteScheduledTick() { ... }
```

An explicit `[TraceParent]` parameter still wins over `NewRoot = true`.

---

## How it works

1. `AddDekori` registers a Castle `ProxyGenerator` and a `DekoriInterceptor` singleton.
2. `AddInstrumented<TInterface, TImplementation>` registers the implementation, then registers the
   interface as a factory that wraps it in a Castle interface proxy.
3. On the first call to a method, the interceptor resolves its `InstrumentationPlan` (attribute
   inspection via reflection) and caches it — reflection never runs on the hot path.
4. For each subsequent call the interceptor consults the cached plan, starts an `Activity` if
   tracing is requested, calls through to the real method (awaiting it for async), records metrics
   and logs, and captures exceptions — all in a single `async` pipeline.

**Requirements:**

- Services must be registered with `AddInstrumented` — resolving the concrete class directly
  bypasses the proxy.
- For the class-proxy overload (`AddInstrumented<T>`), only `virtual` or `abstract` members are
  intercepted. `sealed`/non-virtual methods are called directly without instrumentation.
- The proxy wraps the DI-resolved instance, so constructor injection in the implementation class
  works normally.

---

## Build, test, run

```bash
dotnet build
dotnet test
dotnet run --project samples/Dekori.Demo
```

The test suite asserts against **real** in-process signals via `ActivityListener`/`MeterListener`,
including that async spans cover the full awaited operation and that captured exceptions are
rethrown.

## AI agent skills

This repository ships two different skills:

- Claude: `.claude/skills/use-dekori/SKILL.md` — repository-maintainer guidance for working on
  Dekori itself
- Copilot: `.github/copilot/skills/use-dekori/SKILL.md` — an installable consumer skill for using
  Dekori as a NuGet package in another .NET application

The Copilot skill is intentionally consumer-focused: it assumes Dekori is already referenced as a
package and guides registration, attributes, and OpenTelemetry wiring instead of repository internals
or demo files.

### Install the Copilot consumer skill

Open the skill file in GitHub and click **Install** to add it to your Copilot agent:

👉 **[Install `use-dekori` Copilot skill](https://github.com/m0x6A/dekori/blob/main/.github/copilot/skills/use-dekori/SKILL.md)**

Or point your Copilot skill manager at:

```
https://github.com/m0x6A/dekori/blob/main/.github/copilot/skills/use-dekori/SKILL.md
```
