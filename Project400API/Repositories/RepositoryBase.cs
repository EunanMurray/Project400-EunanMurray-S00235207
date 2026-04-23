using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400API.Repositories.Interfaces;

namespace Project400API.Repositories;

public abstract class RepositoryBase<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    protected RepositoryBase(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id) => await DbSet.FindAsync(id);
    public virtual async Task<List<T>> GetAllAsync() => await DbSet.ToListAsync();
    public virtual async Task AddAsync(T entity) => await DbSet.AddAsync(entity);
    public virtual void Update(T entity) => DbSet.Update(entity);
    public virtual void Remove(T entity) => DbSet.Remove(entity);
    public virtual async Task SaveChangesAsync() => await Context.SaveChangesAsync();
}
