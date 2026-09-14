using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Comanda.
/// Hereda GenericRepository para CRUD estándar.
/// </summary>
public class ComandaRepository : GenericRepository<Comanda>, IComandaRepository
{
    public ComandaRepository(DbContext context) : base(context) { }

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
            .AsNoTracking()
            .Where(c => c.MesaId == mesaId && c.Estado == ComandaEstado.Abierta && c.IsActive)
            .Include(c => c.Items)
                .ThenInclude(i => i.Producto)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<Comanda>> GetAbiertasPorClienteAsync(Guid clienteId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
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
            .AsNoTracking()
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

    public async Task<int> ContarAbiertasDeTurnoAsync(DateTime fechaContable, string turno, CancellationToken ct = default)
        => await FiltrarAbiertasDeTurno(fechaContable, turno).CountAsync(ct);

    public async Task<IEnumerable<Comanda>> GetAbiertasDeTurnoAsync(DateTime fechaContable, string turno, CancellationToken ct = default)
        => await FiltrarAbiertasDeTurno(fechaContable, turno).ToListAsync(ct);

    public async Task<int> ContarAbiertasDeFechaAsync(DateTime fechaContable, CancellationToken ct = default)
        => await _dbSet.CountAsync(
            c => c.FechaContable != null
                && c.FechaContable.Value.Date == fechaContable.Date
                && c.Estado == ComandaEstado.Abierta
                && c.IsActive,
            ct);

    public async Task GuardarCambiosAsync(IEnumerable<Comanda> comandas, CancellationToken ct = default)
    {
        _dbSet.UpdateRange(comandas);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Comandas abiertas de un turno. Las cuentas corrientes abiertas quedan fuera:
    /// tienen FechaContable null porque están exentas del ciclo de turnos.
    /// </summary>
    private IQueryable<Comanda> FiltrarAbiertasDeTurno(DateTime fechaContable, string turno)
        => _dbSet.Where(
            c => c.FechaContable != null
                && c.FechaContable.Value.Date == fechaContable.Date
                && c.Turno == turno
                && c.Estado == ComandaEstado.Abierta
                && c.IsActive);
}
