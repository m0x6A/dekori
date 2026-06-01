namespace Dekori.Tests.Support;

/// <summary>Class-level [Instrument] with a [NoInstrument] opt-out sample contract.</summary>
public interface IMixed
{
    void Tracked();

    void Untracked();
}
