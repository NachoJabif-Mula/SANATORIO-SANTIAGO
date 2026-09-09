using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador de autenticación para el Backoffice.
/// Emite JWT con duración variable según "Mantener sesión".
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly NubeContext _db;
    private readonly JwtSettings _jwt;

    public AuthController(NubeContext db, IOptions<JwtSettings> jwt)
    {
        _db = db;
        _jwt = jwt.Value;
    }

    // ════════════════════════════════════════
    // POST /api/auth/login
    // ════════════════════════════════════════

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email y contraseña son obligatorios." });

        var usuario = await _db.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Sucursal)
            .FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLower() && u.IsActive, ct);

        if (usuario is null)
            return Unauthorized(new { message = "Credenciales inválidas." });

        // Verificar password con BCrypt
        if (!BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash))
            return Unauthorized(new { message = "Credenciales inválidas." });

        // Generar JWT con duración según rememberMe
        var expiration = request.RememberMe
            ? TimeSpan.FromDays(30)
            : TimeSpan.FromHours(1);

        var token = GenerateJwt(usuario, expiration);

        return Ok(new LoginResponse(
            Token: token,
            Usuario: new UsuarioDto(
                Id: usuario.Id,
                Nombre: usuario.Nombre,
                Email: usuario.Email,
                Rol: usuario.Rol.Nombre,
                Sucursal: usuario.Sucursal.Nombre,
                SucursalId: usuario.SucursalId,
                EsGlobal: usuario.Rol.EsGlobal
            )
        ));
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

        var usuario = await _db.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Sucursal)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);

        if (usuario is null)
            return Unauthorized();

        return Ok(new UsuarioDto(
            Id: usuario.Id,
            Nombre: usuario.Nombre,
            Email: usuario.Email,
            Rol: usuario.Rol.Nombre,
            Sucursal: usuario.Sucursal.Nombre,
            SucursalId: usuario.SucursalId,
            EsGlobal: usuario.Rol.EsGlobal
        ));
    }

    // ════════════════════════════════════════
    // Helpers privados
    // ════════════════════════════════════════

    private string GenerateJwt(Core.Models.Entities.Catalogo.Usuario usuario, TimeSpan expiration)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Name, usuario.Nombre),
            new Claim(ClaimTypes.Role, usuario.Rol.Nombre),
            new Claim(Extensions.ClaimsPrincipalExtensions.SucursalIdClaim, usuario.SucursalId.ToString()),
            new Claim(Extensions.ClaimsPrincipalExtensions.EsGlobalClaim, usuario.Rol.EsGlobal.ToString()),
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

// ════════════════════════════════════════
// DTOs de Auth
// ════════════════════════════════════════

public record LoginRequest(string Email, string Password, bool RememberMe = false);

public record LoginResponse(string Token, UsuarioDto Usuario);

public record UsuarioDto(Guid Id, string Nombre, string Email, string Rol, string Sucursal, Guid SucursalId, bool EsGlobal);
