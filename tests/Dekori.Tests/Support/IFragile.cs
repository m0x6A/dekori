namespace Dekori.Tests.Support;

/// <summary>Exception-capture sample contract (sync + async).</summary>
public interface IFragile
{
    void Break();

    Task BreakAsync();
}
