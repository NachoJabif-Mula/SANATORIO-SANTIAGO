using BaresFamilia.Core.Models.Contratos.Catalogos;
using BaresFamilia.Core.Models.Dtos.Catalogos;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para gestionar los precios de los productos segmentados por sucursal y tipo de venta.
/// Flujo: ProductoPrecioController → IProductoPrecioService → ProductoPrecioService → IProductoPrecioRepository → ProductoPrecioRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductoPrecioController : ControllerBase
{
    private readonly IProductoPrecioService _productoPrecioService;
    private readonly IProductoService _productoService;
    private readonly IMonitorSincronizacion _monitorSincronizacion;

    public ProductoPrecioController(
        IProductoPrecioService productoPrecioService,
        IProductoService productoService,
        IMonitorSincronizacion monitorSincronizacion)
    {
        _productoPrecioService = productoPrecioService;
        _productoService = productoService;
        _monitorSincronizacion = monitorSincronizacion;
    }

    /// <summary>
    /// Obtiene todos los precios configurados para un producto (siempre de su propia
    /// sucursal: un producto vive en una sola sucursal, y su precio también).
    /// </summary>
    [HttpGet("producto/{productoId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<ProductoPrecioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProducto(Guid productoId, CancellationToken ct)
    {
        var producto = await _productoService.GetPorIdIncluyendoInactivosAsync(productoId, ct);
        if (producto is null)
            return NotFound(new { message = $"Producto con ID '{productoId}' no encontrado." });

        if (!User.IsGlobal() && producto.SucursalId != User.GetSucursalId())
            return Forbid();

        var precios = await _productoPrecioService.GetActivosPorProductoAsync(productoId, ct);
        return Ok(precios.Select(ProductoPrecioDto.Desde));
    }

    /// <summary>
    /// Guarda o actualiza en bloque los precios de un producto (upsert en lote), uno
    /// por tipo de venta. La sucursal del precio nunca se pide en el request: siempre
    /// es la del producto (un producto vive en una sola sucursal, y su precio también).
    /// Si un precio previo no está en el lote enviado, se desactiva (borrado lógico).
    /// </summary>
    [HttpPut("producto/{productoId:guid}")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePreciosBatch(Guid productoId, [FromBody] List<UpdateProductoPrecioRequest> request, CancellationToken ct)
    {
        var producto = await _productoService.GetByIdAsync(productoId, ct);
        if (producto is null)
            return BadRequest(new { message = $"El producto con ID '{productoId}' no existe o está inactivo." });

        // Un usuario no-global solo puede tocar precios de productos de su propia sucursal
        if (!User.IsGlobal() && producto.SucursalId != User.GetSucursalId())
            return Forbid();

        var precios = request.Select(r => new PrecioPorTipoVenta(r.TipoVentaId, r.PrecioVenta));
        await _productoPrecioService.ReemplazarPreciosDeProductoAsync(productoId, producto.SucursalId, precios, ct);

        return NoContent();
    }

    /// <summary>
    /// Endpoint especial para la sincronización del POS local.
    /// Devuelve todos los precios (activos e inactivos) configurados para una sucursal específica.
    /// </summary>
    [HttpGet("sucursal/{sucursalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<ProductoPrecioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPorSucursal(Guid sucursalId, CancellationToken ct)
    {
        _monitorSincronizacion.RegistrarPull(sucursalId, TipoPull.Config);

        var precios = await _productoPrecioService.GetTodosPorSucursalAsync(sucursalId, ct);
        return Ok(precios.Select(ProductoPrecioDto.Desde));
    }
}
