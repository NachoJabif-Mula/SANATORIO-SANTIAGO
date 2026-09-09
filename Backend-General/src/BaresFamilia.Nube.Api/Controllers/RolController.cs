using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para la gestión de roles.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolController : ControllerBase
{
    private readonly IService<Rol> _rolService;

    public RolController(IService<Rol> rolService)
    {
        _rolService = rolService;
    }

    /// <summary>
    /// Obtiene todos los roles activos.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Rol>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var sucursalIdClaim = User.FindFirst("sucursal_id")?.Value;
        if (!string.IsNullOrEmpty(sucursalIdClaim) && Guid.TryParse(sucursalIdClaim, out var sucursalId))
        {
            SyncManagerStore.RecordPull(sucursalId, "roles");
        }

        var roles = await _rolService.GetAllAsync(ct);
        return Ok(roles.Where(r => r.IsActive));
    }

    /// <summary>
    /// Crea un nuevo rol.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Rol), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateRolRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre del rol es obligatorio." });

        if (request.EsGlobal && !User.IsGlobal())
            return Forbid();

        var rol = new Rol
        {
            Nombre = request.Nombre.Trim(),
            Permisos = request.Permisos ?? [],
            EsGlobal = request.EsGlobal,
            IsActive = true
        };

        var created = await _rolService.CreateAsync(rol, ct);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    /// <summary>
    /// Actualiza un rol existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRolRequest request, CancellationToken ct)
    {
        var rol = await _rolService.GetByIdAsync(id, ct);
        if (rol == null)
            return NotFound(new { message = "Rol no encontrado." });

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre del rol es obligatorio." });

        if (request.EsGlobal != rol.EsGlobal && !User.IsGlobal())
            return Forbid();

        rol.Nombre = request.Nombre.Trim();
        rol.Permisos = request.Permisos ?? [];
        rol.EsGlobal = request.EsGlobal;
        rol.UpdatedAt = DateTime.UtcNow;

        await _rolService.UpdateAsync(rol, ct);
        return NoContent();
    }

    /// <summary>
    /// Elimina un rol (desactivación lógica).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var rol = await _rolService.GetByIdAsync(id, ct);
        if (rol == null)
            return NotFound(new { message = "Rol no encontrado." });

        rol.IsActive = false;
        rol.UpdatedAt = DateTime.UtcNow;

        await _rolService.UpdateAsync(rol, ct);
        return NoContent();
    }
}

public record CreateRolRequest(string Nombre, List<string> Permisos, bool EsGlobal = false);
public record UpdateRolRequest(string Nombre, List<string> Permisos, bool EsGlobal = false);
