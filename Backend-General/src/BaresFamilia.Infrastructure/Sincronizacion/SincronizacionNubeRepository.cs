using BaresFamilia.Core.Models.Contratos.Sincronizacion;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Entities;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Sincronizacion;

/// <summary>
/// Acceso a datos del punto de entrada de sincronización en la Nube.
/// </summary>
public class SincronizacionNubeRepository : ISincronizacionNubeRepository
{
    private readonly DbContext _context;

    public SincronizacionNubeRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> GetSucursalDeUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
        => await _context.Set<Usuario>()
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => (Guid?)u.SucursalId)
            .FirstOrDefaultAsync(ct);

    public async Task<Sucursal?> GetPrimeraSucursalActivaAsync(CancellationToken ct = default)
        => await _context.Set<Sucursal>().AsNoTracking().FirstOrDefaultAsync(s => s.IsActive, ct);

    public async Task<string?> GetNombreSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _context.Set<Sucursal>()
            .AsNoTracking()
            .Where(s => s.Id == sucursalId)
            .Select(s => s.Nombre)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> ExisteSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _context.Set<Sucursal>().AnyAsync(s => s.Id == sucursalId, ct);

    public async Task<HashSet<Guid>> FiltrarIdsExistentesAsync<TEntity>(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        where TEntity : BaseEntity
    {
        if (ids.Count == 0)
            return [];

        var existentes = await _context.Set<TEntity>()
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(ct);

        return existentes.ToHashSet();
    }

    public async Task<Dictionary<Guid, Caja>> GetCajasPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => ids.Count == 0
            ? []
            : await _context.Set<Caja>().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

    public async Task<Dictionary<Guid, TurnoCaja>> GetTurnosCajaPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => ids.Count == 0
            ? []
            : await _context.Set<TurnoCaja>().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);

    public async Task<Dictionary<Guid, Comanda>> GetComandasConItemsPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => ids.Count == 0
            ? []
            : await _context.Set<Comanda>()
                .Include(c => c.Items)
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct);

    public async Task<Dictionary<Guid, CuentaCorriente>> GetCuentasCorrientesPorClienteAsync(
        IReadOnlyCollection<Guid> clienteIds, CancellationToken ct = default)
        => clienteIds.Count == 0
            ? []
            : await _context.Set<CuentaCorriente>()
                .Where(cc => clienteIds.Contains(cc.ClienteId))
                .ToDictionaryAsync(cc => cc.ClienteId, ct);

    public async Task<Caja?> GetCajaActivaDeSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _context.Set<Caja>().FirstOrDefaultAsync(c => c.SucursalId == sucursalId && c.IsActive, ct);

    public async Task<Usuario?> GetPrimerUsuarioActivoDeSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _context.Set<Usuario>().FirstOrDefaultAsync(u => u.SucursalId == sucursalId && u.IsActive, ct);

    public async Task<Rol?> GetPrimerRolActivoAsync(CancellationToken ct = default)
        => await _context.Set<Rol>().FirstOrDefaultAsync(r => r.IsActive, ct);

    public async Task<TipoVenta?> GetPrimerTipoVentaActivoAsync(CancellationToken ct = default)
        => await _context.Set<TipoVenta>().FirstOrDefaultAsync(t => t.IsActive, ct);

    public async Task<MetodoPago?> GetPrimerMetodoPagoActivoAsync(CancellationToken ct = default)
        => await _context.Set<MetodoPago>().FirstOrDefaultAsync(m => m.IsActive, ct);

    public async Task AgregarAsync<TEntity>(IEnumerable<TEntity> entidades, CancellationToken ct = default)
        where TEntity : BaseEntity
        => await _context.Set<TEntity>().AddRangeAsync(entidades, ct);

    public void EliminarItemsDeComanda(IEnumerable<ComandaItem> items)
        => _context.Set<ComandaItem>().RemoveRange(items);

    public async Task GuardarCambiosAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task<ConteosSincronizacion> GetConteosDeSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => new(
            Comandas: await _context.Set<Comanda>().CountAsync(c => c.Usuario.SucursalId == sucursalId, ct),
            Pagos: await _context.Set<Pago>().CountAsync(p => p.Comanda.Usuario.SucursalId == sucursalId, ct),
            Movimientos: await _context.Set<MovimientoCaja>().CountAsync(m => m.TurnoCaja.Usuario.SucursalId == sucursalId, ct),
            CierresDiarios: await _context.Set<CierreDiario>().CountAsync(c => c.Caja.SucursalId == sucursalId, ct),
            Mesas: await _context.Set<Mesa>().CountAsync(m => m.SucursalId == sucursalId, ct),
            Configuraciones: await _context.Set<ConfiguracionPos>().CountAsync(c => c.SucursalId == sucursalId, ct),
            Usuarios: await _context.Set<Usuario>().CountAsync(u => u.SucursalId == sucursalId, ct),
            Roles: await _context.Set<Rol>().CountAsync(ct));

    public async Task<IEnumerable<Sucursal>> GetSucursalesActivasAsync(CancellationToken ct = default)
        => await _context.Set<Sucursal>().AsNoTracking().Where(s => s.IsActive).ToListAsync(ct);
}
