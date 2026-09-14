using BaresFamilia.Core.Models.Contratos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para el ABM de Métodos de Pago (Efectivo, Tarjeta, MercadoPago, etc.).
/// Los métodos de pago se sincronizan desde la Nube a las terminales locales.
/// Flujo: MetodoPagoController → IMetodoPagoService → MetodoPagoService → IMetodoPagoRepository → MetodoPagoRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetodoPagoController : ControllerBase
{
    private readonly IMetodoPagoService _metodoPagoService;

    public MetodoPagoController(IMetodoPagoService metodoPagoService)
    {
        _metodoPagoService = metodoPagoService;
    }

    /// <summary>
    /// Obtiene todos los métodos de pago. Si includeInactive es true, devuelve todos para sincronización.
    /// Siembra datos por defecto si la tabla está vacía.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MetodoPago>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var metodos = await _metodoPagoService.GetOrdenadosPorNombreAsync(includeInactive, ct);
        return Ok(metodos);
    }

    /// <summary>
    /// Obtiene un método de pago por ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MetodoPago), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var metodo = await _metodoPagoService.GetByIdAsync(id, ct);
        if (metodo is null)
            return NotFound(new { message = $"Método de pago con ID '{id}' no encontrado." });

        return Ok(metodo);
    }

    /// <summary>
    /// Crea un nuevo método de pago.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(typeof(MetodoPago), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaveMetodoPagoRequest request, CancellationToken ct)
    {
        var metodo = await _metodoPagoService.CrearAsync(new MetodoPago
        {
            Nombre = request.Nombre,
            ComisionPorcentaje = request.ComisionPorcentaje,
            RequiereFacturaAfip = request.RequiereFacturaAfip
        }, ct);

        return CreatedAtAction(nameof(GetById), new { id = metodo.Id }, metodo);
    }

    /// <summary>
    /// Actualiza un método de pago existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveMetodoPagoRequest request, CancellationToken ct)
    {
        var metodo = await _metodoPagoService.GetByIdAsync(id, ct);
        if (metodo is null)
            return NotFound(new { message = $"Método de pago con ID '{id}' no encontrado." });

        metodo.Nombre = request.Nombre;
        metodo.ComisionPorcentaje = request.ComisionPorcentaje;
        metodo.RequiereFacturaAfip = request.RequiereFacturaAfip;

        await _metodoPagoService.ActualizarAsync(metodo, ct);
        return NoContent();
    }

    /// <summary>
    /// Desactiva un método de pago (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await _metodoPagoService.ExistsAsync(id, ct))
            return NotFound(new { message = $"Método de pago con ID '{id}' no encontrado." });

        await _metodoPagoService.DeleteAsync(id, ct);
        return NoContent();
    }
}
