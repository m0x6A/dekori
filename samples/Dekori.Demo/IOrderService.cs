namespace Dekori.Demo;

public interface IOrderService
{
    Task<string> PlaceOrderAsync(string sku, int quantity);

    void Cancel(string orderId);
}
