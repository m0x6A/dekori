# Getting started

## Installation

```bash
dotnet add package Dekori
```

Requires .NET 10 or later.

## Quick start

```csharp
// 1. Define an interface and implement it.
public interface IOrderService
{
    Task<string> PlaceOrderAsync(string sku, int quantity);
    void Cancel(string orderId);
}

// 2. Decorate the implementation.
[Instrument]  // trace + metrics + exception capture on every method
public sealed class OrderService : IOrderService
{
    [LogCall(Level = LogLevel.Information, LogArguments = true, LogResult = true)]
    public async Task<string> PlaceOrderAsync(string sku, int quantity)
    {
        await Task.Delay(120);
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        return $"order-{Guid.NewGuid():N}";
    }

    public void Cancel(string orderId) { }
}

// 3. Register and wire OpenTelemetry.
builder.Services.AddDekori();
builder.Services.AddInstrumented<IOrderService, OrderService>();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Dekori").AddConsoleExporter())
    .WithMetrics(m => m.AddMeter("Dekori").AddConsoleExporter());
```

Resolve `IOrderService` from the container as normal — every call is now traced, metered, and
logged, and exceptions are captured and rethrown.

## Registration

### Interface proxy (recommended)

```csharp
services.AddInstrumented<IOrderService, OrderService>();

// Explicit lifetime:
services.AddInstrumented<IPaymentGateway, StripeGateway>(ServiceLifetime.Singleton);
```

All interface members are interceptable regardless of whether they are `virtual`.

### Class proxy

```csharp
services.AddInstrumented<ReportGenerator>();
services.AddInstrumented<ReportGenerator>(ServiceLifetime.Scoped);
```

Only `virtual` (or `abstract`) members are intercepted. Prefer the interface overload.

## OpenTelemetry wiring

Subscribe to the source and meter names configured in `AddDekori` (both default to `"Dekori"`):

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("MyApp"))
    .WithTracing(t =>
    {
        t.AddSource("Dekori")
         .AddConsoleExporter();   // or AddOtlpExporter(), AddZipkinExporter(), ...
    })
    .WithMetrics(m =>
    {
        m.AddMeter("Dekori")
         .AddConsoleExporter();
    });

// Logging goes through the standard ILoggerFactory:
builder.Logging.AddOpenTelemetry(l => l.AddOtlpExporter());
```
