using BaresFamilia.Core.Models.Contratos.Dashboard;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Consultas de agregación del dashboard del Backoffice.
/// </summary>
public class DashboardRepository : IDashboardRepository
{
    private readonly DbContext _context;

    public DashboardRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetTotalVentasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default)
        => await ComandasDelRango(sucursalId, rango)
            .Where(c => c.Estado == ComandaEstado.Cobrada)
            .SumAsync(c => (decimal?)c.Total, ct) ?? 0m;

    public async Task<int> ContarComandasNoAnuladasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default)
        => await ComandasDelRango(sucursalId, rango).CountAsync(c => c.Estado != ComandaEstado.Anulada, ct);

    public async Task<int> ContarComandasAnuladasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default)
        => await ComandasDelRango(sucursalId, rango).CountAsync(c => c.Estado == ComandaEstado.Anulada, ct);

    public async Task<int> ContarComandasCobradasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default)
        => await ComandasDelRango(sucursalId, rango).CountAsync(c => c.Estado == ComandaEstado.Cobrada, ct);

    public async Task<IEnumerable<VentasDeTurno>> GetVentasPorTurnoAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default)
        => await ComandasDelRango(sucursalId, rango)
            .Where(c => c.Estado == ComandaEstado.Cobrada)
            .GroupBy(c => c.Turno)
            .Select(g => new VentasDeTurno(g.Key, g.Sum(c => c.Total), g.Count()))
            .ToListAsync(ct);

    public async Task<IEnumerable<int>> GetHorasDeComandasCobradasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default)
        => await ComandasDelRango(sucursalId, rango)
            .Where(c => c.Estado == ComandaEstado.Cobrada)
            .Select(c => c.CreatedAt.Hour)
            .ToListAsync(ct);

    public async Task<IEnumerable<AnulacionAuditada>> GetUltimasAnulacionesAsync(
        Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default)
    {
        // El estado de anulación vive directamente en ComandaItem, no en una tabla
        // aparte; el rango filtra por la fecha contable de la comanda del ítem.
        var query = _context.Set<ComandaItem>()
            .AsNoTracking()
            .Where(i => i.Cancelado
                && i.IsActive
                && i.Comanda.FechaContable >= rango.Desde
                && i.Comanda.FechaContable <= rango.Hasta);

        if (sucursalId.HasValue)
            query = query.Where(i => i.AnuladoPorUsuario != null && i.AnuladoPorUsuario.SucursalId == sucursalId.Value);

        return await query
            .OrderByDescending(i => i.FechaAnulacion)
            .Take(cantidad)
            .Select(i => new AnulacionAuditada(
                i.ComandaId,
                i.MotivoAnulacion,
                i.AnuladoPorUsuario != null ? i.AnuladoPorUsuario.Nombre : null,
                i.FechaAnulacion ?? i.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<DescuentoAuditado>> GetUltimosDescuentosAsync(
        Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default)
        => await ComandasDelRango(sucursalId, rango)
            .Where(c => c.Descuento > 0)
            .OrderByDescending(c => c.CreatedAt)
            .Take(cantidad)
            .Select(c => new DescuentoAuditado(c.Id, c.Descuento, c.Subtotal, c.CreatedAt))
            .ToListAsync(ct);

    public async Task<IEnumerable<TotalPorMedioPago>> GetTotalesPorMedioPagoAsync(
        Guid? sucursalId, RangoFechas rango, CancellationToken ct = default)
    {
        var query = _context.Set<Pago>()
            .AsNoTracking()
            .Where(p => p.Comanda.Estado == ComandaEstado.Cobrada
                && p.Comanda.FechaContable >= rango.Desde
                && p.Comanda.FechaContable <= rango.Hasta);

        if (sucursalId.HasValue)
            query = query.Where(p => p.Comanda.Usuario.SucursalId == sucursalId.Value);

        return await query
            .GroupBy(p => new { p.MetodoPagoId, p.MetodoPago.Nombre })
            .Select(g => new TotalPorMedioPago(g.Key.Nombre, g.Sum(p => p.Monto), g.Count()))
            .OrderByDescending(t => t.Monto)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ProductoVendido>> GetTopProductosAsync(
        Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default)
    {
        var query = _context.Set<ComandaItem>()
            .AsNoTracking()
            .Where(i => !i.Cancelado
                && i.IsActive
                && i.Comanda.Estado == ComandaEstado.Cobrada
                && i.Comanda.FechaContable >= rango.Desde
                && i.Comanda.FechaContable <= rango.Hasta);

        if (sucursalId.HasValue)
            query = query.Where(i => i.Comanda.Usuario.SucursalId == sucursalId.Value);

        return await query
            .GroupBy(i => new { i.ProductoId, i.Producto.Nombre })
            .Select(g => new ProductoVendido(
                g.Key.Nombre,
                g.Sum(i => i.Cantidad),
                g.Sum(i => i.Cantidad * i.PrecioUnitario)))
            .OrderByDescending(p => p.Cantidad)
            .Take(cantidad)
            .ToListAsync(ct);
    }

    // Sin filtro de IsActive: el dashboard debe poder nombrar una sucursal dada de
    // baja que todavía tenga ventas históricas dentro del rango consultado.
    public async Task<string?> GetNombreSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _context.Set<Sucursal>()
            .AsNoTracking()
            .Where(s => s.Id == sucursalId)
            .Select(s => s.Nombre)
            .FirstOrDefaultAsync(ct);

    public async Task<AlcanceSucursales> ContarSucursalesAsync(CancellationToken ct = default)
    {
        var sucursales = _context.Set<Sucursal>().AsNoTracking();

        return new AlcanceSucursales(
            SucursalNombre: null,
            Activas: await sucursales.CountAsync(s => s.IsActive, ct),
            Totales: await sucursales.CountAsync(ct));
    }

    /// <summary>
    /// Comandas del rango de fechas contables, acotadas a una sucursal si se indicó.
    /// </summary>
    private IQueryable<Comanda> ComandasDelRango(Guid? sucursalId, RangoFechas rango)
    {
        var query = _context.Set<Comanda>()
            .AsNoTracking()
            .Where(c => c.FechaContable >= rango.Desde && c.FechaContable <= rango.Hasta);

        return sucursalId.HasValue
            ? query.Where(c => c.Usuario.SucursalId == sucursalId.Value)
            : query;
    }
}
