using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para TipoVenta.
/// </summary>
public class TipoVentaRepository : GenericRepository<TipoVenta>, ITipoVentaRepository
{
    public TipoVentaRepository(DbContext context) : base(context) { }

    public async Task<IEnumerable<TipoVenta>> GetOrdenadosPorNombreAsync(bool incluirInactivos, CancellationToken ct = default)
    {
        IQueryable<TipoVenta> query = _dbSet.AsNoTracking();

        if (!incluirInactivos)
            query = query.Where(t => t.IsActive);

        return await query.OrderBy(t => t.Nombre).ToListAsync(ct);
    }

    public async Task<bool> ExisteAlgunoAsync(CancellationToken ct = default)
        => await _dbSet.AnyAsync(ct);
}
