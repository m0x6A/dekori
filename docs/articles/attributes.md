# Attribute reference

All attributes are in the `Dekori` namespace. Attributes are composable — stack any combination on
one method. A method-level attribute overrides the class-level one for the same concern.

## `[Instrument]`

Class-level convenience. Opts every public/virtual method into the default plan: trace + metrics +
exception capture. Equivalent to placing `[Trace]`, `[Metric]`, and `[CaptureException]` on every
method.

```csharp
[Instrument]
public sealed class InventoryService : IInventoryService
{
    // All methods are traced, metered, and exception-captured.

    [NoInstrument]
    public int Count() => _items.Count;  // opted out
}
```

## `[Trace]`

Starts an OpenTelemetry span (`Activity`) around the decorated method. For `Task`/`ValueTask`
methods the span covers the full awaited operation — not just the synchronous prologue.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Name` | `string?` | `TypeName.MethodName` | Explicit span name. |
| `Source` | `string?` | `DekoriOptions.ActivitySourceName` | Named `ActivitySource` to emit from. |
| `Kind` | `ActivityKind` | `Internal` | OTel span kind. |
| `NewRoot` | `bool` | `false` | Ignore ambient span; start a new root trace. |
| `RecordArguments` | `bool` | `false` | Record method arguments as span tags. |
| `RecordReturnValue` | `bool` | `false` | Record the return value as a span tag. |

```csharp
// Defaults — span name is "OrderService.GetOrder".
[Trace]
public Order GetOrder(int id) { ... }

// Custom name and client kind.
[Trace(Name = "http.get_order", Kind = ActivityKind.Client)]
public async Task<HttpResponseMessage> CallAsync(string url) { ... }

// Emit from a dedicated database source.
[Trace(Source = "MyApp.Database")]
public Widget QueryDatabase(int id) { ... }

// New root trace — ignores Activity.Current.
[Trace(NewRoot = true)]
public void StartBackgroundJob() { ... }

// Capture args and return value (opt in per-method; off by default for PII safety).
[Trace(RecordArguments = true, RecordReturnValue = true)]
public string GetLabel(int id) { ... }
```

**Span tags set automatically:**

| Tag | Value |
|-----|-------|
| `code.function` | `TypeName.MethodName` |
| `dekori.arg.{paramName}` | `param.ToString()` (when `RecordArguments = true`) |
| `dekori.return` | `result.ToString()` (when `RecordReturnValue = true`) |

## `[Metric]`

Records an invocation counter, a duration histogram, and (with `[CaptureException]`) an error
counter on each call.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Name` | `string?` | `DekoriOptions.DefaultMetricName` | Base name for the instruments. |
| `RecordDuration` | `bool` | `true` | Emit the duration histogram. |
| `RecordCount` | `bool` | `true` | Emit the invocation counter. |

```csharp
// Default names: dekori.method.calls, dekori.method.duration.
[Metric]
public void Enqueue(Job job) { ... }

// Custom base → orders.calls, orders.duration, orders.errors.
[Metric(Name = "orders")]
public async Task<string> PlaceOrderAsync(string sku, int qty) { ... }

// Counter only, no histogram.
[Metric(RecordDuration = false)]
public bool IsHealthy() { ... }
```

**Instruments emitted:**

| Instrument | Type | Unit | Tags |
|-----------|------|------|------|
| `{name}.calls` | Counter | — | `code.function`, `code.namespace` |
| `{name}.duration` | Histogram | `ms` | `code.function`, `code.namespace` |
| `{name}.errors` | Counter | — | `code.function`, `code.namespace`, `error.type` |

The `.errors` counter is only incremented when `[CaptureException]` is also present.

## `[CaptureException]`

Captures exceptions thrown by the method:

- Adds the exception as an event on the active span (or `Activity.Current` if no span was started).
- Sets span status to `Error`.
- Increments the `{name}.errors` counter.
- Logs at `LogLevel.Error` with the exception details.
- **Always rethrows.**

```csharp
[Trace]
[Metric(Name = "payments")]
[CaptureException]
public async Task<Receipt> ChargeAsync(string cardToken, decimal amount) { ... }
```

Can be used without `[Trace]` — it will record onto the ambient span and still count errors and
emit the log:

```csharp
[Metric]
[CaptureException]
public void UpdateCache(int id) { ... }
```

## `[LogCall]`

Emits structured entry and exit log messages. Not included in `[Instrument]`'s default plan — opt
in explicitly.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Level` | `LogLevel` | `DekoriOptions.DefaultLogLevel` | Log level for entry/exit messages. |
| `LogArguments` | `bool` | `false` | Include method arguments in the entry log. |
| `LogResult` | `bool` | `false` | Include the return value in the exit log. |

```csharp
[LogCall]
public void Process(Event e) { ... }

[LogCall(Level = LogLevel.Information, LogArguments = true, LogResult = true)]
public async Task<string> PlaceOrderAsync(string sku, int quantity) { ... }
```

**Log messages:**

| | Template |
|---|---------|
| Entry | `Dekori → {Operation}` |
| Entry (with args) | `Dekori → {Operation}({Arguments})` |
| Exit | `Dekori ← {Operation} ({ElapsedMs:F2} ms)` |
| Exit (with result) | `Dekori ← {Operation} = {Result} ({ElapsedMs:F2} ms)` |

Logger category: `Dekori.{TypeName}`.

## `[NoInstrument]`

Opts a single method out of instrumentation even when its declaring type carries `[Instrument]` or
class-level telemetry attributes.

```csharp
[Instrument]
public sealed class CatalogService : ICatalogService
{
    public async Task<Product[]> SearchAsync(string query) { ... }  // instrumented

    [NoInstrument]
    public int GetVersion() => _version;  // exempt
}
```

## `[TraceParent]` (parameter attribute)

Marks a parameter as the explicit parent for the span started by `[Trace]`. Overrides
`Activity.Current`; a `default`/`null` value falls back to normal parenting.

Supported types: `ActivityContext`, `Activity`.

```csharp
public interface IJobProcessor
{
    Task ProcessAsync([TraceParent] ActivityContext parent, Job job);
}

// Captures the caller's context before dispatching to a background thread.
ActivityContext ctx = Activity.Current?.Context ?? default;
_ = Task.Run(() => processor.ProcessAsync(ctx, job));
```

## `[TraceLink]` (parameter attribute)

Marks a parameter that supplies span links for the span started by `[Trace]`. Links express causal
relationships without parent-child hierarchy — useful for fan-in aggregations or batch processors.

Supported types: `ActivityContext`, `ActivityLink`, `IEnumerable<ActivityContext>`,
`IEnumerable<ActivityLink>`. Multiple parameters may each be marked; their links are combined.
`null` or empty values contribute no links.

```csharp
public interface IAggregator
{
    Task AggregateAsync([TraceLink] IEnumerable<ActivityContext> sourceContexts, Report report);
}

await aggregator.AggregateAsync(sourceSpanContexts, report);
```
