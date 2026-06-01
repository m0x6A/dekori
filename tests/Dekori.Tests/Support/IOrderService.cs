namespace Dekori.Tests.Support;

public interface IOrderService
{
    bool Place(string sku, int quantity);
}
