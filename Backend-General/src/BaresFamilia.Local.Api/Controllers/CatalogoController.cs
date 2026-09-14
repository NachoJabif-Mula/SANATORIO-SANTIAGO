using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Controlador local para la consulta del catálogo de productos y categorías.
/// Flujo: CatalogoController → ICatalogoPosService → CatalogoPosService → I*Repository → *Repository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CatalogoController : ControllerBase
{
    /// <summary>Precio de referencia si un producto llega sin precio configurado.</summary>
    private const double PrecioPorDefecto = 3500.0;

    private readonly ICatalogoPosService _catalogoService;

    public CatalogoController(ICatalogoPosService catalogoService)
    {
        _catalogoService = catalogoService;
    }

    /// <summary>
    /// Obtiene las categorías de productos disponibles localmente en la sucursal.
    /// </summary>
    [HttpGet("categorias")]
    public async Task<IActionResult> GetCategorias(CancellationToken ct)
    {
        var categorias = await _catalogoService.GetCategoriasActivasAsync(ct);

        return Ok(categorias.Select(c => new
        {
            id = c.Id,
            nombre = c.Nombre,
            icono = IconoDeCategoria(c.Nombre),
            color = ColorDeCategoria(c.Nombre)
        }));
    }

    /// <summary>
    /// Obtiene los productos con sus respectivos precios de venta asignados a la sucursal.
    /// </summary>
    [HttpGet("productos")]
    public async Task<IActionResult> GetProductos(CancellationToken ct)
    {
        var productos = await _catalogoService.GetProductosDisponiblesAsync(ct);

        return Ok(productos.Select(p => new
        {
            id = p.Id,
            nombre = p.Nombre,
            categoriaId = p.CategoriaId,
            disponible = true,
            precio = p.Precio > 0 ? (double)p.Precio : PrecioPorDefecto
        }));
    }

    /// <summary>
    /// Obtiene los métodos de pago disponibles localmente (sincronizados desde la Nube).
    /// </summary>
    [HttpGet("metodos-pago")]
    public async Task<IActionResult> GetMetodosPago(CancellationToken ct)
    {
        var metodos = await _catalogoService.GetMetodosPagoActivosAsync(ct);

        return Ok(metodos.Select(m => new
        {
            id = m.Id,
            nombre = m.Nombre,
            comisionPorcentaje = m.ComisionPorcentaje,
            requiereFacturaAfip = m.RequiereFacturaAfip,
            esCuentaCorriente = m.EsCuentaCorriente
        }));
    }

    /// <summary>
    /// Obtiene los tipos de venta disponibles localmente (sincronizados desde la Nube).
    /// </summary>
    [HttpGet("tipos-venta")]
    public async Task<IActionResult> GetTiposVenta(CancellationToken ct)
    {
        var tipos = await _catalogoService.GetTiposVentaActivosAsync(ct);

        return Ok(tipos.Select(t => new
        {
            id = t.Id,
            nombre = t.Nombre,
            aplicaRecargo = t.AplicaRecargo
        }));
    }

    /// <summary>
    /// Obtiene las mesas de la sucursal actual con su estado de ocupación.
    /// </summary>
    [HttpGet("mesas")]
    public async Task<IActionResult> GetMesas(CancellationToken ct)
    {
        var mesas = await _catalogoService.GetMesasActivasAsync(ct);

        return Ok(mesas.Select(m => new
        {
            id = m.Id,
            etiqueta = m.Etiqueta,
            capacidad = m.Capacidad,
            ocupada = m.Ocupada
        }));
    }

    /// <summary>
    /// Ícono que el POS muestra para cada familia de productos.
    /// </summary>
    private static string IconoDeCategoria(string nombre) => nombre.ToLower() switch
    {
        "tragos" => "wine",
        "cervezas" => "beer",
        "comida" => "utensils-crossed",
        "postres" => "cake-slice",
        "sin alcohol" => "cup-soda",
        _ => "coffee"
    };

    /// <summary>
    /// Color de la tarjeta de categoría en la grilla táctil del POS.
    /// </summary>
    private static string ColorDeCategoria(string nombre) => nombre.ToLower() switch
    {
        "tragos" => "#f59e0b",
        "cervezas" => "#fbbf24",
        "comida" => "#22c55e",
        "postres" => "#f472b6",
        "sin alcohol" => "#06b6d4",
        _ => "#a78bfa"
    };
}
