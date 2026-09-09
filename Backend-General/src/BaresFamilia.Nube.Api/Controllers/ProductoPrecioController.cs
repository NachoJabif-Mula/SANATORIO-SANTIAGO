using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para gestionar los precios de los productos segmentados por sucursal y tipo de venta.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductoPrecioController : ControllerBase
{
    private readonly NubeContext _context;

    public ProductoPrecioController(NubeContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene todos los precios configurados para un producto.
    /// </summary>
    [HttpGet("producto/{productoId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<ProductoPrecioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProducto(Guid productoId, CancellationToken ct)
    {
        var precios = await _context.ProductoPrecios
            .Where(p => p.ProductoId == productoId && p.IsActive)
            .Select(p => new ProductoPrecioDto(
                p.Id,
                p.ProductoId,
                p.SucursalId,
                p.TipoVentaId,
                p.PrecioVenta,
                p.IsActive
            ))
            .ToListAsync(ct);

        return Ok(precios);
    }

    /// <summary>
    /// Guarda o actualiza en bloque los precios de un producto (upsert en lote).
    /// Si un precio previo no está en el lote enviado, se desactiva (borrado lógico).
    /// </summary>
    [HttpPut("producto/{productoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePreciosBatch(Guid productoId, [FromBody] List<UpdateProductoPrecioRequest> request, CancellationToken ct)
    {
        // Un usuario no-global solo puede tocar precios de su propia sucursal
        if (!User.IsGlobal())
        {
            var propiaSucursal = User.GetSucursalId();
            if (propiaSucursal is null || request.Any(r => r.SucursalId != propiaSucursal.Value))
                return Forbid();
        }

        // Validar que el producto exista
        var productoExiste = await _context.Productos.AnyAsync(p => p.Id == productoId && p.IsActive, ct);
        if (!productoExiste)
            return BadRequest(new { message = $"El producto con ID '{productoId}' no existe o está inactivo." });

        // Obtener todos los precios existentes (activos e inactivos) del producto
        var preciosExistentes = await _context.ProductoPrecios
            .Where(p => p.ProductoId == productoId)
            .ToListAsync(ct);

        // Crear mapa para búsqueda rápida
        var mapaExistente = preciosExistentes
            .ToDictionary(p => (p.SucursalId, p.TipoVentaId));

        var idsProcesados = new HashSet<Guid>();

        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var req in request)
            {
                // Validar que la sucursal y tipo de venta existan
                var sucursalExiste = await _context.Sucursales.AnyAsync(s => s.Id == req.SucursalId, ct);
                var tipoVentaExiste = await _context.TiposVenta.AnyAsync(t => t.Id == req.TipoVentaId, ct);

                if (!sucursalExiste || !tipoVentaExiste)
                    continue; // Saltar entradas inválidas para mantener integridad

                var clave = (req.SucursalId, req.TipoVentaId);
                if (mapaExistente.TryGetValue(clave, out var precioDb))
                {
                    // Actualizar precio existente
                    precioDb.PrecioVenta = req.PrecioVenta;
                    precioDb.IsActive = true;
                    precioDb.UpdatedAt = DateTime.UtcNow;
                    _context.ProductoPrecios.Update(precioDb);
                    idsProcesados.Add(precioDb.Id);
                }
                else
                {
                    // Crear nuevo precio
                    var nuevoPrecio = new ProductoPrecio
                    {
                        Id = Guid.NewGuid(),
                        ProductoId = productoId,
                        SucursalId = req.SucursalId,
                        TipoVentaId = req.TipoVentaId,
                        PrecioVenta = req.PrecioVenta,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ProductoPrecios.Add(nuevoPrecio);
                    idsProcesados.Add(nuevoPrecio.Id);
                }
            }

            // Desactivar precios que no se incluyeron en la lista request, pero solo
            // dentro de las sucursales realmente enviadas en este guardado (para no
            // afectar precios de otras sucursales que el caller no está gestionando).
            var sucursalesEnRequest = request.Select(r => r.SucursalId).ToHashSet();
            foreach (var precioDb in preciosExistentes)
            {
                if (!idsProcesados.Contains(precioDb.Id) && precioDb.IsActive && sucursalesEnRequest.Contains(precioDb.SucursalId))
                {
                    precioDb.IsActive = false;
                    precioDb.UpdatedAt = DateTime.UtcNow;
                    _context.ProductoPrecios.Update(precioDb);
                }
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

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
        SyncManagerStore.RecordPull(sucursalId, "config");

        var precios = await _context.ProductoPrecios
            .Where(p => p.SucursalId == sucursalId)
            .Select(p => new ProductoPrecioDto(
                p.Id,
                p.ProductoId,
                p.SucursalId,
                p.TipoVentaId,
                p.PrecioVenta,
                p.IsActive
            ))
            .ToListAsync(ct);

        return Ok(precios);
    }
}

public record ProductoPrecioDto(
    Guid Id,
    Guid ProductoId,
    Guid SucursalId,
    Guid TipoVentaId,
    decimal PrecioVenta,
    bool IsActive
);

public record UpdateProductoPrecioRequest(
    Guid SucursalId,
    Guid TipoVentaId,
    decimal PrecioVenta
);
