using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// ABM (CRUD) completo para la entidad Producto.
/// Flujo: ProductoController → IProductoService → ProductoService → IProductoRepository → ProductoRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductoController : ControllerBase
{
    private readonly IProductoService _productoService;
    private readonly NubeContext _context;

    public ProductoController(IProductoService productoService, NubeContext context)
    {
        _productoService = productoService;
        _context = context;
    }

    /// <summary>
    /// Obtiene todos los productos. Si includeInactive es true, devuelve todos (activos e inactivos) para sincronización.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Producto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var sucursalIdClaim = User.FindFirst("sucursal_id")?.Value;
        if (!string.IsNullOrEmpty(sucursalIdClaim) && Guid.TryParse(sucursalIdClaim, out var sucursalId))
        {
            SyncManagerStore.RecordPull(sucursalId, "config");
        }

        if (includeInactive)
        {
            var todos = await _context.Productos.OrderBy(p => p.Nombre).ToListAsync(ct);
            return Ok(todos);
        }

        var productos = await _productoService.GetAllAsync(ct);
        return Ok(productos);
    }

    /// <summary>
    /// Obtiene un producto por su ID con detalles (categoría + precios).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Producto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var producto = await _productoService.GetWithDetailsAsync(id, ct);
        if (producto is null)
            return NotFound(new { message = $"Producto con ID '{id}' no encontrado." });

        return Ok(producto);
    }

    /// <summary>
    /// Obtiene productos filtrados por categoría.
    /// </summary>
    [HttpGet("por-categoria/{categoriaId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<Producto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCategoria(Guid categoriaId, CancellationToken ct)
    {
        var productos = await _productoService.GetByCategoriaAsync(categoriaId, ct);
        return Ok(productos);
    }

    /// <summary>
    /// Crea un nuevo producto.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Producto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProductoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre del producto es obligatorio." });

        var producto = new Producto
        {
            CategoriaId = request.CategoriaId,
            Nombre = request.Nombre.Trim(),
            ColorUi = request.ColorUi ?? "#FFFFFF",
            RequiereCocina = request.RequiereCocina,
            AlicuotaIva = request.AlicuotaIva
        };

        var created = await _productoService.CreateAsync(producto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Actualiza un producto existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductoRequest request, CancellationToken ct)
    {
        var producto = await _productoService.GetByIdAsync(id, ct);
        if (producto is null)
            return NotFound(new { message = $"Producto con ID '{id}' no encontrado." });

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre del producto es obligatorio." });

        producto.CategoriaId = request.CategoriaId;
        producto.Nombre = request.Nombre.Trim();
        producto.ColorUi = request.ColorUi ?? producto.ColorUi;
        producto.RequiereCocina = request.RequiereCocina;
        producto.AlicuotaIva = request.AlicuotaIva;

        await _productoService.UpdateAsync(producto, ct);
        return NoContent();
    }

    /// <summary>
    /// Elimina un producto (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var exists = await _productoService.ExistsAsync(id, ct);
        if (!exists)
            return NotFound(new { message = $"Producto con ID '{id}' no encontrado." });

        await _productoService.DeleteAsync(id, ct);
        return NoContent();
    }
}

// ═══════════════════════════════════
// DTOs de Request (en el mismo archivo por simplicidad inicial)
// ═══════════════════════════════════

/// <summary>
/// DTO para creación de Producto.
/// </summary>
public record CreateProductoRequest(
    Guid CategoriaId,
    string Nombre,
    string? ColorUi,
    bool RequiereCocina,
    AlicuotaIva AlicuotaIva = AlicuotaIva.Iva21
);

/// <summary>
/// DTO para actualización de Producto.
/// </summary>
public record UpdateProductoRequest(
    Guid CategoriaId,
    string Nombre,
    string? ColorUi,
    bool RequiereCocina,
    AlicuotaIva AlicuotaIva = AlicuotaIva.Iva21
);
