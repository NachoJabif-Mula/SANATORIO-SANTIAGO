using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para ProductoPrecio.
/// </summary>
public class ProductoPrecioRepository : GenericRepository<ProductoPrecio>, IProductoPrecioRepository
{
    public ProductoPrecioRepository(DbContext context) : base(context) { }

    public async Task<IEnumerable<ProductoPrecio>> GetActivosPorProductoAsync(Guid productoId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(p => p.ProductoId == productoId && p.IsActive)
            .ToListAsync(ct);

    // Sin AsNoTracking: el servicio modifica las entidades devueltas y las
    // reenvía a GuardarLoteAsync para que se persistan como UPDATE.
    public async Task<IEnumerable<ProductoPrecio>> GetTodosPorProductoYSucursalAsync(Guid productoId, Guid sucursalId, CancellationToken ct = default)
        => await _dbSet
            .Where(p => p.ProductoId == productoId && p.SucursalId == sucursalId)
            .ToListAsync(ct);

    public async Task<IEnumerable<ProductoPrecio>> GetTodosPorSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(p => p.SucursalId == sucursalId)
            .ToListAsync(ct);

    public async Task GuardarLoteAsync(IEnumerable<ProductoPrecio> nuevos, IEnumerable<ProductoPrecio> modificados, CancellationToken ct = default)
    {
        using var transaccion = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            await _dbSet.AddRangeAsync(nuevos, ct);
            _dbSet.UpdateRange(modificados);

            await _context.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (Exception)
        {
            await transaccion.RollbackAsync(ct);
            throw;
        }
    }
}
