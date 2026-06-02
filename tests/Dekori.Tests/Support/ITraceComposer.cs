using System.Diagnostics;

namespace Dekori.Tests.Support;

/// <summary>Sample contract exercising named sources and span/trace association controls.</summary>
public interface ITraceComposer
{
    string MapTo(string place);

    void StartFresh();

    void ContinueFrom(ActivityContext parent);

    void RelateTo(ActivityContext other);

    void RelateToMany(IEnumerable<ActivityContext> others);
}
