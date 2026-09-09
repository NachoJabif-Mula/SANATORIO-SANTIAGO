using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Controlador local para la consulta del catálogo de productos y categorías.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CatalogoController : ControllerBase
{
    private readonly LocalContext _context;

    public CatalogoController(LocalContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene las categorías de productos disponibles localmente en la sucursal.
    /// </summary>
    [HttpGet("categorias")]
    public async Task<IActionResult> GetCategorias(CancellationToken ct)
    {
        var dbCategorias = await _context.Categorias
            .Where(c => c.IsActive)
            .OrderBy(c => c.OrdenVisual)
            .Select(c => new {
                id = c.Id,
                nombre = c.Nombre
            })
            .ToListAsync(ct);

        var result = dbCategorias.Select(c => new {
            c.id,
            c.nombre,
            icono = c.nombre.ToLower() switch {
                "tragos" => "wine",
                "cervezas" => "beer",
                "comida" => "utensils-crossed",
                "postres" => "cake-slice",
                "sin alcohol" => "cup-soda",
                _ => "coffee"
            },
            color = c.nombre.ToLower() switch {
                "tragos" => "#f59e0b",
                "cervezas" => "#fbbf24",
                "comida" => "#22c55e",
                "postres" => "#f472b6",
                "sin alcohol" => "#06b6d4",
                _ => "#a78bfa"
            }
        });

        return Ok(result);
    }

    /// <summary>
    /// Obtiene los productos con sus respectivos precios de venta asignados a la sucursal.
    /// </summary>
    [HttpGet("productos")]
    public async Task<IActionResult> GetProductos(CancellationToken ct)
    {
        var productos = await _context.Productos
            .Where(p => p.IsActive)
            .Select(p => new {
                id = p.Id,
                nombre = p.Nombre,
                categoriaId = p.CategoriaId,
                disponible = true,
                precio = _context.ProductoPrecios
                    .Where(pr => pr.ProductoId == p.Id)
                    .Select(pr => (double)pr.PrecioVenta)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        // Fallback de precio si por alguna razón no tiene asignado precio de venta
        var result = productos.Select(p => new {
            p.id,
            p.nombre,
            p.categoriaId,
            p.disponible,
            precio = p.precio > 0 ? p.precio : 3500.0
        });

        return Ok(result);
    }

    /// <summary>
    /// Obtiene los métodos de pago disponibles localmente (sincronizados desde la Nube).
    /// </summary>
    [HttpGet("metodos-pago")]
    public async Task<IActionResult> GetMetodosPago(CancellationToken ct)
    {
        var metodos = await _context.MetodosPago
            .Where(m => m.IsActive)
            .OrderBy(m => m.Nombre)
            .Select(m => new {
                id = m.Id,
                nombre = m.Nombre,
                comisionPorcentaje = m.ComisionPorcentaje,
                requiereFacturaAfip = m.RequiereFacturaAfip,
                esCuentaCorriente = m.EsCuentaCorriente
            })
            .ToListAsync(ct);

        return Ok(metodos);
    }

    /// <summary>
    /// Obtiene los tipos de venta disponibles localmente (sincronizados desde la Nube).
    /// </summary>
    [HttpGet("tipos-venta")]
    public async Task<IActionResult> GetTiposVenta(CancellationToken ct)
    {
        var tipos = await _context.TiposVenta
            .Where(t => t.IsActive)
            .Select(t => new {
                id = t.Id,
                nombre = t.Nombre,
                aplicaRecargo = t.AplicaRecargo
            })
            .ToListAsync(ct);

        return Ok(tipos);
    }

    /// <summary>
    /// Obtiene las mesas de la sucursal actual con su estado de ocupación.
    /// </summary>
    [HttpGet("mesas")]
    public async Task<IActionResult> GetMesas(CancellationToken ct)
    {
        var mesas = await _context.Mesas
            .Where(m => m.IsActive)
            .OrderBy(m => m.Etiqueta)
            .Select(m => new {
                id = m.Id,
                etiqueta = m.Etiqueta,
                capacidad = m.Capacidad,
                ocupada = m.Ocupada
            })
            .ToListAsync(ct);

        return Ok(mesas);
    }
}
