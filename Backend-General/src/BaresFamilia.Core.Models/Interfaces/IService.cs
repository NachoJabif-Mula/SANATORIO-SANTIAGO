using System.Linq.Expressions;
using BaresFamilia.Core.Models.Entities;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio genérico base. Define las operaciones de negocio estándar.
/// El flujo es: Controlador -> IService -> Service -> IRepository -> Repository.
/// </summary>
public interface IService<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T> CreateAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
}
