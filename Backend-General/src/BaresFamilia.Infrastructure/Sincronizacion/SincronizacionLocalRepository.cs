using BaresFamilia.Core.Models.Entities;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Sincronizacion;

/// <summary>
/// Acceso a datos del motor de sincronización de la sucursal.
/// </summary>
public class SincronizacionLocalRepository : ISincronizacionLocalRepository
{
    private readonly DbContext _context;

    public SincronizacionLocalRepository(DbContext context)
    {
        _context = context;
    }

    // ── Activación ──────────────────────────────────────────

    public async Task<DispositivoActivacion?> GetActivacionVigenteAsync(CancellationToken ct = default)
        => await _context.Set<DispositivoActivacion>().FirstOrDefaultAsync(d => d.Activado, ct);

    public async Task EliminarTodasLasActivacionesAsync(CancellationToken ct = default)
    {
        var activaciones = await _context.Set<DispositivoActivacion>().ToListAsync(ct);
        _context.Set<DispositivoActivacion>().RemoveRange(activaciones);
        await _context.SaveChangesAsync(ct);
    }

    // ── Sucursal propia ─────────────────────────────────────

    public async Task<bool> ExisteSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _context.Set<Sucursal>().AnyAsync(s => s.Id == sucursalId, ct);

    public async Task<Sucursal?> GetSucursalSinSeguimientoAsync(Guid sucursalId, CancellationToken ct = default)
        => await _context.Set<Sucursal>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == sucursalId, ct);

    public async Task<Sucursal?> GetSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _context.Set<Sucursal>().FirstOrDefaultAsync(s => s.Id == sucursalId, ct);

    // ── Push: registros pendientes de subir ─────────────────

    public async Task<List<Comanda>> GetComandasPendientesAsync(CancellationToken ct = default)
        => await _context.Set<Comanda>()
            .Where(c => c.IsActive
                && c.SyncEstado == SyncEstado.Pendiente
                && (c.Estado == ComandaEstado.Cobrada
                    || c.Estado == ComandaEstado.Anulada
                    || c.Items.Any(i => i.Cancelado)))
            .Include(c => c.Items)
            .ToListAsync(ct);

    public async Task<List<Pago>> GetPagosPendientesAsync(CancellationToken ct = default)
        => await _context.Set<Pago>()
            .Include(p => p.Comanda)
            .Where(p => p.SyncEstado == SyncEstado.Pendiente && p.Comanda.Estado == ComandaEstado.Cobrada && p.IsActive)
            .ToListAsync(ct);

    public async Task<List<MovimientoCaja>> GetMovimientosCajaPendientesAsync(CancellationToken ct = default)
        => await _context.Set<MovimientoCaja>()
            .Where(m => m.SyncEstado == SyncEstado.Pendiente && m.IsActive)
            .ToListAsync(ct);

    public async Task<List<CierreDiario>> GetCierresDiariosPendientesAsync(CancellationToken ct = default)
        => await _context.Set<CierreDiario>()
            .Where(c => c.SyncEstado == SyncEstado.Pendiente && c.IsActive)
            .ToListAsync(ct);

    public async Task<List<Cliente>> GetClientesPendientesAsync(CancellationToken ct = default)
        => await _context.Set<Cliente>()
            .Where(c => c.SyncEstado == SyncEstado.Pendiente && c.IsActive)
            .ToListAsync(ct);

    public async Task<List<MovimientoCuentaCorriente>> GetMovimientosCuentaCorrientePendientesAsync(CancellationToken ct = default)
        => await _context.Set<MovimientoCuentaCorriente>()
            .Include(m => m.CuentaCorriente)
            .Where(m => m.SyncEstado == SyncEstado.Pendiente && m.IsActive)
            .ToListAsync(ct);

    public async Task<List<TurnoCaja>> GetTurnosCajaPendientesAsync(CancellationToken ct = default)
        => await _context.Set<TurnoCaja>()
            .Where(t => t.SyncEstado == SyncEstado.Pendiente && t.IsActive)
            .ToListAsync(ct);

    // La caja real de la sucursal se sube antes que sus turnos: si no, la Nube tiene
    // que fabricar una caja propia con otro Id y el turno real nunca puede enlazar.
    public async Task<List<Caja>> GetCajasPendientesAsync(CancellationToken ct = default)
        => await _context.Set<Caja>()
            .Where(c => c.SyncEstado == SyncEstado.Pendiente && c.IsActive)
            .ToListAsync(ct);

    // ── Pull: volcado del catálogo que baja de la Nube ───────

    public async Task<List<TEntity>> GetTodosAsync<TEntity>(CancellationToken ct = default) where TEntity : BaseEntity
        => await _context.Set<TEntity>().ToListAsync(ct);

    public async Task<bool> ExisteAsync<TEntity>(Guid id, CancellationToken ct = default) where TEntity : BaseEntity
        => await _context.Set<TEntity>().AnyAsync(e => e.Id == id, ct);

    public async Task<bool> HayAlgunoAsync<TEntity>(CancellationToken ct = default) where TEntity : BaseEntity
        => await _context.Set<TEntity>().AnyAsync(ct);

    public async Task<bool> ExisteCuentaCorrienteDeClienteAsync(Guid clienteId, CancellationToken ct = default)
        => await _context.Set<CuentaCorriente>().AnyAsync(cc => cc.ClienteId == clienteId, ct);

    public void Agregar<TEntity>(TEntity entidad) where TEntity : BaseEntity
        => _context.Set<TEntity>().Add(entidad);

    public async Task GuardarCambiosAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
