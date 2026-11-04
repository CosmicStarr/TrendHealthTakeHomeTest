using System;
using System.Collections;
using Data.Interfaces;

namespace Data.Classes;

public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    private Hashtable? _repo;
    private readonly ApplicationDbContext _context = context;


    public async Task<int> Complete()
    {
        return await _context.SaveChangesAsync();
    }

    /// <summary>Disposes the staging DbContext.</summary>
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
    {
        _context.Dispose();
    }

 
    public IRepository<T> Repository<T>() where T : class
    {
        _repo ??= [];
        var type = typeof(T).Name;
        if (!_repo.ContainsKey(type))
        {
            var repoType = typeof(Repository<>);
            var repoInstance = Activator.CreateInstance(repoType.MakeGenericType(typeof(T)),_context);
            _repo.Add(type,repoInstance);
        }
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
        return (IRepository<T>)_repo[type];
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
    }
}
