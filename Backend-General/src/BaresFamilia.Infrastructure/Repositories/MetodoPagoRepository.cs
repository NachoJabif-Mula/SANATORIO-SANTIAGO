using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para MetodoPago.
/// </summary>
public class MetodoPagoRepository : GenericRepository<MetodoPago>, IMetodoPagoRepository
{
    public MetodoPagoRepository(DbContext context) : base(context) { }

    public async Task<IEnumerable<MetodoPago>> GetOrdenadosPorNombreAsync(bool incluirInactivos, CancellationToken ct = default)
    {
        IQueryable<MetodoPago> query = _dbSet.AsNoTracking();

        if (!incluirInactivos)
            query = query.Where(m => m.IsActive);

        return await query.OrderBy(m => m.Nombre).ToListAsync(ct);
    }

    public async Task<bool> ExisteAlgunoAsync(CancellationToken ct = default)
        => await _dbSet.AnyAsync(ct);

    public async Task<bool> ExisteNombreAsync(string nombre, Guid? idExcluido = null, CancellationToken ct = default)
        => await _dbSet.AnyAsync(
            m => m.Nombre == nombre && m.IsActive && (idExcluido == null || m.Id != idExcluido.Value),
            ct);
}
