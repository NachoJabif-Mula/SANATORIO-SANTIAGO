using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador en la Nube para reportería consolidada del Backoffice.
/// La anulación (de ítem o de comanda completa) vive como estado directamente en
/// ComandaItem (Cancelado, MotivoAnulacion, AnuladoPorUsuarioId, FechaAnulacion) —
/// no hay una tabla de auditoría separada, así estos reportes y el futuro reporte
/// de mix de productos comparten la misma fuente de verdad.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly NubeContext _context;

    public ReportesController(NubeContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Número de orden legible a partir del Id de la comanda (mismo formato corto
    /// usado en el resto del sistema, ya que no existe un número de ticket secuencial).
    /// </summary>
    private static string NumeroOrden(Guid comandaId) => $"#{comandaId.ToString()[..8]}";

    private static (int pagina, int tamanoPagina) NormalizarPaginacion(int pagina, int tamanoPagina)
    {
        if (pagina < 1) pagina = 1;
        if (tamanoPagina < 1 || tamanoPagina > 200) tamanoPagina = 50;
        return (pagina, tamanoPagina);
    }

    /// <summary>
    /// Resuelve el sucursalId efectivo a aplicar en las consultas: si el usuario
    /// no tiene alcance global, se ignora cualquier valor recibido y se fuerza
    /// su propia sucursal. Un usuario global sin filtro recibe null (todas).
    /// </summary>
    private Guid? ResolveSucursalId(Guid? requested)
        => User.IsGlobal() ? requested : User.GetSucursalId();

    /// <summary>
    /// Reporte de anulaciones ítem por ítem: cada fila es un producto anulado ya
    /// comandado, con el número de orden a la que pertenece, cantidad, monto,
    /// motivo y quién lo anuló.
    /// </summary>
    [HttpGet("anulaciones/items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnulacionesItems(
        [FromQuery] Guid? sucursalId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 50,
        CancellationToken ct = default)
    {
        (pagina, tamanoPagina) = NormalizarPaginacion(pagina, tamanoPagina);
        var sucId = ResolveSucursalId(sucursalId);

        var query = _context.Set<ComandaItem>()
            .Include(i => i.Producto)
            .Include(i => i.AnuladoPorUsuario)
            .Where(i => i.Cancelado && i.IsActive);

        if (sucId.HasValue) query = query.Where(i => i.AnuladoPorUsuario != null && i.AnuladoPorUsuario.SucursalId == sucId.Value);
        // El rango filtra por la fecha contable del turno de la comanda a la que pertenece el ítem, no por el momento de la anulación.
        // Los DateTime? de query string llegan con Kind=Unspecified; Npgsql exige Utc contra timestamptz.
        if (desde.AsUtc() is DateTime desdeUtc) query = query.Where(i => i.Comanda.FechaContable >= desdeUtc);
        if (hasta.AsUtc() is DateTime hastaUtc) query = query.Where(i => i.Comanda.FechaContable <= hastaUtc);

        var total = await query.CountAsync(ct);

        var registros = await query
            .OrderByDescending(i => i.FechaAnulacion)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct);

        var items = registros.Select(i => new
        {
            id = i.Id,
            numeroOrden = NumeroOrden(i.ComandaId),
            comandaId = i.ComandaId,
            producto = i.Producto?.Nombre ?? "Producto",
            cantidad = i.Cantidad,
            monto = i.Cantidad * i.PrecioUnitario,
            motivo = i.MotivoAnulacion ?? "",
            usuarioNombre = i.AnuladoPorUsuario?.Nombre ?? "—",
            fecha = i.FechaAnulacion
        });

        return Ok(new { total, pagina, tamanoPagina, items });
    }

    /// <summary>
    /// Reporte de transacciones (comandas completas) anuladas en el período de fechas
    /// indicado: cada fila es una orden anulada por completo, con su monto, motivo
    /// y quién la anuló. Motivo/usuario se toman del ítem con la anulación más
    /// reciente de la orden (el que representa la acción final que la cerró).
    /// </summary>
    [HttpGet("anulaciones/comandas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnulacionesComandas(
        [FromQuery] Guid? sucursalId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 50,
        CancellationToken ct = default)
    {
        (pagina, tamanoPagina) = NormalizarPaginacion(pagina, tamanoPagina);
        var sucId = ResolveSucursalId(sucursalId);

        var query = _context.Comandas
            .Include(c => c.Items)
                .ThenInclude(i => i.AnuladoPorUsuario)
            .Where(c => c.Estado == ComandaEstado.Anulada && c.IsActive);

        if (sucId.HasValue) query = query.Where(c => c.Usuario.SucursalId == sucId.Value);
        // El rango filtra por la fecha contable del turno, no por el momento de la anulación.
        // Los DateTime? de query string llegan con Kind=Unspecified; Npgsql exige Utc contra timestamptz.
        if (desde.AsUtc() is DateTime desdeUtc) query = query.Where(c => c.FechaContable >= desdeUtc);
        if (hasta.AsUtc() is DateTime hastaUtc) query = query.Where(c => c.FechaContable <= hastaUtc);

        var total = await query.CountAsync(ct);

        var comandas = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct);

        var items = comandas.Select(c =>
        {
            var ultimaAnulacion = c.Items
                .Where(i => i.Cancelado)
                .OrderByDescending(i => i.FechaAnulacion)
                .FirstOrDefault();

            return new
            {
                id = c.Id,
                numeroOrden = NumeroOrden(c.Id),
                comandaId = c.Id,
                monto = c.Total,
                motivo = ultimaAnulacion?.MotivoAnulacion ?? "",
                usuarioNombre = ultimaAnulacion?.AnuladoPorUsuario?.Nombre ?? "—",
                fecha = c.UpdatedAt
            };
        });

        return Ok(new { total, pagina, tamanoPagina, items });
    }
}
