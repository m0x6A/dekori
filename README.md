# Dekori

Attribute-driven instrumentation for .NET. Decorate your classes and methods with attributes and
get OpenTelemetry **traces/spans**, **metrics**, **logs** and **exception capture** automatically —
no manual `ActivitySource`/`Meter` plumbing in your business code.

Dekori uses [Castle DynamicProxy](https://github.com/castleproject/Core) to intercept calls on
services resolved from the DI container, and emits to OpenTelemetry-native primitives
(`System.Diagnostics.ActivitySource`, `System.Diagnostics.Metrics.Meter`,
`Microsoft.Extensions.Logging.ILogger`), so it works with any OpenTelemetry exporter.

## Quick start

```csharp
// 1. Decorate your service.
public interface IOrderService
{
    Task<string> PlaceOrderAsync(string sku, int quantity);
}

[Instrument] // class-level: trace + metrics + exception capture on every method
public sealed class OrderService : IOrderService
{
    [LogCall(Level = LogLevel.Information, LogArguments = true, LogResult = true)]
    public async Task<string> PlaceOrderAsync(string sku, int quantity)
    {
        await Task.Delay(120);
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        return $"order-{Guid.NewGuid():N}";
    }
}

// 2. Register Dekori and the instrumented service.
builder.Services.AddDekori();
builder.Services.AddInstrumented<IOrderService, OrderService>();

// 3. Subscribe OpenTelemetry to the "Dekori" source/meter.
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Dekori").AddConsoleExporter())
    .WithMetrics(m => m.AddMeter("Dekori").AddConsoleExporter());
```

Resolve `IOrderService` as usual — every call is now traced, metered, logged, and its exceptions
captured (and rethrown).

## Attributes

| Attribute             | Target         | Effect |
|-----------------------|----------------|--------|
| `[Instrument]`        | class          | Opts every method into the default plan: trace + metrics + exception capture. |
| `[Trace]`             | method / class | Starts a span. `Name`, `Kind`, `RecordArguments`, `RecordReturnValue`. |
| `[Metric]`            | method / class | Invocation counter `{name}.calls`, duration histogram `{name}.duration`, error counter `{name}.errors`. `Name`, `RecordDuration`, `RecordCount`. |
| `[CaptureException]`  | method / class | Records exceptions on the span, sets error status, increments the error counter, logs at error level — then **rethrows**. |
| `[LogCall]`           | method / class | Structured entry/exit logs. `Level`, `LogArguments`, `LogResult`. |
| `[NoInstrument]`      | method         | Opts a single method out when its class is `[Instrument]`ed. |

Attributes are composable — stack `[Trace]`, `[Metric]`, `[CaptureException]` and `[LogCall]` on one
method. A method-level attribute overrides the class-level one for the same concern.

Argument/return-value capture is **off by default** for PII safety; opt in per attribute or globally
via `DekoriOptions`.

## Registration

```csharp
services.AddDekori(options =>
{
    options.ActivitySourceName = "Dekori"; // what OTel AddSource(...) subscribes to
    options.MeterName          = "Dekori"; // what OTel AddMeter(...) subscribes to
    options.DefaultLogLevel    = LogLevel.Debug;
});

services.AddInstrumented<IOrderService, OrderService>();  // interface proxy (recommended)
services.AddInstrumented<Repository<Widget>>();           // class proxy (virtual members only)
```

Generic classes (`Repository<T>`) and generic methods are supported with no extra configuration.

## Solution layout

```
src/Dekori/            # the library
samples/Dekori.Demo/   # runnable console demo wiring OpenTelemetry console exporters
tests/Dekori.Tests/    # xUnit + Shouldly + NSubstitute, BDD/TDD specs
```

## Build, test, run

```bash
dotnet build
dotnet test
dotnet run --project samples/Dekori.Demo
```

The test suite asserts against **real** in-process signals via `ActivityListener`/`MeterListener`,
including that async spans cover the full awaited operation and that captured exceptions are
rethrown.
