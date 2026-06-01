namespace Dekori.Tests.Support;

public sealed class Repository<T> : IRepository<T>
{
    [Trace]
    public T Echo(T value) => value;
}
