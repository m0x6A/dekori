namespace Dekori.Demo;

/// <summary>Generic sample proving generic-class instrumentation works unchanged.</summary>
public sealed class InMemoryRepository<T> : IRepository<T>
    where T : new()
{
    [Trace]
    [Metric]
    public T GetById(int id) => new();
}
