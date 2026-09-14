using BaresFamilia.Core.Models.Contratos.Seguridad;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Controlador local para gestionar la autenticación de usuarios del POS.
/// Flujo: UsuarioController → IAutenticacionPosService → AutenticacionPosService → IUsuarioRepository → UsuarioRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsuarioController : ControllerBase
{
    private readonly IAutenticacionPosService _autenticacionService;

    public UsuarioController(IAutenticacionPosService autenticacionService)
    {
        _autenticacionService = autenticacionService;
    }

    /// <summary>
    /// Autentica a un usuario del POS validando su PIN de acceso.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginPinRequest request, CancellationToken ct)
    {
        var usuario = await _autenticacionService.AutenticarPorPinAsync(request.Pin, ct);
        if (usuario is null)
            return Unauthorized(new { message = "PIN incorrecto o usuario inactivo." });

        return Ok(new
        {
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
        if (!await _autenticacionService.EsGerenteAsync(request.Pin, ct))
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
        var count = await _autenticacionService.ContarUsuariosDisponiblesAsync(ct);
        return Ok(new { disponibles = count > 0, count });
    }
}
