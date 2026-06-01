namespace Dekori.Demo;

public interface IRepository<T>
{
    T GetById(int id);
}
