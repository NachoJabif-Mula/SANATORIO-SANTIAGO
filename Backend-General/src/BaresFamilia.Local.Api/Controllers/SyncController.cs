using System.Threading;
using System.Threading.Tasks;
using BaresFamilia.Local.Api.Workers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly SincronizacionWorker _worker;

    public SyncController(SincronizacionWorker worker)
    {
        _worker = worker;
    }

    /// <summary>
    /// Fuerza la ejecución manual de una sincronización completa (PUSH + PULL).
    /// </summary>
    [HttpPost("run")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RunSync(CancellationToken ct)
    {
        var started = await _worker.TriggerManualSyncAsync(ct);
        if (!started)
        {
            return Conflict(new { message = "Ya hay un proceso de sincronización ejecutándose en este momento." });
        }

        return Ok(new { message = "Sincronización manual iniciada con éxito." });
    }

    /// <summary>
    /// Obtiene los logs locales de las sincronizaciones recientes.
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetLogs()
    {
        var logs = LocalSyncLogStore.GetLogs();
        return Ok(logs);
    }
}
