using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para la consulta de mesas en la nube.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MesaController : ControllerBase
{
    private readonly IService<Mesa> _mesaService;
    private readonly IMonitorSincronizacion _monitorSincronizacion;

    public MesaController(IService<Mesa> mesaService, IMonitorSincronizacion monitorSincronizacion)
    {
        _mesaService = mesaService;
        _monitorSincronizacion = monitorSincronizacion;
    }

    /// <summary>
    /// Obtiene las mesas activas de una sucursal específica.
    /// </summary>
    [HttpGet("sucursal/{sucursalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<Mesa>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySucursal(Guid sucursalId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        _monitorSincronizacion.RegistrarPull(sucursalId, TipoPull.Mesas);
        var mesas = includeInactive
            ? await _mesaService.FindAsync(m => m.SucursalId == sucursalId, ct)
            : await _mesaService.FindAsync(m => m.SucursalId == sucursalId && m.IsActive, ct);
        return Ok(mesas);
    }
}
