using System;
using System.Linq.Expressions;
using Data.Interfaces;
using Data.Util;
using Microsoft.EntityFrameworkCore;

namespace Data.Classes;


public class Repository<T>(ApplicationDbContext context) : IRepository<T> where T : class
{
    private readonly ApplicationDbContext _context = context;

    internal DbSet<T> dbSet = context.Set<T>();

    /// <summary>Stages a new staging-table row.</summary>
    public void Add(T entity)
    {
        dbSet.Add(entity);
    }
    
    /// <summary>Finds one row by integer primary key.</summary>
    public async Task<T> Get(int Id)
    {
#pragma warning disable CS8603 // Possible null reference return.
        return await dbSet.FindAsync(Id);
#pragma warning restore CS8603 // Possible null reference return.
    }
    
    /// <summary>Unpaged staging-table query with optional filter, sort, and Includes.</summary>
    public async Task<IEnumerable<T>> GetAll(Expression<Func<T, bool>>? filter = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderby = null, string? includeProperties = null)
    {
        IQueryable<T> query = dbSet.AsNoTracking();
        if(filter != null)
        {
            query = query.Where(filter);
        }
        if(includeProperties != null)
        {
            foreach(var ItemData in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(ItemData);
            }
        }
        if(orderby != null)
        {
            return await orderby(query).ToListAsync();
        }
        return await query.ToListAsync();
    }

    /// <summary>Paged staging-table query for the admin catalog grid.</summary>
    public async Task<PagerList<T>> GetAllParams(PageParams? pageParams = null, Expression<Func<T, bool>>? filter = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderby = null, string? includeProperties = null)
    {
        pageParams ??= new PageParams();
        IQueryable<T> query = dbSet.AsNoTracking();
        if(filter != null)
        {
            query = query.Where(filter);
        }
        if(includeProperties != null)
        {
            foreach(var ItemData in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(ItemData);
            }
        }
        if(orderby != null)
        {
            return await PagerList<T>.CreateAsync(orderby(query),pageParams.PageNumber,pageParams.PageSize);
        }
        return await PagerList<T>.CreateAsync(query,pageParams.PageNumber,pageParams.PageSize);
    }

    /// <summary>First matching staging row, or null.</summary>
    public async Task<T> GetFirstOrDefault(Expression<Func<T, bool>>? filter = null, string? includeProperties = null)
    {
        IQueryable<T> query = dbSet.AsNoTracking();
        if(filter != null)
        {
            query = query.Where(filter);
        }
        if(includeProperties != null)
        {
            foreach(var ItemData in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(ItemData);
            }
        }
#pragma warning disable CS8603 // Possible null reference return.
        return await query.FirstOrDefaultAsync();
#pragma warning restore CS8603 // Possible null reference return.
    }

    /// <summary>Stages deletion of one entity instance.</summary>
    public void Remove(T entity)
    {
        dbSet.Remove(entity);
    }

    /// <summary>Looks up a string-key row and stages its deletion if it exists.</summary>
    public void Remove(string Id)
    {
        var info = dbSet.Find(Id);
        if(info != null)
        {
            dbSet.Remove(info);
        }
    }

    /// <summary>Stages deletion of many rows.</summary>
    public void RemoveRange(IEnumerable<T> entities)
    {
        dbSet.RemoveRange(entities);
    }

    /// <summary>Marks a staging row as modified.</summary>
    public void Update(T entity)
    {
        dbSet.Update(entity);
    }
}
