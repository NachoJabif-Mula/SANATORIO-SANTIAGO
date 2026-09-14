using BaresFamilia.Core.Models.Contratos.Sincronizacion;
using BaresFamilia.Core.Models.Dtos.Sincronizacion;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para procesar la sincronización bidireccional desde las terminales locales.
/// Flujo: SyncController → ISincronizacionNubeService → SincronizacionNubeService → ISincronizacionNubeRepository → SincronizacionNubeRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : ControllerBase
{
    private const int RegistrosRecientesMostrados = 50;

    private readonly ISincronizacionNubeService _sincronizacionService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ISincronizacionNubeService sincronizacionService, ILogger<SyncController> logger)
    {
        _sincronizacionService = sincronizacionService;
        _logger = logger;
    }

    /// <summary>
    /// Recibe comandas, pagos y movimientos desde el POS local y los persiste en la Nube.
    /// </summary>
    [HttpPost("recibir")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Recibir([FromBody] SyncPayloadDto payload, CancellationToken ct)
    {
        if (payload is null)
            return BadRequest(new { message = "El payload no puede ser nulo." });

        try
        {
            await _sincronizacionService.RecibirPayloadAsync(payload, User.GetSucursalId(), ct);
            return Ok(new { message = "Sincronización procesada correctamente en la nube." });
        }
        catch (Exception ex)
        {
            // La sucursal reintenta el lote en el próximo ciclo: se responde el error
            // sin tumbar la request para que el POS lo registre y siga operando.
            _logger.LogError(ex, "Error al procesar el payload de sincronización.");
            _sincronizacionService.RegistrarErrorDeIngesta($"Error al procesar payload de sync: {ex.Message}");

            return BadRequest(new { message = $"Error en el servidor de Nube: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene las estadísticas de sincronización para todas las sucursales.
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Policy = "Backoffice")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var estadisticas = await _sincronizacionService.GetEstadisticasAsync(ct);

        return Ok(estadisticas.Select(e => new
        {
            sucursalId = e.SucursalId,
            sucursalNombre = e.SucursalNombre,
            comandasCount = e.Conteos.Comandas,
            pagosCount = e.Conteos.Pagos,
            movimientosCount = e.Conteos.Movimientos,
            cierresDiariosCount = e.Conteos.CierresDiarios,
            mesasCount = e.Conteos.Mesas,
            configuracionesCount = e.Conteos.Configuraciones,
            usuariosCount = e.Conteos.Usuarios,
            rolesCount = e.Conteos.Roles,
            connectedPosCount = e.DispositivosConectados,
            lastPushComandas = e.UltimaActividad.LastPushComandas,
            lastPushPagos = e.UltimaActividad.LastPushPagos,
            lastPushMovimientos = e.UltimaActividad.LastPushMovimientos,
            lastPushCierresDiarios = e.UltimaActividad.LastPushCierresDiarios,
            lastPullConfig = e.UltimaActividad.LastPullConfig,
            lastPullMesas = e.UltimaActividad.LastPullMesas,
            lastPullRoles = e.UltimaActividad.LastPullRoles,
            lastPullUsuarios = e.UltimaActividad.LastPullUsuarios,
            forceSyncPending = e.SincronizacionForzadaPendiente,
            syncIntervalSeconds = e.IntervaloSegundos
        }));
    }

    /// <summary>
    /// Fuerza una sincronización inmediata para la sucursal indicada.
    /// </summary>
    [HttpPost("force/{sucursalId:guid}")]
    [Authorize(Policy = "Backoffice")]
    public IActionResult ForceSync(Guid sucursalId)
    {
        _sincronizacionService.ForzarSincronizacion(sucursalId);
        return Ok(new { message = "Sincronización forzada solicitada correctamente en la Nube." });
    }

    /// <summary>
    /// Consulta si hay una solicitud de sincronización forzada para la sucursal y retorna el intervalo.
    /// </summary>
    [HttpGet("check-force/{sucursalId:guid}")]
    [AllowAnonymous]
    public IActionResult CheckForceSync(Guid sucursalId, [FromQuery] Guid? dispositivoId)
    {
        var chequeo = _sincronizacionService.ChequearSincronizacionForzada(sucursalId, dispositivoId);

        return Ok(new
        {
            forceSync = chequeo.ForzarSincronizacion,
            syncIntervalSeconds = chequeo.IntervaloSegundos
        });
    }

    /// <summary>
    /// Configura el intervalo de sincronización para una sucursal.
    /// </summary>
    [HttpPost("interval/{sucursalId:guid}")]
    [Authorize(Policy = "Backoffice")]
    public async Task<IActionResult> SetSyncInterval(Guid sucursalId, [FromBody] SetSyncIntervalRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { message = "El intervalo es obligatorio." });

        await _sincronizacionService.ConfigurarIntervaloAsync(sucursalId, request.IntervalSeconds, ct);
        return Ok(new { message = $"Intervalo de sincronización actualizado a {request.IntervalSeconds} segundos." });
    }

    /// <summary>
    /// Recibe reportes de logs de las terminales locales.
    /// </summary>
    /// <remarks>
    /// Anónimo porque lo llama el POS local con el JWT M2M, pero el servicio valida
    /// que la sucursal exista y acota los tamaños antes de aceptarlo.
    /// </remarks>
    [HttpPost("log")]
    [AllowAnonymous]
    public async Task<IActionResult> ReportLog([FromBody] ReportLogRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest();

        await _sincronizacionService.RegistrarReporteAsync(
            request.SucursalId, request.SucursalNombre, request.Tipo, request.Mensaje, request.Exitoso, ct);

        return Ok();
    }

    /// <summary>
    /// Obtiene el historial reciente de logs de sincronización.
    /// </summary>
    [HttpGet("logs")]
    [Authorize(Policy = "Backoffice")]
    public IActionResult GetLogs()
        => Ok(_sincronizacionService.GetRegistrosRecientes(RegistrosRecientesMostrados));
}
