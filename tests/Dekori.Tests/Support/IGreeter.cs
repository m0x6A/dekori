namespace Dekori.Tests.Support;

/// <summary>Tracing + async sample contract.</summary>
public interface IGreeter
{
    string Greet(string name);

    Task<string> GreetAsync(string name, int delayMs);
}
