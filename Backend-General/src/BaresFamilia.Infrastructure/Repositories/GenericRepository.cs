using System.Linq.Expressions;
using BaresFamilia.Core.Models.Entities;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio genérico que implementa las operaciones CRUD contra EF Core.
/// Utiliza borrado lógico (IsActive = false) en lugar de DELETE físico.
/// </summary>
public class GenericRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly DbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(e => e.Id == id && e.IsActive, ct);

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.Where(e => e.IsActive).ToListAsync(ct);

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _dbSet.Where(predicate).Where(e => e.IsActive).ToListAsync(ct);

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        await _dbSet.AddRangeAsync(entities, ct);
        await _context.SaveChangesAsync(ct);
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Borrado lógico: marca IsActive = false en vez de eliminar el registro.
    /// </summary>
    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbSet.FindAsync([id], ct);
        if (entity is not null)
        {
            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.AnyAsync(e => e.Id == id && e.IsActive, ct);

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null
            ? await _dbSet.CountAsync(e => e.IsActive, ct)
            : await _dbSet.Where(predicate).CountAsync(e => e.IsActive, ct);
}
