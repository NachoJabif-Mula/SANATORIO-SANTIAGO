using BaresFamilia.Core.Models.Contratos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para la gestión de sucursales.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SucursalController : ControllerBase
{
    private readonly IService<Sucursal> _sucursalService;

    public SucursalController(IService<Sucursal> sucursalService)
    {
        _sucursalService = sucursalService;
    }

    /// <summary>
    /// Obtiene todas las sucursales activas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Sucursal>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var sucursales = await _sucursalService.GetAllAsync(ct);
        return Ok(sucursales.Where(s => s.IsActive));
    }

    /// <summary>
    /// Crea una nueva sucursal.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Backoffice")]
    [ProducesResponseType(typeof(Sucursal), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateSucursalRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre de la sucursal es obligatorio." });

        var sucursal = new Sucursal
        {
            Nombre = request.Nombre.Trim(),
            Direccion = request.Direccion?.Trim() ?? string.Empty,
            Cuit = request.Cuit?.Trim(),
            RazonSocial = request.RazonSocial?.Trim(),
            DomicilioFiscal = request.DomicilioFiscal?.Trim(),
            CondicionIva = request.CondicionIva,
            PuntoDeVenta = request.PuntoDeVenta ?? 1,
            NumeroIIBB = request.NumeroIIBB?.Trim(),
            FechaInicioActividades = request.FechaInicioActividades,
            ImpresionSimulada = request.ImpresionSimulada ?? false,
            IsActive = true
        };

        var created = await _sucursalService.CreateAsync(sucursal, ct);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    /// <summary>
    /// Actualiza una sucursal existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSucursalRequest request, CancellationToken ct)
    {
        var sucursal = await _sucursalService.GetByIdAsync(id, ct);
        if (sucursal == null)
            return NotFound(new { message = "Sucursal no encontrada." });

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre de la sucursal es obligatorio." });

        sucursal.Nombre = request.Nombre.Trim();
        sucursal.Direccion = request.Direccion?.Trim() ?? string.Empty;
        sucursal.Cuit = request.Cuit?.Trim();
        sucursal.RazonSocial = request.RazonSocial?.Trim();
        sucursal.DomicilioFiscal = request.DomicilioFiscal?.Trim();
        sucursal.CondicionIva = request.CondicionIva;
        sucursal.PuntoDeVenta = request.PuntoDeVenta ?? sucursal.PuntoDeVenta;
        sucursal.NumeroIIBB = request.NumeroIIBB?.Trim();
        sucursal.FechaInicioActividades = request.FechaInicioActividades;
        sucursal.ImpresionSimulada = request.ImpresionSimulada ?? sucursal.ImpresionSimulada;
        sucursal.UpdatedAt = DateTime.UtcNow;

        await _sucursalService.UpdateAsync(sucursal, ct);
        return NoContent();
    }

    /// <summary>
    /// Elimina una sucursal (desactivación lógica).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Backoffice")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var sucursal = await _sucursalService.GetByIdAsync(id, ct);
        if (sucursal == null)
            return NotFound(new { message = "Sucursal no encontrada." });

        sucursal.IsActive = false;
        sucursal.UpdatedAt = DateTime.UtcNow;

        await _sucursalService.UpdateAsync(sucursal, ct);
        return NoContent();
    }
}
