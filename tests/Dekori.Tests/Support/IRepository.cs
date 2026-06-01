namespace Dekori.Tests.Support;

/// <summary>Generic sample contract.</summary>
public interface IRepository<T>
{
    T Echo(T value);
}
