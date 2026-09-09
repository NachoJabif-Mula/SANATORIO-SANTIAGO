using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para el ABM de Métodos de Pago (Efectivo, Tarjeta, MercadoPago, etc.).
/// Los métodos de pago se sincronizan desde la Nube a las terminales locales.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetodoPagoController : ControllerBase
{
    private readonly NubeContext _context;

    public MetodoPagoController(NubeContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene todos los métodos de pago. Si includeInactive es true, devuelve todos para sincronización.
    /// Siembra datos por defecto si la tabla está vacía.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MetodoPago>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        // Asegurar semillas básicas si no hay ninguna
        if (!await _context.MetodosPago.AnyAsync(ct))
        {
            _context.MetodosPago.AddRange(
                new MetodoPago { Id = Guid.NewGuid(), Nombre = "Efectivo", ComisionPorcentaje = 0m, RequiereFacturaAfip = false, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new MetodoPago { Id = Guid.NewGuid(), Nombre = "Tarjeta Débito", ComisionPorcentaje = 1.5m, RequiereFacturaAfip = true, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new MetodoPago { Id = Guid.NewGuid(), Nombre = "Tarjeta Crédito", ComisionPorcentaje = 3.5m, RequiereFacturaAfip = true, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new MetodoPago { Id = Guid.NewGuid(), Nombre = "MercadoPago / QR", ComisionPorcentaje = 4.5m, RequiereFacturaAfip = true, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new MetodoPago { Id = Guid.NewGuid(), Nombre = "Transferencia Bancaria", ComisionPorcentaje = 0m, RequiereFacturaAfip = false, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );
            await _context.SaveChangesAsync(ct);
        }

        IQueryable<MetodoPago> query = _context.MetodosPago;

        if (!includeInactive)
        {
            query = query.Where(m => m.IsActive);
        }

        var metodos = await query.OrderBy(m => m.Nombre).ToListAsync(ct);
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
        var metodo = await _context.MetodosPago.FirstOrDefaultAsync(m => m.Id == id && m.IsActive, ct);
        if (metodo is null)
            return NotFound(new { message = $"Método de pago con ID '{id}' no encontrado." });

        return Ok(metodo);
    }

    /// <summary>
    /// Crea un nuevo método de pago.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MetodoPago), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaveMetodoPagoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });

        // Verificar unicidad del nombre
        var existe = await _context.MetodosPago.AnyAsync(m => m.Nombre == request.Nombre.Trim() && m.IsActive, ct);
        if (existe)
            return BadRequest(new { message = $"Ya existe un método de pago con el nombre '{request.Nombre}'." });

        var metodo = new MetodoPago
        {
            Nombre = request.Nombre.Trim(),
            ComisionPorcentaje = request.ComisionPorcentaje,
            RequiereFacturaAfip = request.RequiereFacturaAfip,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.MetodosPago.Add(metodo);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = metodo.Id }, metodo);
    }

    /// <summary>
    /// Actualiza un método de pago existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveMetodoPagoRequest request, CancellationToken ct)
    {
        var metodo = await _context.MetodosPago.FirstOrDefaultAsync(m => m.Id == id && m.IsActive, ct);
        if (metodo is null)
            return NotFound(new { message = $"Método de pago con ID '{id}' no encontrado." });

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre es obligatorio." });

        // Verificar unicidad del nombre (excluyendo el actual)
        var existe = await _context.MetodosPago.AnyAsync(m => m.Nombre == request.Nombre.Trim() && m.IsActive && m.Id != id, ct);
        if (existe)
            return BadRequest(new { message = $"Ya existe otro método de pago con el nombre '{request.Nombre}'." });

        metodo.Nombre = request.Nombre.Trim();
        metodo.ComisionPorcentaje = request.ComisionPorcentaje;
        metodo.RequiereFacturaAfip = request.RequiereFacturaAfip;
        metodo.UpdatedAt = DateTime.UtcNow;

        _context.MetodosPago.Update(metodo);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Desactiva un método de pago (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var metodo = await _context.MetodosPago.FirstOrDefaultAsync(m => m.Id == id && m.IsActive, ct);
        if (metodo is null)
            return NotFound(new { message = $"Método de pago con ID '{id}' no encontrado." });

        metodo.IsActive = false;
        metodo.UpdatedAt = DateTime.UtcNow;

        _context.MetodosPago.Update(metodo);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}

/// <summary>
/// DTO para crear/actualizar un método de pago.
/// </summary>
public record SaveMetodoPagoRequest(string Nombre, decimal ComisionPorcentaje, bool RequiereFacturaAfip);
