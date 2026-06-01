using Microsoft.Extensions.Logging;

namespace Dekori.Tests.Support;

public sealed class Chatter : IChatter
{
    [LogCall(Level = LogLevel.Information, LogArguments = true, LogResult = true)]
    public int Speak(string phrase) => phrase.Length;
}
