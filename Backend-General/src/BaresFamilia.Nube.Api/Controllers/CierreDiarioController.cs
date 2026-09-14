using BaresFamilia.Core.Models.Dtos.Cajas;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para la visualización de cierres diarios consolidados desde el backoffice.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Backoffice")]
public class CierreDiarioController : ControllerBase
{
    private readonly ICierreDiarioService _cierreDiarioService;

    public CierreDiarioController(ICierreDiarioService cierreDiarioService)
    {
        _cierreDiarioService = cierreDiarioService;
    }

    /// <summary>
    /// Resuelve el sucursalId efectivo a aplicar en las consultas: si el usuario
    /// no tiene alcance global, se ignora cualquier valor recibido y se fuerza
    /// su propia sucursal. Un usuario global sin filtro recibe null (todas).
    /// </summary>
    private Guid? ResolveSucursalId(Guid? requested)
        => User.IsGlobal() ? requested : User.GetSucursalId();

    /// <summary>
    /// Obtiene todos los cierres diarios consolidados.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CierreDiarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var sucId = ResolveSucursalId(sucursalId);

        // Los DateTime? de query string llegan con Kind=Unspecified; Npgsql exige Utc contra timestamptz.
        var cierres = await _cierreDiarioService.GetConDetallesAsync(sucId, desde.AsUtc(), hasta.AsUtc(), ct);

        return Ok(cierres.Select(MapearResumen));
    }

    /// <summary>
    /// Obtiene el detalle de un cierre diario específico.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CierreDiarioDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var cierre = await _cierreDiarioService.GetPorIdConDetallesAsync(id, ct);
        if (cierre is null)
            return NotFound(new { message = "Cierre diario no encontrado." });

        return Ok(new CierreDiarioDetalleDto
        {
            Id = cierre.Id,
            CajaId = cierre.CajaId,
            CajaNombre = cierre.Caja.Nombre,
            Fecha = cierre.Fecha,
            UsuarioCierreId = cierre.UsuarioCierreId,
            UsuarioCierreNombre = cierre.UsuarioCierre.Nombre,
            TotalVentas = cierre.TotalVentas,
            TotalEgresos = cierre.TotalEgresos,
            TotalNeto = cierre.TotalNeto,
            ResumenJson = cierre.ResumenJson,
            Observaciones = cierre.Observaciones,
            CreatedAt = cierre.CreatedAt
        });
    }

    private static CierreDiarioDto MapearResumen(CierreDiario cierre)
        => new()
        {
            Id = cierre.Id,
            CajaId = cierre.CajaId,
            CajaNombre = cierre.Caja.Nombre,
            Fecha = cierre.Fecha,
            UsuarioCierreId = cierre.UsuarioCierreId,
            UsuarioCierreNombre = cierre.UsuarioCierre.Nombre,
            TotalVentas = cierre.TotalVentas,
            TotalEgresos = cierre.TotalEgresos,
            TotalNeto = cierre.TotalNeto,
            Observaciones = cierre.Observaciones,
            CreatedAt = cierre.CreatedAt
        };
}
