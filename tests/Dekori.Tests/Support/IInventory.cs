namespace Dekori.Tests.Support;

/// <summary>Collaborator used to prove the proxy still delegates (NSubstitute target).</summary>
public interface IInventory
{
    bool Reserve(string sku, int quantity);
}
