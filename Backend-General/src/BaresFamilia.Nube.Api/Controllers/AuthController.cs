using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Core.Models.Contratos.Seguridad;
using BaresFamilia.Core.Models.Dtos.Seguridad;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador de autenticación para el Backoffice.
/// Emite JWT con duración variable según "Mantener sesión".
/// Flujo: AuthController → IAutenticacionBackofficeService → AutenticacionBackofficeService → I*Repository → *Repository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private static readonly TimeSpan DuracionSesionLarga = TimeSpan.FromDays(30);
    private static readonly TimeSpan DuracionSesionCorta = TimeSpan.FromHours(1);

    private readonly IAutenticacionBackofficeService _autenticacionService;
    private readonly JwtSettings _jwt;

    public AuthController(IAutenticacionBackofficeService autenticacionService, IOptions<JwtSettings> jwt)
    {
        _autenticacionService = autenticacionService;
        _jwt = jwt.Value;
    }

    // ════════════════════════════════════════
    // POST /api/auth/login
    // ════════════════════════════════════════

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email y contraseña son obligatorios." });

        var usuario = await _autenticacionService.ValidarCredencialesAsync(request.Email, request.Password, ct);
        if (usuario is null)
            return Unauthorized(new { message = "Credenciales inválidas." });

        var duracion = request.RememberMe ? DuracionSesionLarga : DuracionSesionCorta;
        var token = GenerateJwt(usuario, duracion);

        // Si el usuario tildó "mantener sesión iniciada", persistimos la sesión
        // para poder validarla/revocarla contra la base en cada request en vez
        // de confiar únicamente en la expiración firmada del JWT.
        if (request.RememberMe)
        {
            await _autenticacionService.RegistrarSesionAsync(
                usuario.Id,
                token,
                DateTime.UtcNow.Add(duracion),
                Request.Headers.UserAgent.ToString(),
                ct);
        }

        return Ok(new LoginResponse(token, MapearUsuario(usuario)));
    }

    // ════════════════════════════════════════
    // GET /api/auth/me
    // ════════════════════════════════════════

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var usuario = await _autenticacionService.GetPerfilAsync(userId, ct);
        if (usuario is null)
            return Unauthorized();

        return Ok(MapearUsuario(usuario));
    }

    // ════════════════════════════════════════
    // POST /api/auth/logout
    // ════════════════════════════════════════

    /// <summary>
    /// Revoca la sesión persistida asociada al JWT actual (si existía, es decir,
    /// si el login se hizo con "mantener sesión iniciada"). Los tokens de sesión
    /// corta (sin "mantener sesión") no se persisten y solo expiran naturalmente.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _autenticacionService.RevocarSesionAsync(Request.Headers.LeerTokenBearer(), ct);
        return NoContent();
    }

    // ════════════════════════════════════════
    // Helpers privados
    // ════════════════════════════════════════

    private static UsuarioDto MapearUsuario(Usuario usuario)
        => new(
            Id: usuario.Id,
            Nombre: usuario.Nombre,
            Email: usuario.Email,
            Rol: usuario.Rol.Nombre,
            Sucursal: usuario.Sucursal.Nombre,
            SucursalId: usuario.SucursalId,
            EsGlobal: usuario.Rol.EsGlobal);

    private string GenerateJwt(Usuario usuario, TimeSpan expiration)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Name, usuario.Nombre),
            new Claim(ClaimTypes.Role, usuario.Rol.Nombre),
            new Claim(ClaimsPrincipalExtensions.SucursalIdClaim, usuario.SucursalId.ToString()),
            new Claim(ClaimsPrincipalExtensions.EsGlobalClaim, usuario.Rol.EsGlobal.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: "BaresFamilia.Backoffice",
            claims: claims,
            expires: DateTime.UtcNow.Add(expiration),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
