namespace Dekori.Tests.Support;

public sealed class OrderService(IInventory inventory) : IOrderService
{
    [Trace]
    [Metric]
    [CaptureException]
    public bool Place(string sku, int quantity) => inventory.Reserve(sku, quantity);
}
