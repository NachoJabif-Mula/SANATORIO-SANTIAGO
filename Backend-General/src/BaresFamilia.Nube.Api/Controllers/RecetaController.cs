using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// ABM (CRUD) completo para la entidad Receta.
/// Relaciona Producto → Insumo con la cantidad necesaria por unidad.
/// Flujo: RecetaController → IRecetaService → RecetaService → IRecetaRepository → RecetaRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecetaController : ControllerBase
{
    private readonly IRecetaService _recetaService;

    public RecetaController(IRecetaService recetaService)
    {
        _recetaService = recetaService;
    }

    /// <summary>
    /// Obtiene todas las recetas activas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Receta>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var recetas = await _recetaService.GetAllAsync(ct);
        return Ok(recetas);
    }

    /// <summary>
    /// Obtiene una receta por su ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Receta), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var receta = await _recetaService.GetByIdAsync(id, ct);
        if (receta is null)
            return NotFound(new { message = $"Receta con ID '{id}' no encontrada." });

        return Ok(receta);
    }

    /// <summary>
    /// Obtiene todas las recetas (insumos) de un producto específico.
    /// </summary>
    [HttpGet("por-producto/{productoId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<Receta>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProducto(Guid productoId, CancellationToken ct)
    {
        var recetas = await _recetaService.GetByProductoAsync(productoId, ct);
        return Ok(recetas);
    }

    /// <summary>
    /// Crea una nueva receta (asigna un insumo a un producto).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Receta), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateRecetaRequest request, CancellationToken ct)
    {
        if (request.CantidadNecesaria <= 0)
            return BadRequest(new { message = "La cantidad necesaria debe ser mayor a 0." });

        var receta = new Receta
        {
            ProductoId = request.ProductoId,
            InsumoId = request.InsumoId,
            CantidadNecesaria = request.CantidadNecesaria
        };

        var created = await _recetaService.CreateAsync(receta, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Actualiza la cantidad necesaria de una receta existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRecetaRequest request, CancellationToken ct)
    {
        var receta = await _recetaService.GetByIdAsync(id, ct);
        if (receta is null)
            return NotFound(new { message = $"Receta con ID '{id}' no encontrada." });

        if (request.CantidadNecesaria <= 0)
            return BadRequest(new { message = "La cantidad necesaria debe ser mayor a 0." });

        receta.CantidadNecesaria = request.CantidadNecesaria;

        await _recetaService.UpdateAsync(receta, ct);
        return NoContent();
    }

    /// <summary>
    /// Elimina una receta (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var exists = await _recetaService.ExistsAsync(id, ct);
        if (!exists)
            return NotFound(new { message = $"Receta con ID '{id}' no encontrada." });

        await _recetaService.DeleteAsync(id, ct);
        return NoContent();
    }
}

// ═══════════════════════════════════
// DTOs de Request
// ═══════════════════════════════════

public record CreateRecetaRequest(
    Guid ProductoId,
    Guid InsumoId,
    decimal CantidadNecesaria
);

public record UpdateRecetaRequest(
    decimal CantidadNecesaria
);
