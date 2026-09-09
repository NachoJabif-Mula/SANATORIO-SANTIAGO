using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Controlador local para gestionar la autenticación de usuarios del POS.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsuarioController : ControllerBase
{
    private readonly LocalContext _context;

    public UsuarioController(LocalContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Autentica a un usuario del POS validando su PIN de acceso.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginPinRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
            return BadRequest(new { message = "El PIN es obligatorio." });

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.PinAcceso == request.Pin.Trim() && u.IsActive, ct);

        if (usuario is null)
            return Unauthorized(new { message = "PIN incorrecto o usuario inactivo." });

        return Ok(new {
            id = usuario.Id,
            nombre = usuario.Nombre,
            pin = usuario.PinAcceso,
            rol = usuario.Rol.Nombre.ToLower(),
            permisos = usuario.Rol.Permisos ?? new List<string>()
        });
    }

    /// <summary>
    /// Valida que el PIN corresponda a un usuario con rol Gerente.
    /// </summary>
    [HttpPost("validar-gerente")]
    public async Task<IActionResult> ValidarGerente([FromBody] LoginPinRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
            return BadRequest(new { message = "El PIN es obligatorio." });

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.PinAcceso == request.Pin.Trim() && u.IsActive, ct);

        if (usuario is null || !(usuario.Rol.Permisos.Contains("gerente.override") || usuario.Rol.Nombre.ToLower() == "gerente"))
            return Unauthorized(new { message = "El PIN ingresado no corresponde a un gerente autorizado." });

        return Ok(new { valid = true });
    }

    /// <summary>
    /// Verifica si existen usuarios activos sincronizados en la base local.
    /// Usado por el POS tras la activación para saber cuándo es seguro mostrar la pantalla de PIN.
    /// </summary>
    [HttpGet("disponibles")]
    public async Task<IActionResult> Disponibles(CancellationToken ct)
    {
        var count = await _context.Usuarios.CountAsync(u => u.IsActive, ct);
        return Ok(new { disponibles = count > 0, count });
    }
}

public class LoginPinRequest
{
    public string Pin { get; set; } = string.Empty;
}
