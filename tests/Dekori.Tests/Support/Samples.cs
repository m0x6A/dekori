using System.Diagnostics;

namespace Dekori.Tests.Support;

/// <summary>Tracing + async samples.</summary>
public interface IGreeter
{
    string Greet(string name);

    Task<string> GreetAsync(string name, int delayMs);
}

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

/// <summary>Metric sample.</summary>
public interface ICounterService
{
    void DoWork();
}

public sealed class CounterService : ICounterService
{
    [Metric(Name = "demo.work")]
    public void DoWork()
    {
    }
}

/// <summary>Exception-capture samples (sync + async).</summary>
public interface IFragile
{
    void Break();

    Task BreakAsync();
}

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

/// <summary>Logging sample.</summary>
public interface IChatter
{
    int Speak(string phrase);
}

public sealed class Chatter : IChatter
{
    [LogCall(Level = Microsoft.Extensions.Logging.LogLevel.Information, LogArguments = true, LogResult = true)]
    public int Speak(string phrase) => phrase.Length;
}

/// <summary>Class-level [Instrument] with a [NoInstrument] opt-out.</summary>
public interface IMixed
{
    void Tracked();

    void Untracked();
}

[Instrument]
public sealed class Mixed : IMixed
{
    public void Tracked()
    {
    }

    [NoInstrument]
    public void Untracked()
    {
    }
}

/// <summary>Generic sample to prove generic-class instrumentation.</summary>
public interface IRepository<T>
{
    T Echo(T value);
}

public sealed class Repository<T> : IRepository<T>
{
    [Trace]
    public T Echo(T value) => value;
}

/// <summary>Collaborator used to prove the proxy still delegates (NSubstitute target).</summary>
public interface IInventory
{
    bool Reserve(string sku, int quantity);
}

public interface IOrderService
{
    bool Place(string sku, int quantity);
}

public sealed class OrderService(IInventory inventory) : IOrderService
{
    [Trace]
    [Metric]
    [CaptureException]
    public bool Place(string sku, int quantity) => inventory.Reserve(sku, quantity);
}

/// <summary>Helper to read the span tag value as string.</summary>
public static class ActivityExtensions
{
    public static string? Tag(this Activity activity, string key) =>
        activity.GetTagItem(key) as string;
}
