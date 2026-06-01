using Microsoft.Extensions.Logging;

namespace Dekori.Demo;

/// <summary>A plain payload type returned by the generic repository sample.</summary>
public sealed class Widget
{
    public string Name { get; set; } = "demo-widget";

    public override string ToString() => Name;
}

public interface IOrderService
{
    Task<string> PlaceOrderAsync(string sku, int quantity);

    void Cancel(string orderId);
}

/// <summary>
/// Class-level <c>[Instrument]</c> opts every method into trace + metrics + exception capture.
/// <c>[LogCall]</c> layers structured entry/exit logging onto the async method.
/// </summary>
[Instrument]
public sealed class OrderService : IOrderService
{
    [LogCall(Level = LogLevel.Information, LogArguments = true, LogResult = true)]
    public async Task<string> PlaceOrderAsync(string sku, int quantity)
    {
        await Task.Delay(120); // simulate I/O — the span must cover this awaited work

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return $"order-{Guid.NewGuid():N}";
    }

    public void Cancel(string orderId)
    {
        // Only class-level instrumentation applies here (trace + metrics + exception capture).
    }
}

public interface IRepository<T>
{
    T GetById(int id);
}

/// <summary>Generic sample proving generic-class instrumentation works unchanged.</summary>
public sealed class InMemoryRepository<T> : IRepository<T>
    where T : new()
{
    [Trace]
    [Metric]
    public T GetById(int id) => new();
}
