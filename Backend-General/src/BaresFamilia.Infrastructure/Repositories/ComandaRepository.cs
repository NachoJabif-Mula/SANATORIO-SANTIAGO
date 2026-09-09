using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Comanda contra LocalContext.
/// Hereda GenericRepository para CRUD estándar.
/// </summary>
public class ComandaRepository : GenericRepository<Comanda>, IComandaRepository
{
    public ComandaRepository(LocalContext context) : base(context) { }

    public async Task<Comanda?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .Where(c => c.Id == id && c.IsActive)
            .Include(c => c.Items)
                .ThenInclude(i => i.Producto)
            .Include(c => c.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .Include(c => c.TipoVenta)
            .Include(c => c.Mesa)
            .Include(c => c.Cliente)
            .Include(c => c.Usuario)
            .FirstOrDefaultAsync(ct);

    public async Task<IEnumerable<Comanda>> GetAbierdasPorMesaAsync(Guid mesaId, CancellationToken ct = default)
        => await _dbSet
            .Where(c => c.MesaId == mesaId && c.Estado == ComandaEstado.Abierta && c.IsActive)
            .Include(c => c.Items)
                .ThenInclude(i => i.Producto)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<Comanda>> GetAbiertasPorClienteAsync(Guid clienteId, CancellationToken ct = default)
        => await _dbSet
            .Where(c => c.ClienteId == clienteId && c.Estado == ComandaEstado.Abierta && c.IsActive)
            .Include(c => c.Items)
                .ThenInclude(i => i.Producto)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<Comanda>> GetPendientesSyncAsync(CancellationToken ct = default)
        => await _dbSet
            .Where(c => c.SyncEstado == SyncEstado.Pendiente && c.Estado == ComandaEstado.Cobrada && c.IsActive)
            .Include(c => c.Items)
            .Include(c => c.Pagos)
            .ToListAsync(ct);

    public async Task<IEnumerable<Comanda>> GetAllWithDetailsAsync(CancellationToken ct = default)
        => await _dbSet
            .Where(c => c.IsActive)
            .Include(c => c.Items)
                .ThenInclude(i => i.Producto)
            .Include(c => c.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .Include(c => c.TipoVenta)
            .Include(c => c.Mesa)
            .Include(c => c.Cliente)
            .Include(c => c.Usuario)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task UpdateComandaWithItemsAsync(Comanda existing, List<ComandaItem> newItems, CancellationToken ct)
    {
        // 1. Remover ítems viejos físicos de la base de datos.
        // Los ítems ya anulados se preservan intactos: conservan su propia auditoría
        // (Cancelado/MotivoAnulacion/AnuladoPorUsuarioId/FechaAnulacion) y el frontend ya los
        // excluye de newItems, así que recrearlos o borrarlos perdería esa información.
        var oldItems = _context.Set<ComandaItem>().Where(i => i.ComandaId == existing.Id && !i.Cancelado);
        _context.Set<ComandaItem>().RemoveRange(oldItems);

        // 2. Asignar nuevos ítems
        foreach (var item in newItems)
        {
            item.ComandaId = existing.Id;
            await _context.Set<ComandaItem>().AddAsync(item, ct);
        }

        // 3. Actualizar cabecera
        existing.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(existing);

        // 4. Guardar
        await _context.SaveChangesAsync(ct);
    }
}
