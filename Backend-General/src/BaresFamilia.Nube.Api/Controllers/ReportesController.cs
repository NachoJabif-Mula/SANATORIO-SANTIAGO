using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador en la Nube para reportería consolidada del Backoffice.
/// Flujo: ReportesController → IReporteVentasService → ReporteVentasService → IReporteVentasRepository → ReporteVentasRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Backoffice")]
public class ReportesController : ControllerBase
{
    private readonly IReporteVentasService _reporteVentasService;

    public ReportesController(IReporteVentasService reporteVentasService)
    {
        _reporteVentasService = reporteVentasService;
    }

    /// <summary>
    /// Número de orden legible a partir del Id de la comanda (mismo formato corto
    /// usado en el resto del sistema, ya que no existe un número de ticket secuencial).
    /// </summary>
    private static string NumeroOrden(Guid comandaId) => $"#{comandaId.ToString()[..8]}";

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
        // Los DateTime? de query string llegan con Kind=Unspecified; Npgsql exige Utc contra timestamptz.
        var resultado = await _reporteVentasService.GetItemsAnuladosAsync(
            ResolveSucursalId(sucursalId), desde.AsUtc(), hasta.AsUtc(), pagina, tamanoPagina, ct);

        var items = resultado.Items.Select(i => new
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

        return Ok(new { total = resultado.Total, pagina = resultado.Pagina, tamanoPagina = resultado.TamanoPagina, items });
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
        var resultado = await _reporteVentasService.GetComandasAnuladasAsync(
            ResolveSucursalId(sucursalId), desde.AsUtc(), hasta.AsUtc(), pagina, tamanoPagina, ct);

        var items = resultado.Items.Select(c =>
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

        return Ok(new { total = resultado.Total, pagina = resultado.Pagina, tamanoPagina = resultado.TamanoPagina, items });
    }
}
