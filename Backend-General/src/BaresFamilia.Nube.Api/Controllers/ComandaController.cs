using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador en la Nube para la consulta de comandas / reportes de ventas.
/// Flujo: ComandaController → IReporteVentasService → ReporteVentasService → IReporteVentasRepository → ReporteVentasRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Backoffice")]
public class ComandaController : ControllerBase
{
    private readonly IReporteVentasService _reporteVentasService;

    public ComandaController(IReporteVentasService reporteVentasService)
    {
        _reporteVentasService = reporteVentasService;
    }

    /// <summary>
    /// Obtiene todas las comandas sincronizadas en la Nube con su detalle proyectado.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var comandas = await _reporteVentasService.GetComandasConDetallesAsync(ct);

        var reporte = comandas.Select(c => new
        {
            id = c.Id.ToString(),
            fecha = c.CreatedAt.ToString("o"),
            sucursal = c.Mesa?.Sucursal?.Nombre ?? c.Usuario?.Sucursal?.Nombre ?? "Nube",
            comandaNumero = NumeroComanda(c.Id),
            tipoVenta = c.TipoVenta?.Nombre ?? "Mostrador",
            items = c.Items.Sum(i => i.Cantidad),
            subtotal = c.Subtotal,
            descuento = c.Descuento,
            total = c.Total,
            metodoPago = c.Pagos.FirstOrDefault()?.MetodoPago?.Nombre ?? "Efectivo",
            estado = c.Estado.ToString(),
            syncEstado = "Sincronizado"
        });

        return Ok(reporte);
    }

    /// <summary>
    /// Número de comanda legible y determinista a partir del Guid: el sistema no
    /// lleva un secuencial de ticket, pero la vista necesita un número corto estable.
    /// </summary>
    private static int NumeroComanda(Guid comandaId) => Math.Abs(comandaId.GetHashCode()) % 9000 + 1000;
}
