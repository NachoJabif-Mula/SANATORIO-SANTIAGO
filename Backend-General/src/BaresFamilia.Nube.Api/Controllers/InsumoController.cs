using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// ABM (CRUD) completo para la entidad Insumo.
/// Flujo: InsumoController → IInsumoService → InsumoService → IInsumoRepository → InsumoRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InsumoController : ControllerBase
{
    private readonly IInsumoService _insumoService;

    public InsumoController(IInsumoService insumoService)
    {
        _insumoService = insumoService;
    }

    /// <summary>
    /// Obtiene todos los insumos activos.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Insumo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var insumos = await _insumoService.GetAllAsync(ct);
        return Ok(insumos);
    }

    /// <summary>
    /// Obtiene un insumo por su ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Insumo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var insumo = await _insumoService.GetByIdAsync(id, ct);
        if (insumo is null)
            return NotFound(new { message = $"Insumo con ID '{id}' no encontrado." });

        return Ok(insumo);
    }

    /// <summary>
    /// Obtiene insumos con stock por debajo del mínimo en una sucursal.
    /// </summary>
    [HttpGet("stock-bajo/{sucursalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<Insumo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConStockBajo(Guid sucursalId, CancellationToken ct)
    {
        var insumos = await _insumoService.GetConStockBajoAsync(sucursalId, ct);
        return Ok(insumos);
    }

    /// <summary>
    /// Crea un nuevo insumo.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Insumo), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateInsumoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre del insumo es obligatorio." });

        if (string.IsNullOrWhiteSpace(request.UnidadMedida))
            return BadRequest(new { message = "La unidad de medida es obligatoria." });

        var insumo = new Insumo
        {
            Nombre = request.Nombre.Trim(),
            UnidadMedida = request.UnidadMedida.Trim(),
            StockMinimo = request.StockMinimo
        };

        var created = await _insumoService.CreateAsync(insumo, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Actualiza un insumo existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInsumoRequest request, CancellationToken ct)
    {
        var insumo = await _insumoService.GetByIdAsync(id, ct);
        if (insumo is null)
            return NotFound(new { message = $"Insumo con ID '{id}' no encontrado." });

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre del insumo es obligatorio." });

        insumo.Nombre = request.Nombre.Trim();
        insumo.UnidadMedida = request.UnidadMedida?.Trim() ?? insumo.UnidadMedida;
        insumo.StockMinimo = request.StockMinimo;

        await _insumoService.UpdateAsync(insumo, ct);
        return NoContent();
    }

    /// <summary>
    /// Elimina un insumo (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var exists = await _insumoService.ExistsAsync(id, ct);
        if (!exists)
            return NotFound(new { message = $"Insumo con ID '{id}' no encontrado." });

        await _insumoService.DeleteAsync(id, ct);
        return NoContent();
    }
}

// ═══════════════════════════════════
// DTOs de Request
// ═══════════════════════════════════

public record CreateInsumoRequest(
    string Nombre,
    string UnidadMedida,
    decimal StockMinimo
);

public record UpdateInsumoRequest(
    string Nombre,
    string? UnidadMedida,
    decimal StockMinimo
);
