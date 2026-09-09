using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Producto con queries de dominio.
/// Hereda GenericRepository para CRUD estándar.
/// </summary>
public class ProductoRepository : GenericRepository<Producto>, IProductoRepository
{
    public ProductoRepository(NubeContext context) : base(context) { }

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
}
