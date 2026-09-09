using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Módulo de Activación de Dispositivos.
/// Gestiona la generación de códigos de activación para sucursales
/// y la emisión de tokens JWT M2M de larga duración.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DispositivosController : ControllerBase
{
    private readonly NubeContext _context;
    private readonly JwtSettings _jwtSettings;

    public DispositivosController(NubeContext context, IOptions<JwtSettings> jwtSettings)
    {
        _context = context;
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
    [Authorize]
    [ProducesResponseType(typeof(GenerarCodigoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerarCodigo([FromBody] GenerarCodigoRequest request, CancellationToken ct)
    {
        // Validar que la sucursal existe
        var sucursalExiste = await _context.Sucursales
            .AnyAsync(s => s.Id == request.SucursalId && s.IsActive, ct);

        if (!sucursalExiste)
            return BadRequest(new { message = $"La sucursal con ID '{request.SucursalId}' no existe o está inactiva." });

        // Generar código único con formato BAR-XXXX-XXXX
        var codigo = GenerarCodigoAlfanumerico();

        var dispositivo = new DispositivoActivacion
        {
            SucursalId = request.SucursalId,
            CodigoActivacion = codigo,
            NombreDispositivo = request.NombreDispositivo?.Trim() ?? "POS Sin Nombre",
            Activado = false,
            ExpiraCodigo = DateTime.UtcNow.AddHours(24)
        };

        _context.DispositivosActivacion.Add(dispositivo);
        await _context.SaveChangesAsync(ct);

        return Created(string.Empty, new GenerarCodigoResponse(
            DispositivoId: dispositivo.Id,
            CodigoActivacion: codigo,
            SucursalId: request.SucursalId,
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
        if (string.IsNullOrWhiteSpace(request.CodigoActivacion))
            return BadRequest(new { message = "El código de activación es obligatorio." });

        var dispositivo = await _context.DispositivosActivacion
            .Include(d => d.Sucursal)
            .FirstOrDefaultAsync(d =>
                d.CodigoActivacion == request.CodigoActivacion.Trim().ToUpper()
                && d.IsActive, ct);

        if (dispositivo is null)
            return NotFound(new { message = "Código de activación no encontrado." });

        if (dispositivo.Activado)
            return BadRequest(new { message = "Este código ya fue utilizado. Solicite uno nuevo." });

        if (dispositivo.ExpiraCodigo < DateTime.UtcNow)
            return BadRequest(new { message = "El código de activación ha expirado. Solicite uno nuevo." });

        // Generar JWT M2M de larga duración
        var (token, expiration) = GenerarTokenM2M(dispositivo);

        // Marcar como activado y guardar hash del token
        dispositivo.Activado = true;
        dispositivo.FechaActivacion = DateTime.UtcNow;
        dispositivo.TokenHash = ComputeSha256(token);
        dispositivo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return Ok(new ActivarResponse(
            Token: token,
            TokenType: "Bearer",
            ExpiresAt: expiration,
            SucursalId: dispositivo.SucursalId,
            SucursalNombre: dispositivo.Sucursal.Nombre,
            DispositivoId: dispositivo.Id,
            NombreDispositivo: dispositivo.NombreDispositivo
        ));
    }

    // ═══════════════════════════════════════════════════════
    // MÉTODOS PRIVADOS
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Genera un código con formato BAR-XXXX-XXXX usando caracteres alfanuméricos
    /// (sin caracteres ambiguos: 0/O, 1/I/L).
    /// </summary>
    private static string GenerarCodigoAlfanumerico()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var random = RandomNumberGenerator.Create();
        var bytes = new byte[8];
        random.GetBytes(bytes);

        var part1 = new string(bytes.Take(4).Select(b => chars[b % chars.Length]).ToArray());
        var part2 = new string(bytes.Skip(4).Select(b => chars[b % chars.Length]).ToArray());

        return $"BAR-{part1}-{part2}";
    }

    /// <summary>
    /// Genera un JWT M2M de larga duración para un dispositivo vinculado a una sucursal.
    /// </summary>
    private (string token, DateTime expiration) GenerarTokenM2M(DispositivoActivacion dispositivo)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddDays(_jwtSettings.M2MTokenDurationDays);

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
            expires: expiration,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
    }

    /// <summary>
    /// Computa SHA-256 del token para almacenamiento seguro (permite revocar).
    /// </summary>
    private static string ComputeSha256(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }

    // ═══════════════════════════════════════════════════════
    // ENDPOINT 3: Listar todos los Dispositivos / Activaciones
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Lista todos los dispositivos y códigos de activación registrados.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<DispositivoDetalleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarDispositivos(CancellationToken ct)
    {
        var dispositivos = await _context.DispositivosActivacion
            .Include(d => d.Sucursal)
            .Where(d => d.IsActive)
            .Select(d => new DispositivoDetalleResponse(
                d.Id,
                d.Sucursal.Nombre,
                d.CodigoActivacion,
                d.NombreDispositivo,
                d.Activado,
                d.FechaActivacion,
                d.ExpiraCodigo
            ))
            .ToListAsync(ct);

        return Ok(dispositivos);
    }

    // ═══════════════════════════════════════════════════════
    // ENDPOINT 4: Revocar Dispositivo
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Revoca un dispositivo/código de activación, desactivándolo.
    /// </summary>
    [HttpPost("{id:guid}/revocar")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevocarDispositivo(Guid id, CancellationToken ct)
    {
        var dispositivo = await _context.DispositivosActivacion.FindAsync(new object[] { id }, ct);
        if (dispositivo is null || !dispositivo.IsActive)
            return NotFound(new { message = "Dispositivo no encontrado o ya inactivo." });

        dispositivo.IsActive = false;
        dispositivo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
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
        var dispositivo = await _context.DispositivosActivacion
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (dispositivo is null)
            return NotFound(new { activo = false, message = "Dispositivo no encontrado." });

        return Ok(new { activo = dispositivo.IsActive && dispositivo.Activado });
    }
}

// ═══════════════════════════════════
// DTOs
// ═══════════════════════════════════

public record GenerarCodigoRequest(
    Guid SucursalId,
    string? NombreDispositivo
);

public record GenerarCodigoResponse(
    Guid DispositivoId,
    string CodigoActivacion,
    Guid SucursalId,
    string NombreDispositivo,
    DateTime ExpiraEn
);

public record ActivarRequest(
    string CodigoActivacion
);

public record ActivarResponse(
    string Token,
    string TokenType,
    DateTime ExpiresAt,
    Guid SucursalId,
    string SucursalNombre,
    Guid DispositivoId,
    string NombreDispositivo
);

public record DispositivoDetalleResponse(
    Guid Id,
    string Sucursal,
    string Codigo,
    string Descripcion,
    bool IsActivado,
    DateTime? ActivadoEn,
    DateTime ExpiraEn
);

