using BaresFamilia.Core.Models.Contratos.Seguridad;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Controlador para gestionar la activación local del POS y verificar su estado.
/// Flujo: DispositivoController → IActivacionPosService → ActivacionPosService → I*Repository → *Repository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DispositivoController : ControllerBase
{
    private readonly IActivacionPosService _activacionService;

    public DispositivoController(IActivacionPosService activacionService)
    {
        _activacionService = activacionService;
    }

    /// <summary>
    /// Verifica si el dispositivo local ya se encuentra activado.
    /// </summary>
    [HttpGet("estado")]
    public async Task<IActionResult> ObtenerEstado(CancellationToken ct)
    {
        var estado = await _activacionService.GetEstadoAsync(ct);

        if (estado is null)
            return Ok(new { activado = false });

        return Ok(new
        {
            activado = true,
            sucursalId = estado.SucursalId,
            sucursalNombre = estado.SucursalNombre,
            dispositivoId = estado.DispositivoId,
            nombreDispositivo = estado.NombreDispositivo,
            expiresAt = estado.ExpiresAt
        });
    }

    /// <summary>
    /// Canjea un código de activación en la API de Nube y guarda el token M2M localmente en la base de datos.
    /// </summary>
    [HttpPost("activar")]
    public async Task<IActionResult> Activar([FromBody] ActivarLocalRequest request, CancellationToken ct)
    {
        await _activacionService.ActivarAsync(request.CodigoActivacion, ct);
        return Ok(new { message = "Dispositivo activado con éxito." });
    }

    /// <summary>
    /// Desactiva el dispositivo local, dando de baja los registros de activación.
    /// </summary>
    [HttpPost("desactivar")]
    public async Task<IActionResult> Desactivar(CancellationToken ct)
    {
        await _activacionService.DesactivarAsync(ct);
        return Ok(new { desactivado = true, message = "Dispositivo desactivado correctamente." });
    }
}
