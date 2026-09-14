using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Endpoints de control manual del motor de sincronización de la sucursal.
/// Flujo: SyncController → IMotorSincronizacionLocal → SincronizacionWorker.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly IMotorSincronizacionLocal _motorSincronizacion;

    public SyncController(IMotorSincronizacionLocal motorSincronizacion)
    {
        _motorSincronizacion = motorSincronizacion;
    }

    /// <summary>
    /// Fuerza la ejecución manual de una sincronización completa (PUSH + PULL).
    /// </summary>
    [HttpPost("run")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RunSync(CancellationToken ct)
    {
        if (!await _motorSincronizacion.DispararSincronizacionManualAsync(ct))
            return Conflict(new { message = "Ya hay un proceso de sincronización ejecutándose en este momento." });

        return Ok(new { message = "Sincronización manual iniciada con éxito." });
    }

    /// <summary>
    /// Obtiene los logs locales de las sincronizaciones recientes.
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetLogs() => Ok(_motorSincronizacion.GetRegistrosRecientes());
}
