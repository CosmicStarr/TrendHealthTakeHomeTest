using System;

namespace Data.Interfaces;


public interface IUnitOfWork:IDisposable
{
    /// <summary>Returns (and caches) a staging repository for entity <typeparamref name="T"/>.</summary>
    IRepository<T> Repository<T>() where T : class;

    /// <summary>Saves all staged Add/Update/Remove changes to dbo.</summary>
    Task<int> Complete();
}
