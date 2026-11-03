using System.Linq.Expressions;
using Data.Util;

namespace Data.Interfaces;

/// <summary>
/// Generic data access for the CJ staging context (<c>dbo.FlatProducts</c> / <c>FlatCategories</c>).
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>Finds one row by integer primary key, or null if it does not exist.</summary>
    Task<T> Get(int Id);

    /// <summary>
    /// Paged list with optional filter, sort, and Include paths (comma-separated navigation names).
    /// </summary>
    Task<PagerList<T>> GetAllParams(PageParams? pageParams = null, Expression<Func<T,bool>>? filter = null, Func<IQueryable<T>,IOrderedQueryable<T>>? orderby = null, string? includeProperties = null);

    /// <summary>
    /// Unpaged list with optional filter, sort, and Include paths.
    /// </summary>
    Task<IEnumerable<T>> GetAll(Expression<Func<T,bool>>? filter = null,
    Func<IQueryable<T>,IOrderedQueryable<T>>? orderby = null,
    string? includeProperties = null);

    /// <summary>
    /// First matching row (or null), with optional Include paths.
    /// </summary>
    Task<T> GetFirstOrDefault(Expression<Func<T,bool>>? filter = null,
    string? includeProperties = null);

    /// <summary>Stages a new row; call <c>IUnitOfWork.Complete</c> to save.</summary>
    void Add(T entity);

    /// <summary>Stages deletion of a tracked or attached entity.</summary>
    void Remove(T entity);

    /// <summary>Finds a row by string primary key and stages its deletion if found.</summary>
    void Remove(string Id);

    /// <summary>Stages deletion of many rows.</summary>
    void RemoveRange(IEnumerable<T> entities);

    /// <summary>Marks an existing row as modified.</summary>
    void Update(T entity);
}
