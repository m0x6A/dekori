namespace Dekori.Demo;

/// <summary>Generic sample proving generic-class instrumentation works unchanged.</summary>
public sealed class InMemoryRepository<T> : IRepository<T>
    where T : new()
{
    /// <summary>Emits its span from a dedicated <c>Dekori.Db</c> source rather than the default one.</summary>
    [Trace(Source = "Dekori.Db")]
    [Metric]
    public T GetById(int id) => new();
}
