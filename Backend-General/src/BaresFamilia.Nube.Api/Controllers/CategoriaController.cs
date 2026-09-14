using BaresFamilia.Core.Models.Contratos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para el ABM y sincronización de Categorías.
/// Flujo: CategoriaController → ICategoriaService → CategoriaService → ICategoriaRepository → CategoriaRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriaController : ControllerBase
{
    private readonly ICategoriaService _categoriaService;
    private readonly IMonitorSincronizacion _monitorSincronizacion;

    public CategoriaController(ICategoriaService categoriaService, IMonitorSincronizacion monitorSincronizacion)
    {
        _categoriaService = categoriaService;
        _monitorSincronizacion = monitorSincronizacion;
    }

    /// <summary>
    /// Obtiene las categorías de una sucursal. Un usuario no-global (o un token M2M
    /// de POS) solo ve las de su propia sucursal, sin importar lo que pida por query;
    /// un usuario global puede pedir una sucursal puntual o, si no especifica ninguna,
    /// ve el catálogo de todas (vista consolidada). Si includeInactive es true, incluye
    /// también las desactivadas (usado por la sincronización).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Categoria>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, [FromQuery] Guid? sucursalId = null, CancellationToken ct = default)
    {
        var sucId = User.IsGlobal() ? sucursalId : User.GetSucursalId();

        if (sucId.HasValue)
        {
            _monitorSincronizacion.RegistrarPull(sucId.Value, TipoPull.Config); // Marcamos actividad de pull para la sucursal
        }

        var categorias = await _categoriaService.GetPorSucursalAsync(sucId, includeInactive, ct);
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
        var categoria = await _categoriaService.GetByIdAsync(id, ct);
        if (categoria is null)
            return NotFound(new { message = $"Categoría con ID '{id}' no encontrada." });

        if (!PuedeAcceder(categoria))
            return Forbid();

        return Ok(categoria);
    }

    /// <summary>
    /// Crea una nueva categoría en una sucursal. Un usuario no-global siempre crea
    /// en su propia sucursal, sin importar lo que envíe en el request.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(typeof(Categoria), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCategoriaRequest request, CancellationToken ct)
    {
        var sucursalId = request.SucursalId;
        if (!User.IsGlobal())
        {
            var propiaSucursal = User.GetSucursalId();
            if (propiaSucursal is null)
                return Forbid();
            sucursalId = propiaSucursal.Value;
        }

        var categoria = await _categoriaService.CrearAsync(new Categoria
        {
            SucursalId = sucursalId,
            Nombre = request.Nombre,
            OrdenVisual = request.OrdenVisual
        }, ct);

        return CreatedAtAction(nameof(GetById), new { id = categoria.Id }, categoria);
    }

    /// <summary>
    /// Actualiza una categoría existente. La sucursal dueña no se puede reasignar
    /// desde acá (si hace falta mover un producto/categoría de sucursal, se recrea).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoriaRequest request, CancellationToken ct)
    {
        var categoria = await _categoriaService.GetByIdAsync(id, ct);
        if (categoria is null)
            return NotFound(new { message = $"Categoría con ID '{id}' no encontrada." });

        if (!PuedeAcceder(categoria))
            return Forbid();

        categoria.Nombre = request.Nombre;
        categoria.OrdenVisual = request.OrdenVisual;

        await _categoriaService.ActualizarAsync(categoria, ct);
        return NoContent();
    }

    /// <summary>
    /// Desactiva una categoría (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var categoria = await _categoriaService.GetByIdAsync(id, ct);
        if (categoria is null)
            return NotFound(new { message = $"Categoría con ID '{id}' no encontrada." });

        if (!PuedeAcceder(categoria))
            return Forbid();

        await _categoriaService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Un usuario global ve cualquier sucursal; el resto solo la propia.
    /// </summary>
    private bool PuedeAcceder(Categoria categoria)
        => User.IsGlobal() || categoria.SucursalId == User.GetSucursalId();
}
