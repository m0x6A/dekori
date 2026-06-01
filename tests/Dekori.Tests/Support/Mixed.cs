namespace Dekori.Tests.Support;

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
