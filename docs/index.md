# Dekori

Attribute-driven instrumentation for .NET. Decorate your classes and methods with attributes and
get OpenTelemetry **traces/spans**, **metrics**, **logs** and **exception capture** automatically —
no manual `ActivitySource`/`Meter` plumbing in your business code.

```csharp
[Instrument]
public sealed class OrderService : IOrderService
{
    [LogCall(Level = LogLevel.Information, LogArguments = true)]
    public async Task<string> PlaceOrderAsync(string sku, int quantity)
    {
        await Task.Delay(120);
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        return $"order-{Guid.NewGuid():N}";
    }
}
```

## Get started

- [Getting started](articles/getting-started.md) — install, register, wire OpenTelemetry
- [Attribute reference](articles/attributes.md) — every attribute and its properties
- [Configuration](articles/options.md) — `DekoriOptions` and global defaults
- [Advanced topics](articles/advanced.md) — async, PII safety, multiple sources, span links
