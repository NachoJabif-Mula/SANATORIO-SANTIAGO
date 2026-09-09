using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para la gestión de empleados/usuarios.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmpleadoController : ControllerBase
{
    private readonly NubeContext _db;

    public EmpleadoController(NubeContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Obtiene todos los empleados con sus roles y sucursales.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmpleadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? sucursalId, CancellationToken ct)
    {
        var sucId = User.IsGlobal() ? sucursalId : User.GetSucursalId();

        var query = _db.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Sucursal)
            .Where(u => u.IsActive);

        if (sucId.HasValue)
            query = query.Where(u => u.SucursalId == sucId.Value);

        var empleados = await query
            .Select(u => new EmpleadoDto(
                u.Id,
                u.Nombre,
                u.Email,
                u.PinAcceso,
                u.RolId,
                u.Rol.Nombre,
                u.SucursalId,
                u.Sucursal.Nombre,
                u.IsActive
            ))
            .ToListAsync(ct);

        return Ok(empleados);
    }

    /// <summary>
    /// Crea un nuevo empleado.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmpleadoDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateEmpleadoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });
        if (string.IsNullOrWhiteSpace(request.PinAcceso))
            return BadRequest(new { message = "El PIN de acceso es obligatorio." });

        // Un usuario no-global solo puede crear empleados en su propia sucursal
        var sucursalId = request.SucursalId;
        if (!User.IsGlobal())
        {
            var propiaSucursal = User.GetSucursalId();
            if (propiaSucursal is null)
                return Forbid();
            sucursalId = propiaSucursal.Value;
        }

        // Verificar si el PIN ya está en uso en la misma sucursal
        var pinExistente = await _db.Usuarios.AnyAsync(u =>
            u.SucursalId == sucursalId &&
            u.PinAcceso == request.PinAcceso.Trim() &&
            u.IsActive, ct);

        if (pinExistente)
            return BadRequest(new { message = "El PIN ingresado ya está asignado a otro empleado en esta sucursal." });

        var rol = await _db.Roles.FindAsync(new object[] { request.RolId }, ct);
        if (rol == null || !rol.IsActive)
            return BadRequest(new { message = "El rol seleccionado no es válido." });

        if (rol.EsGlobal && !User.IsGlobal())
            return Forbid();

        var sucursal = await _db.Set<Sucursal>().FindAsync(new object[] { sucursalId }, ct);
        if (sucursal == null || !sucursal.IsActive)
            return BadRequest(new { message = "La sucursal seleccionada no es válida." });

        var usuario = new Usuario
        {
            Nombre = request.Nombre.Trim(),
            Email = request.Email?.Trim().ToLower() ?? string.Empty,
            PinAcceso = request.PinAcceso.Trim(),
            RolId = request.RolId,
            SucursalId = sucursalId,
            PasswordHash = !string.IsNullOrWhiteSpace(request.Password) 
                ? BCrypt.Net.BCrypt.HashPassword(request.Password)
                : string.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(ct);

        var dto = new EmpleadoDto(
            usuario.Id,
            usuario.Nombre,
            usuario.Email,
            usuario.PinAcceso,
            usuario.RolId,
            rol.Nombre,
            usuario.SucursalId,
            sucursal.Nombre,
            usuario.IsActive
        );

        return CreatedAtAction(nameof(GetAll), new { id = usuario.Id }, dto);
    }

    /// <summary>
    /// Actualiza un empleado existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmpleadoRequest request, CancellationToken ct)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);
        if (usuario == null)
            return NotFound(new { message = "Empleado no encontrado." });

        // Un usuario no-global solo puede editar empleados de su propia sucursal,
        // y no puede reasignarlos a otra sucursal.
        var sucursalId = request.SucursalId;
        if (!User.IsGlobal())
        {
            var propiaSucursal = User.GetSucursalId();
            if (propiaSucursal is null || usuario.SucursalId != propiaSucursal.Value)
                return Forbid();
            sucursalId = propiaSucursal.Value;
        }

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });
        if (string.IsNullOrWhiteSpace(request.PinAcceso))
            return BadRequest(new { message = "El PIN de acceso es obligatorio." });

        // Verificar si el PIN está en uso por otro empleado de la sucursal
        var pinExistente = await _db.Usuarios.AnyAsync(u =>
            u.Id != id &&
            u.SucursalId == sucursalId &&
            u.PinAcceso == request.PinAcceso.Trim() &&
            u.IsActive, ct);

        if (pinExistente)
            return BadRequest(new { message = "El PIN ingresado ya está asignado a otro empleado en esta sucursal." });

        var rol = await _db.Roles.FindAsync(new object[] { request.RolId }, ct);
        if (rol == null || !rol.IsActive)
            return BadRequest(new { message = "El rol seleccionado no es válido." });

        if (rol.EsGlobal && !User.IsGlobal())
            return Forbid();

        var sucursal = await _db.Set<Sucursal>().FindAsync(new object[] { sucursalId }, ct);
        if (sucursal == null || !sucursal.IsActive)
            return BadRequest(new { message = "La sucursal seleccionada no es válida." });

        usuario.Nombre = request.Nombre.Trim();
        usuario.Email = request.Email?.Trim().ToLower() ?? string.Empty;
        usuario.PinAcceso = request.PinAcceso.Trim();
        usuario.RolId = request.RolId;
        usuario.SucursalId = sucursalId;
        usuario.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Elimina un empleado (desactivación lógica).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);
        if (usuario == null)
            return NotFound(new { message = "Empleado no encontrado." });

        if (!User.IsGlobal() && usuario.SucursalId != User.GetSucursalId())
            return Forbid();

        // Evitar que el administrador se borre a sí mismo
        if (usuario.Email == "admin@baresfamilia.com")
            return BadRequest(new { message = "No se puede eliminar al usuario administrador del sistema." });

        usuario.IsActive = false;
        usuario.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Endpoint especial para la sincronización del POS local.
    /// Devuelve todos los usuarios activos de una sucursal específica.
    /// </summary>
    [HttpGet("por-sucursal/{sucursalId:guid}")]
    public async Task<IActionResult> GetPorSucursal(Guid sucursalId, CancellationToken ct)
    {
        SyncManagerStore.RecordPull(sucursalId, "usuarios");
        var usuarios = await _db.Usuarios
            .Where(u => u.SucursalId == sucursalId && u.IsActive)
            .Select(u => new SyncUsuarioDto(
                u.Id,
                u.RolId,
                u.SucursalId,
                u.Nombre,
                u.Email,
                u.PinAcceso,
                u.IsActive,
                u.CreatedAt,
                u.UpdatedAt
            ))
            .ToListAsync(ct);

        return Ok(usuarios);
    }
}

public record EmpleadoDto(
    Guid Id,
    string Nombre,
    string Email,
    string PinAcceso,
    Guid RolId,
    string RolNombre,
    Guid SucursalId,
    string SucursalNombre,
    bool IsActive
);

public record CreateEmpleadoRequest(
    string Nombre,
    string? Email,
    string PinAcceso,
    Guid RolId,
    Guid SucursalId,
    string? Password
);

public record UpdateEmpleadoRequest(
    string Nombre,
    string? Email,
    string PinAcceso,
    Guid RolId,
    Guid SucursalId,
    string? Password
);

public record SyncUsuarioDto(
    Guid Id,
    Guid RolId,
    Guid SucursalId,
    string Nombre,
    string Email,
    string PinAcceso,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
