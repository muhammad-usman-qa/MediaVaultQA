using MediaVault.API.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.API.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly MediaVaultDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(MediaVaultDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id) => await _dbSet.FindAsync(id);

    public async Task<IReadOnlyList<T>> GetAllAsync() => await _dbSet.ToListAsync();

    public async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id) => await _dbSet.FindAsync(id) is not null;
}
