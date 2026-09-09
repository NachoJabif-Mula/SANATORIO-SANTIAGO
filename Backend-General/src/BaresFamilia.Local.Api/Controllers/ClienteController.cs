using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClienteController : ControllerBase
{
    private readonly IClienteService _clienteService;
    private readonly IService<CuentaCorriente> _cuentaCorrienteService;
    private readonly ILogger<ClienteController> _logger;

    public ClienteController(
        IClienteService clienteService,
        IService<CuentaCorriente> cuentaCorrienteService,
        ILogger<ClienteController> logger)
    {
        _clienteService = clienteService;
        _cuentaCorrienteService = cuentaCorrienteService;
        _logger = logger;
    }

    /// <summary>Obtiene todos los clientes locales con saldoActual (para sincronización).</summary>
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
                saldoActual = conCuenta?.CuentaCorriente?.SaldoActual ?? 0m
            });
        }
        return Ok(result);
    }

    /// <summary>Busca clientes por nombre (para modal de selección en cobro).</summary>
    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar([FromQuery] string nombre, CancellationToken ct)
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
            saldoActual = c.CuentaCorriente?.SaldoActual ?? 0m
        });
        return Ok(result);
    }

    /// <summary>Crea un nuevo cliente local (alta rápida desde el POS).</summary>
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
        await _cuentaCorrienteService.CreateAsync(new CuentaCorriente
        {
            ClienteId = created.Id,
            SaldoActual = 0
        }, ct);

        _logger.LogInformation("Cliente creado localmente: {ClienteId} - {Nombre} {Apellido}", created.Id, created.Nombre, created.Apellido);

        var conCuenta = await _clienteService.GetWithCuentaCorrienteAsync(created.Id, ct);
        return CreatedAtAction(nameof(GetAll), new {
            id = conCuenta.Id,
            nombre = conCuenta.Nombre,
            apellido = conCuenta.Apellido,
            telefono = conCuenta.Telefono,
            email = conCuenta.Email,
            limiteCredito = conCuenta.LimiteCredito,
            saldoActual = conCuenta.CuentaCorriente?.SaldoActual ?? 0m
        });
    }

    /// <summary>Actualiza un cliente local.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClienteRequest request, CancellationToken ct)
    {
        var cliente = await _clienteService.GetByIdAsync(id, ct);
        if (cliente is null)
            return NotFound(new { message = "Cliente no encontrado." });

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
}

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
