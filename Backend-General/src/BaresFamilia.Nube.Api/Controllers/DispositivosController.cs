using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Core.Models.Contratos.Seguridad;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Módulo de Activación de Dispositivos.
/// Gestiona la generación de códigos de activación para sucursales
/// y la emisión de tokens JWT M2M de larga duración.
/// Flujo: DispositivosController → IActivacionDispositivoService → ActivacionDispositivoService → IDispositivoActivacionRepository → DispositivoActivacionRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DispositivosController : ControllerBase
{
    private readonly IActivacionDispositivoService _activacionService;
    private readonly JwtSettings _jwtSettings;

    public DispositivosController(IActivacionDispositivoService activacionService, IOptions<JwtSettings> jwtSettings)
    {
        _activacionService = activacionService;
        _jwtSettings = jwtSettings.Value;
    }

    // ═══════════════════════════════════════════════════════
    // ENDPOINT 1: Generar Código de Activación
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Genera un código alfanumérico único de activación para una sucursal.
    /// Formato: BAR-XXXX-XXXX (ej: BAR-7X9P-M2A1).
    /// El código expira en 24 horas si no es utilizado.
    /// </summary>
    /// <remarks>
    /// Este endpoint debe ser invocado por un administrador autenticado.
    /// El código generado se entrega al personal de la sucursal para
    /// ingresarlo en el POS local durante la configuración inicial.
    /// </remarks>
    [HttpPost("generar-codigo")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(typeof(GenerarCodigoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarCodigo([FromBody] GenerarCodigoRequest request, CancellationToken ct)
    {
        var dispositivo = await _activacionService.GenerarCodigoAsync(request.SucursalId, request.NombreDispositivo, ct);

        return Created(string.Empty, new GenerarCodigoResponse(
            DispositivoId: dispositivo.Id,
            CodigoActivacion: dispositivo.CodigoActivacion,
            SucursalId: dispositivo.SucursalId,
            NombreDispositivo: dispositivo.NombreDispositivo,
            ExpiraEn: dispositivo.ExpiraCodigo
        ));
    }

    // ═══════════════════════════════════════════════════════
    // ENDPOINT 2: Activar Dispositivo (canjear código por JWT M2M)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Recibe un código de activación desde el POS local y devuelve
    /// un JWT de Máquina a Máquina (M2M) de larga duración.
    /// </summary>
    /// <remarks>
    /// Este endpoint NO requiere autenticación previa.
    /// Es el punto de entrada para que un POS local se vincule al sistema.
    /// El JWT emitido autoriza la sincronización futura con la API Nube.
    /// </remarks>
    [HttpPost("activar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ActivarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activar([FromBody] ActivarRequest request, CancellationToken ct)
    {
        var dispositivo = await _activacionService.ValidarCodigoCanjeableAsync(request.CodigoActivacion, ct);

        var (token, expiracion) = GenerarTokenM2M(dispositivo);
        await _activacionService.ConfirmarActivacionAsync(dispositivo, token, ct);

        return Ok(new ActivarResponse(
            Token: token,
            TokenType: "Bearer",
            ExpiresAt: expiracion,
            SucursalId: dispositivo.SucursalId,
            SucursalNombre: dispositivo.Sucursal.Nombre,
            DispositivoId: dispositivo.Id,
            NombreDispositivo: dispositivo.NombreDispositivo
        ));
    }

    // ═══════════════════════════════════════════════════════
    // ENDPOINT 3: Listar todos los Dispositivos / Activaciones
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Lista todos los dispositivos y códigos de activación registrados.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(typeof(IEnumerable<DispositivoDetalleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarDispositivos(CancellationToken ct)
    {
        var dispositivos = await _activacionService.ListarVigentesAsync(ct);

        return Ok(dispositivos.Select(d => new DispositivoDetalleResponse(
            d.Id,
            d.Sucursal.Nombre,
            d.CodigoActivacion,
            d.NombreDispositivo,
            d.Activado,
            d.FechaActivacion,
            d.ExpiraCodigo
        )));
    }

    // ═══════════════════════════════════════════════════════
    // ENDPOINT 4: Revocar Dispositivo
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Revoca un dispositivo/código de activación, desactivándolo.
    /// </summary>
    [HttpPost("{id:guid}/revocar")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevocarDispositivo(Guid id, CancellationToken ct)
    {
        await _activacionService.RevocarAsync(id, ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════
    // ENDPOINT 5: Verificar Estado de Activación (para polling desde POS local)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Consulta si un dispositivo sigue activo. El POS local lo usa en cada ciclo
    /// de sincronización para detectar revocaciones remotas desde el Backoffice.
    /// </summary>
    [HttpGet("{id:guid}/estado-activacion")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EstadoActivacion(Guid id, CancellationToken ct)
    {
        var activo = await _activacionService.EstaActivoAsync(id, ct);

        if (activo is null)
            return NotFound(new { activo = false, message = "Dispositivo no encontrado." });

        return Ok(new { activo = activo.Value });
    }

    // ═══════════════════════════════════════════════════════
    // MÉTODOS PRIVADOS
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Genera un JWT M2M de larga duración para un dispositivo vinculado a una sucursal.
    /// </summary>
    private (string Token, DateTime Expiracion) GenerarTokenM2M(DispositivoActivacion dispositivo)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiracion = DateTime.UtcNow.AddDays(_jwtSettings.M2MTokenDurationDays);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, dispositivo.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("tipo", "m2m"),
            new Claim("sucursal_id", dispositivo.SucursalId.ToString()),
            new Claim("dispositivo_id", dispositivo.Id.ToString()),
            new Claim("dispositivo_nombre", dispositivo.NombreDispositivo),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiracion,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiracion);
    }
}
