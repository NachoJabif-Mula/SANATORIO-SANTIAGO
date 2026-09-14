using BaresFamilia.Core.Models.Contratos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para el ABM y sincronización de Tipos de Venta.
/// Flujo: TipoVentaController → ITipoVentaService → TipoVentaService → ITipoVentaRepository → TipoVentaRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TipoVentaController : ControllerBase
{
    private readonly ITipoVentaService _tipoVentaService;

    public TipoVentaController(ITipoVentaService tipoVentaService)
    {
        _tipoVentaService = tipoVentaService;
    }

    /// <summary>
    /// Obtiene todos los tipos de venta. Si includeInactive es true, devuelve todos (activos e inactivos) para sincronización.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TipoVenta>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var tipos = await _tipoVentaService.GetOrdenadosPorNombreAsync(includeInactive, ct);
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
        var tipo = await _tipoVentaService.GetByIdAsync(id, ct);
        if (tipo is null)
            return NotFound(new { message = $"Tipo de venta con ID '{id}' no encontrado." });

        return Ok(tipo);
    }

    /// <summary>
    /// Crea un nuevo tipo de venta.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(typeof(TipoVenta), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTipoVentaRequest request, CancellationToken ct)
    {
        var tipo = await _tipoVentaService.CrearAsync(new TipoVenta
        {
            Nombre = request.Nombre,
            AplicaRecargo = request.AplicaRecargo
        }, ct);

        return CreatedAtAction(nameof(GetById), new { id = tipo.Id }, tipo);
    }

    /// <summary>
    /// Actualiza un tipo de venta existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTipoVentaRequest request, CancellationToken ct)
    {
        var tipo = await _tipoVentaService.GetByIdAsync(id, ct);
        if (tipo is null)
            return NotFound(new { message = $"Tipo de venta con ID '{id}' no encontrado." });

        tipo.Nombre = request.Nombre;
        tipo.AplicaRecargo = request.AplicaRecargo;

        await _tipoVentaService.ActualizarAsync(tipo, ct);
        return NoContent();
    }

    /// <summary>
    /// Desactiva un tipo de venta (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await _tipoVentaService.ExistsAsync(id, ct))
            return NotFound(new { message = $"Tipo de venta con ID '{id}' no encontrado." });

        await _tipoVentaService.DeleteAsync(id, ct);
        return NoContent();
    }
}
