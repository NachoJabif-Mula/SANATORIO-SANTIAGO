using BaresFamilia.Core.Models.Contratos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly IMonitorSincronizacion _monitorSincronizacion;

    public ProductoController(IProductoService productoService, IMonitorSincronizacion monitorSincronizacion)
    {
        _productoService = productoService;
        _monitorSincronizacion = monitorSincronizacion;
    }

    /// <summary>
    /// Obtiene los productos de una sucursal. Un usuario no-global (o un token M2M de
    /// POS) solo ve los de su propia sucursal, sin importar lo que pida por query; un
    /// usuario global puede pedir una sucursal puntual o, si no especifica ninguna, ve
    /// el catálogo de todas (vista consolidada). Si includeInactive es true, incluye
    /// también los desactivados (usado por la sincronización).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Producto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, [FromQuery] Guid? sucursalId = null, CancellationToken ct = default)
    {
        var sucId = User.IsGlobal() ? sucursalId : User.GetSucursalId();

        if (sucId.HasValue)
        {
            _monitorSincronizacion.RegistrarPull(sucId.Value, TipoPull.Config);
        }

        var productos = await _productoService.GetPorSucursalAsync(sucId, includeInactive, ct);
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

        if (!User.IsGlobal() && producto.SucursalId != User.GetSucursalId())
            return Forbid();

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
    /// Crea un nuevo producto en una sucursal. Un usuario no-global siempre crea en
    /// su propia sucursal, sin importar lo que envíe en el request. La categoría
    /// indicada debe pertenecer a esa misma sucursal.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(typeof(Producto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProductoRequest request, CancellationToken ct)
    {
        var sucursalId = request.SucursalId;
        if (!User.IsGlobal())
        {
            var propiaSucursal = User.GetSucursalId();
            if (propiaSucursal is null)
                return Forbid();
            sucursalId = propiaSucursal.Value;
        }

        var created = await _productoService.CrearAsync(new Producto
        {
            SucursalId = sucursalId,
            CategoriaId = request.CategoriaId,
            Nombre = request.Nombre,
            ColorUi = request.ColorUi ?? "#FFFFFF",
            RequiereCocina = request.RequiereCocina,
            AlicuotaIva = request.AlicuotaIva
        }, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Actualiza un producto existente. La sucursal dueña no se puede reasignar desde
    /// acá; la nueva categoría debe seguir perteneciendo a esa misma sucursal.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductoRequest request, CancellationToken ct)
    {
        var producto = await _productoService.GetByIdAsync(id, ct);
        if (producto is null)
            return NotFound(new { message = $"Producto con ID '{id}' no encontrado." });

        if (!User.IsGlobal() && producto.SucursalId != User.GetSucursalId())
            return Forbid();

        producto.CategoriaId = request.CategoriaId;
        producto.Nombre = request.Nombre;
        producto.ColorUi = request.ColorUi ?? producto.ColorUi;
        producto.RequiereCocina = request.RequiereCocina;
        producto.AlicuotaIva = request.AlicuotaIva;

        await _productoService.ActualizarAsync(producto, ct);
        return NoContent();
    }

    /// <summary>
    /// Elimina un producto (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var producto = await _productoService.GetByIdAsync(id, ct);
        if (producto is null)
            return NotFound(new { message = $"Producto con ID '{id}' no encontrado." });

        if (!User.IsGlobal() && producto.SucursalId != User.GetSucursalId())
            return Forbid();

        await _productoService.DeleteAsync(id, ct);
        return NoContent();
    }
}
