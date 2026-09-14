using BaresFamilia.Core.Models.Contratos.Seguridad;
using BaresFamilia.Core.Models.Dtos.Seguridad;
using BaresFamilia.Core.Models.Dtos.Sincronizacion;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para la gestión de empleados/usuarios.
/// Flujo: EmpleadoController → IEmpleadoService → EmpleadoService → IUsuarioRepository → UsuarioRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmpleadoController : ControllerBase
{
    private readonly IEmpleadoService _empleadoService;
    private readonly IMonitorSincronizacion _monitorSincronizacion;

    public EmpleadoController(IEmpleadoService empleadoService, IMonitorSincronizacion monitorSincronizacion)
    {
        _empleadoService = empleadoService;
        _monitorSincronizacion = monitorSincronizacion;
    }

    /// <summary>
    /// Obtiene todos los empleados con sus roles y sucursales.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(typeof(IEnumerable<EmpleadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? sucursalId, CancellationToken ct)
    {
        var empleados = await _empleadoService.GetAsync(sucursalId, User.GetAlcance(), ct);
        return Ok(empleados.Select(MapearEmpleado));
    }

    /// <summary>
    /// Crea un nuevo empleado.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(typeof(EmpleadoDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateEmpleadoRequest request, CancellationToken ct)
    {
        var datos = new DatosEmpleado(
            request.Nombre, request.Email, request.PinAcceso, request.RolId, request.SucursalId, request.Password);

        var usuario = await _empleadoService.CrearAsync(datos, User.GetAlcance(), ct);

        return CreatedAtAction(nameof(GetAll), new { id = usuario.Id }, MapearEmpleado(usuario));
    }

    /// <summary>
    /// Actualiza un empleado existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmpleadoRequest request, CancellationToken ct)
    {
        var datos = new DatosEmpleado(
            request.Nombre, request.Email, request.PinAcceso, request.RolId, request.SucursalId, request.Password);

        await _empleadoService.ActualizarAsync(id, datos, User.GetAlcance(), ct);
        return NoContent();
    }

    /// <summary>
    /// Elimina un empleado (desactivación lógica).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _empleadoService.DesactivarAsync(id, User.GetAlcance(), ct);
        return NoContent();
    }

    /// <summary>
    /// Endpoint especial para la sincronización del POS local.
    /// Devuelve todos los usuarios activos de una sucursal específica.
    /// </summary>
    [HttpGet("por-sucursal/{sucursalId:guid}")]
    public async Task<IActionResult> GetPorSucursal(Guid sucursalId, CancellationToken ct)
    {
        _monitorSincronizacion.RegistrarPull(sucursalId, TipoPull.Usuarios);

        var usuarios = await _empleadoService.GetActivosDeSucursalAsync(sucursalId, ct);

        return Ok(usuarios.Select(u => new SyncUsuarioDto(
            u.Id, u.RolId, u.SucursalId, u.Nombre, u.Email, u.PinAcceso, u.IsActive, u.CreatedAt, u.UpdatedAt)));
    }

    private static EmpleadoDto MapearEmpleado(Usuario usuario)
        => new(
            usuario.Id,
            usuario.Nombre,
            usuario.Email,
            usuario.PinAcceso,
            usuario.RolId,
            usuario.Rol.Nombre,
            usuario.SucursalId,
            usuario.Sucursal.Nombre,
            usuario.IsActive);
}
