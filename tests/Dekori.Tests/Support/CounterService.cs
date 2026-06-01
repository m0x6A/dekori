namespace Dekori.Tests.Support;

public sealed class CounterService : ICounterService
{
    [Metric(Name = "demo.work")]
    public void DoWork()
    {
    }
}
