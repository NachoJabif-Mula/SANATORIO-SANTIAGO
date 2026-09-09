using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// ABM (CRUD) completo para la entidad Cliente.
/// Incluye gestión de cuenta corriente.
/// Flujo: ClienteController → IClienteService → ClienteService → IClienteRepository → ClienteRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClienteController : ControllerBase
{
    private readonly IClienteService _clienteService;
    private readonly IService<CuentaCorriente> _cuentaCorrienteService;

    public ClienteController(IClienteService clienteService, IService<CuentaCorriente> cuentaCorrienteService)
    {
        _clienteService = clienteService;
        _cuentaCorrienteService = cuentaCorrienteService;
    }

    /// <summary>
    /// Obtiene todos los clientes activos con saldoActual incluido (para sincronización y reporte).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var clientes = await _clienteService.GetAllAsync(ct);
        var result = new List<object>();
        foreach (var c in clientes)
        {
            var conCuenta = await _clienteService.GetWithCuentaCorrienteAsync(c.Id, ct);
            result.Add(new {
                id = c.Id,
                nombre = c.Nombre,
                apellido = c.Apellido,
                telefono = c.Telefono,
                email = c.Email,
                limiteCredito = c.LimiteCredito,
                saldoActual = conCuenta?.CuentaCorriente?.SaldoActual ?? 0m,
                isActive = c.IsActive
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un cliente por su ID con su cuenta corriente.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var cliente = await _clienteService.GetWithCuentaCorrienteAsync(id, ct);
        if (cliente is null)
            return NotFound(new { message = $"Cliente con ID '{id}' no encontrado." });

        return Ok(new {
            id = cliente.Id,
            nombre = cliente.Nombre,
            apellido = cliente.Apellido,
            telefono = cliente.Telefono,
            email = cliente.Email,
            limiteCredito = cliente.LimiteCredito,
            saldoActual = cliente.CuentaCorriente?.SaldoActual ?? 0m,
            isActive = cliente.IsActive
        });
    }

    /// <summary>
    /// Busca clientes por nombre o apellido (búsqueda parcial).
    /// </summary>
    [HttpGet("buscar")]
    public async Task<IActionResult> BuscarPorNombre([FromQuery] string nombre, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return Ok(Array.Empty<object>());

        var clientes = await _clienteService.BuscarPorNombreAsync(nombre.Trim(), ct);
        var result = clientes.Select(c => new {
            id = c.Id,
            nombre = c.Nombre,
            apellido = c.Apellido,
            telefono = c.Telefono,
            email = c.Email,
            limiteCredito = c.LimiteCredito,
            saldoActual = c.CuentaCorriente?.SaldoActual ?? 0m,
            isActive = c.IsActive
        });
        return Ok(result);
    }

    /// <summary>
    /// Crea un nuevo cliente con su cuenta corriente inicializada en saldo 0.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClienteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Apellido))
            return BadRequest(new { message = "Nombre y apellido son obligatorios." });

        if (string.IsNullOrWhiteSpace(request.Telefono) && string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Debe indicar al menos un teléfono o un email de contacto." });

        var cliente = new Cliente
        {
            Nombre = request.Nombre.Trim(),
            Apellido = request.Apellido.Trim(),
            Telefono = request.Telefono?.Trim(),
            Email = request.Email?.Trim(),
            LimiteCredito = request.LimiteCredito
        };

        var created = await _clienteService.CreateAsync(cliente, ct);

        // Crear automáticamente la cuenta corriente con saldo 0
        var cuentaCorriente = new CuentaCorriente
        {
            ClienteId = created.Id,
            SaldoActual = 0
        };

        await _cuentaCorrienteService.CreateAsync(cuentaCorriente, ct);

        // Recargar con cuenta corriente incluida
        var clienteConCuenta = await _clienteService.GetWithCuentaCorrienteAsync(created.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, clienteConCuenta);
    }

    /// <summary>
    /// Actualiza un cliente existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClienteRequest request, CancellationToken ct)
    {
        var cliente = await _clienteService.GetByIdAsync(id, ct);
        if (cliente is null)
            return NotFound(new { message = $"Cliente con ID '{id}' no encontrado." });

        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Apellido))
            return BadRequest(new { message = "Nombre y apellido son obligatorios." });

        cliente.Nombre = request.Nombre.Trim();
        cliente.Apellido = request.Apellido.Trim();
        cliente.Telefono = request.Telefono?.Trim();
        cliente.Email = request.Email?.Trim();
        cliente.LimiteCredito = request.LimiteCredito;

        await _clienteService.UpdateAsync(cliente, ct);
        return NoContent();
    }

    /// <summary>
    /// Elimina un cliente (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var exists = await _clienteService.ExistsAsync(id, ct);
        if (!exists)
            return NotFound(new { message = $"Cliente con ID '{id}' no encontrado." });

        await _clienteService.DeleteAsync(id, ct);
        return NoContent();
    }
}

// ═══════════════════════════════════
// DTOs de Request
// ═══════════════════════════════════

public record CreateClienteRequest(
    string Nombre,
    string Apellido,
    string? Telefono,
    string? Email,
    decimal LimiteCredito
);

public record UpdateClienteRequest(
    string Nombre,
    string Apellido,
    string? Telefono,
    string? Email,
    decimal LimiteCredito
);
