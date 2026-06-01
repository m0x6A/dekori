namespace Dekori.Tests.Support;

public sealed class Greeter : IGreeter
{
    [Trace]
    public string Greet(string name) => $"Hello {name}";

    [Trace(Name = "greet.async", RecordArguments = true, RecordReturnValue = true)]
    public async Task<string> GreetAsync(string name, int delayMs)
    {
        await Task.Delay(delayMs);
        return $"Hello {name}";
    }
}
