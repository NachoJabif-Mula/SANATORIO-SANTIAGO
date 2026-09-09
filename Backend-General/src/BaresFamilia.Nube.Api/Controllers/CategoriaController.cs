using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para el ABM y sincronización de Categorías.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriaController : ControllerBase
{
    private readonly NubeContext _context;

    public CategoriaController(NubeContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene las categorías. Si includeInactive es true, devuelve todas (activas e inactivas) para sincronización.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Categoria>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var sucursalIdClaim = User.FindFirst("sucursal_id")?.Value;
        if (!string.IsNullOrEmpty(sucursalIdClaim) && Guid.TryParse(sucursalIdClaim, out var sucursalId))
        {
            SyncManagerStore.RecordPull(sucursalId, "config"); // Marcamos actividad de pull para la sucursal
        }

        IQueryable<Categoria> query = _context.Categorias;

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var categorias = await query.OrderBy(c => c.OrdenVisual).ToListAsync(ct);
        return Ok(categorias);
    }

    /// <summary>
    /// Obtiene una categoría por su ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Categoria), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var categoria = await _context.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);
        if (categoria is null)
            return NotFound(new { message = $"Categoría con ID '{id}' no encontrada." });

        return Ok(categoria);
    }

    /// <summary>
    /// Crea una nueva categoría.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Categoria), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCategoriaRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre de la categoría es obligatorio." });

        var categoria = new Categoria
        {
            Nombre = request.Nombre.Trim(),
            OrdenVisual = request.OrdenVisual,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = categoria.Id }, categoria);
    }

    /// <summary>
    /// Actualiza una categoría existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoriaRequest request, CancellationToken ct)
    {
        var categoria = await _context.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);
        if (categoria is null)
            return NotFound(new { message = $"Categoría con ID '{id}' no encontrada." });

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre de la categoría es obligatorio." });

        categoria.Nombre = request.Nombre.Trim();
        categoria.OrdenVisual = request.OrdenVisual;
        categoria.UpdatedAt = DateTime.UtcNow;

        _context.Categorias.Update(categoria);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Desactiva una categoría (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var categoria = await _context.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);
        if (categoria is null)
            return NotFound(new { message = $"Categoría con ID '{id}' no encontrada." });

        categoria.IsActive = false;
        categoria.UpdatedAt = DateTime.UtcNow;

        _context.Categorias.Update(categoria);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}

public record CreateCategoriaRequest(string Nombre, int OrdenVisual);
public record UpdateCategoriaRequest(string Nombre, int OrdenVisual);
