using System.Linq.Expressions;
using BaresFamilia.Core.Models.Entities;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Implementación genérica del servicio de negocio.
/// Delega operaciones de persistencia al repositorio inyectado.
/// Flujo: Controller -> IService -> Service -> IRepository -> Repository
/// </summary>
public class GenericService<T> : IService<T> where T : BaseEntity
{
    protected readonly IRepository<T> _repository;

    public GenericService(IRepository<T> repository)
    {
        _repository = repository;
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _repository.GetByIdAsync(id, ct);

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => await _repository.GetAllAsync(ct);

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _repository.FindAsync(predicate, ct);

    public virtual async Task<T> CreateAsync(T entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        return await _repository.AddAsync(entity, ct);
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
        => await _repository.DeleteAsync(id, ct);

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await _repository.ExistsAsync(id, ct);
}
