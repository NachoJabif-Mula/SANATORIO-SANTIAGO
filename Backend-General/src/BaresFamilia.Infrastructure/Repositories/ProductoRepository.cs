using BaresFamilia.Core.Models.Dtos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Producto con queries de dominio.
/// Hereda GenericRepository para CRUD estándar.
/// </summary>
public class ProductoRepository : GenericRepository<Producto>, IProductoRepository
{
    public ProductoRepository(DbContext context) : base(context) { }

    public async Task<IEnumerable<Producto>> GetByCategoriaAsync(Guid categoriaId, CancellationToken ct = default)
        => await _dbSet
            .Where(p => p.CategoriaId == categoriaId && p.IsActive)
            .Include(p => p.Categoria)
            .OrderBy(p => p.Nombre)
            .ToListAsync(ct);

    public async Task<Producto?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .Where(p => p.Id == id && p.IsActive)
            .Include(p => p.Categoria)
            .Include(p => p.ProductoPrecios)
            .FirstOrDefaultAsync(ct);

    public async Task<IEnumerable<Producto>> GetPorSucursalAsync(Guid? sucursalId, bool incluirInactivos, CancellationToken ct = default)
    {
        IQueryable<Producto> query = _dbSet.AsNoTracking();

        if (sucursalId.HasValue)
            query = query.Where(p => p.SucursalId == sucursalId.Value);

        if (!incluirInactivos)
            query = query.Where(p => p.IsActive);

        return await query.OrderBy(p => p.Nombre).ToListAsync(ct);
    }

    public async Task<Producto?> GetPorIdIncluyendoInactivosAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<ProductoDisponible>> GetDisponiblesConPrecioAsync(CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new ProductoDisponible(
                p.Id,
                p.Nombre,
                p.CategoriaId,
                p.ProductoPrecios.Select(pr => pr.PrecioVenta).FirstOrDefault()))
            .ToListAsync(ct);
}
