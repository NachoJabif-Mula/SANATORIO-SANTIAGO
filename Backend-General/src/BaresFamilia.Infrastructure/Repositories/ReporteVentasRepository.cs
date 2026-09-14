using BaresFamilia.Core.Models.Contratos;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Consultas de reportería de ventas sobre los datos consolidados en la Nube.
/// </summary>
public class ReporteVentasRepository : IReporteVentasRepository
{
    private readonly DbContext _context;

    public ReporteVentasRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Comanda>> GetComandasConDetallesAsync(CancellationToken ct = default)
        => await _context.Set<Comanda>()
            .AsNoTracking()
            // Mesa es opcional (mostrador y delivery no la tienen); EF resuelve el
            // Include igual, el "!" solo silencia la anotación de nulabilidad.
            .Include(c => c.Mesa)
                .ThenInclude(m => m!.Sucursal)
            .Include(c => c.Usuario)
                .ThenInclude(u => u.Sucursal)
            .Include(c => c.TipoVenta)
            .Include(c => c.Items)
            .Include(c => c.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<ResultadoPaginado<ComandaItem>> GetItemsAnuladosAsync(
        Guid? sucursalId, DateTime? desde, DateTime? hasta, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        var query = _context.Set<ComandaItem>()
            .AsNoTracking()
            .Include(i => i.Producto)
            .Include(i => i.AnuladoPorUsuario)
            .Where(i => i.Cancelado && i.IsActive);

        if (sucursalId.HasValue)
            query = query.Where(i => i.AnuladoPorUsuario != null && i.AnuladoPorUsuario.SucursalId == sucursalId.Value);

        // El rango filtra por la fecha contable del turno de la comanda a la que
        // pertenece el ítem, no por el momento en que se anuló.
        if (desde.HasValue)
            query = query.Where(i => i.Comanda.FechaContable >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(i => i.Comanda.FechaContable <= hasta.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.FechaAnulacion)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct);

        return new ResultadoPaginado<ComandaItem>(total, pagina, tamanoPagina, items);
    }

    public async Task<ResultadoPaginado<Comanda>> GetComandasAnuladasAsync(
        Guid? sucursalId, DateTime? desde, DateTime? hasta, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        var query = _context.Set<Comanda>()
            .AsNoTracking()
            .Include(c => c.Items)
                .ThenInclude(i => i.AnuladoPorUsuario)
            .Where(c => c.Estado == ComandaEstado.Anulada && c.IsActive);

        if (sucursalId.HasValue)
            query = query.Where(c => c.Usuario.SucursalId == sucursalId.Value);

        if (desde.HasValue)
            query = query.Where(c => c.FechaContable >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(c => c.FechaContable <= hasta.Value);

        var total = await query.CountAsync(ct);

        var comandas = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct);

        return new ResultadoPaginado<Comanda>(total, pagina, tamanoPagina, comandas);
    }
}
