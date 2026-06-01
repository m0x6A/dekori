namespace Dekori.Tests.Support;

public sealed class Fragile : IFragile
{
    [Trace]
    [Metric(Name = "demo.fragile")]
    [CaptureException]
    public void Break() => throw new InvalidOperationException("boom");

    [Trace]
    [CaptureException]
    public async Task BreakAsync()
    {
        await Task.Delay(5);
        throw new InvalidOperationException("async boom");
    }
}
