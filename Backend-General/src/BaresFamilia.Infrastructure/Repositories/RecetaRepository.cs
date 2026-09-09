using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Receta con queries de dominio.
/// Hereda GenericRepository para CRUD estándar.
/// </summary>
public class RecetaRepository : GenericRepository<Receta>, IRecetaRepository
{
    public RecetaRepository(NubeContext context) : base(context) { }

    public async Task<IEnumerable<Receta>> GetByProductoAsync(Guid productoId, CancellationToken ct = default)
        => await _dbSet
            .Where(r => r.ProductoId == productoId && r.IsActive)
            .Include(r => r.Insumo)
            .Include(r => r.Producto)
            .OrderBy(r => r.Insumo.Nombre)
            .ToListAsync(ct);
}
