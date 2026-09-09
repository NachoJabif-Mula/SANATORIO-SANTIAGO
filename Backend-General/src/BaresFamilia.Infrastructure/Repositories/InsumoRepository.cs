using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Insumo con queries de inventario.
/// Hereda GenericRepository para CRUD estándar.
/// </summary>
public class InsumoRepository : GenericRepository<Insumo>, IInsumoRepository
{
    public InsumoRepository(NubeContext context) : base(context) { }

    /// <summary>
    /// Obtiene los insumos cuyo stock actual en la sucursal indicada
    /// está por debajo del StockMinimo configurado.
    /// </summary>
    public async Task<IEnumerable<Insumo>> GetConStockBajoAsync(Guid sucursalId, CancellationToken ct = default)
    {
        return await _context.Set<StockSucursal>()
            .Where(ss => ss.SucursalId == sucursalId && ss.IsActive)
            .Include(ss => ss.Insumo)
            .Where(ss => ss.CantidadActual < ss.Insumo.StockMinimo)
            .Select(ss => ss.Insumo)
            .Where(i => i.IsActive)
            .ToListAsync(ct);
    }
}
