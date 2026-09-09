using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para el ABM y sincronización de Tipos de Venta.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TipoVentaController : ControllerBase
{
    private readonly NubeContext _context;

    public TipoVentaController(NubeContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene todos los tipos de venta. Si includeInactive es true, devuelve todos (activos e inactivos) para sincronización.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TipoVenta>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        // Asegurar semillas básicas de Tipos de Venta si no hay ninguna
        if (!await _context.TiposVenta.AnyAsync(ct))
        {
            _context.TiposVenta.AddRange(
                new TipoVenta { Id = Guid.NewGuid(), Nombre = "Salón", AplicaRecargo = false, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TipoVenta { Id = Guid.NewGuid(), Nombre = "Delivery", AplicaRecargo = true, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TipoVenta { Id = Guid.NewGuid(), Nombre = "Mostrador", AplicaRecargo = false, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );
            await _context.SaveChangesAsync(ct);
        }

        IQueryable<TipoVenta> query = _context.TiposVenta;

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        var tipos = await query.OrderBy(t => t.Nombre).ToListAsync(ct);
        return Ok(tipos);
    }

    /// <summary>
    /// Obtiene un tipo de venta por ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TipoVenta), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tipo = await _context.TiposVenta.FirstOrDefaultAsync(t => t.Id == id && t.IsActive, ct);
        if (tipo is null)
            return NotFound(new { message = $"Tipo de venta con ID '{id}' no encontrado." });

        return Ok(tipo);
    }

    /// <summary>
    /// Crea un nuevo tipo de venta.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TipoVenta), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTipoVentaRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });

        var tipo = new TipoVenta
        {
            Nombre = request.Nombre.Trim(),
            AplicaRecargo = request.AplicaRecargo,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TiposVenta.Add(tipo);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = tipo.Id }, tipo);
    }

    /// <summary>
    /// Actualiza un tipo de venta existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTipoVentaRequest request, CancellationToken ct)
    {
        var tipo = await _context.TiposVenta.FirstOrDefaultAsync(t => t.Id == id && t.IsActive, ct);
        if (tipo is null)
            return NotFound(new { message = $"Tipo de venta con ID '{id}' no encontrado." });

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });

        tipo.Nombre = request.Nombre.Trim();
        tipo.AplicaRecargo = request.AplicaRecargo;
        tipo.UpdatedAt = DateTime.UtcNow;

        _context.TiposVenta.Update(tipo);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Desactiva un tipo de venta (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tipo = await _context.TiposVenta.FirstOrDefaultAsync(t => t.Id == id && t.IsActive, ct);
        if (tipo is null)
            return NotFound(new { message = $"Tipo de venta con ID '{id}' no encontrado." });

        tipo.IsActive = false;
        tipo.UpdatedAt = DateTime.UtcNow;

        _context.TiposVenta.Update(tipo);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}

public record CreateTipoVentaRequest(string Nombre, bool AplicaRecargo);
public record UpdateTipoVentaRequest(string Nombre, bool AplicaRecargo);
