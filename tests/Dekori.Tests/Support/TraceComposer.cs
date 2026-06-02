using System.Diagnostics;

namespace Dekori.Tests.Support;

public sealed class TraceComposer : ITraceComposer
{
    /// <summary>A dedicated, non-default activity source name used by <see cref="MapTo"/>.</summary>
    public const string NamedSource = "Dekori.Test.NamedSource";

    [Trace(Source = NamedSource)]
    public string MapTo(string place) => $"Mapped {place}";

    [Trace(NewRoot = true)]
    public void StartFresh()
    {
    }

    [Trace]
    public void ContinueFrom([TraceParent] ActivityContext parent)
    {
    }

    [Trace]
    public void RelateTo([TraceLink] ActivityContext other)
    {
    }

    [Trace]
    public void RelateToMany([TraceLink] IEnumerable<ActivityContext> others)
    {
    }
}
