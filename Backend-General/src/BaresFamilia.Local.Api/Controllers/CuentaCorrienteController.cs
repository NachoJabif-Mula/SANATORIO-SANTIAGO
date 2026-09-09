using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Local.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CuentaCorrienteController : ControllerBase
{
    private readonly LocalContext _context;
    private readonly IService<MovimientoCuentaCorriente> _movimientoService;
    private readonly IService<CuentaCorriente> _cuentaCorrienteService;
    private readonly IService<MovimientoCaja> _movimientoCajaService;
    private readonly ILogger<CuentaCorrienteController> _logger;

    public CuentaCorrienteController(
        LocalContext context,
        IService<MovimientoCuentaCorriente> movimientoService,
        IService<CuentaCorriente> cuentaCorrienteService,
        IService<MovimientoCaja> movimientoCajaService,
        ILogger<CuentaCorrienteController> logger)
    {
        _context = context;
        _movimientoService = movimientoService;
        _cuentaCorrienteService = cuentaCorrienteService;
        _movimientoCajaService = movimientoCajaService;
        _logger = logger;
    }

    /// <summary>Obtiene el historial de movimientos de un cliente para seguimiento de cuenta corriente.</summary>
    [HttpGet("{clienteId:guid}/movimientos")]
    public async Task<IActionResult> GetMovimientos(Guid clienteId, CancellationToken ct)
    {
        var cuenta = await _context.CuentasCorrientes.FirstOrDefaultAsync(c => c.ClienteId == clienteId, ct);
        if (cuenta is null) return NotFound(new { message = "El cliente no tiene cuenta corriente." });

        var movimientos = await _context.Set<MovimientoCuentaCorriente>()
            .Where(m => m.CuentaCorrienteId == cuenta.Id && m.IsActive)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new {
                id = m.Id,
                tipo = m.Tipo.ToString(),
                monto = m.Monto,
                detalle = m.Detalle,
                comandaId = m.ComandaId,
                fecha = m.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { saldoActual = cuenta.SaldoActual, movimientos });
    }

    /// <summary>Liquida (total o parcialmente) el saldo de cuenta corriente de un cliente.</summary>
    [HttpPost("{clienteId:guid}/abonar")]
    public async Task<IActionResult> Abonar(Guid clienteId, [FromBody] AbonarCuentaCorrienteRequest request, CancellationToken ct)
    {
        if (request.Monto <= 0)
            return BadRequest(new { message = "El monto a abonar debe ser mayor a cero." });

        var cuenta = await _context.CuentasCorrientes
            .Include(c => c.Cliente)
            .FirstOrDefaultAsync(c => c.ClienteId == clienteId, ct);
        if (cuenta is null) return NotFound(new { message = "El cliente no tiene cuenta corriente." });

        if (request.Monto > cuenta.SaldoActual)
            return BadRequest(new { message = $"El monto a abonar ({request.Monto:N2}) supera el saldo adeudado ({cuenta.SaldoActual:N2})." });

        var metodoPago = await _context.MetodosPago.FirstOrDefaultAsync(m => m.Id == request.MetodoPagoId && m.IsActive, ct);
        if (metodoPago is null) return BadRequest(new { message = "Método de pago no encontrado." });
        if (metodoPago.EsCuentaCorriente)
            return BadRequest(new { message = "No se puede liquidar una cuenta corriente usando 'Cuenta Corriente' como método de pago." });

        var turnoCaja = await _context.TurnosCaja.FirstOrDefaultAsync(t => t.Id == request.TurnoCajaId && t.FechaCierre == null, ct);
        if (turnoCaja is null) return BadRequest(new { message = "No hay un turno de caja activo válido." });

        // 1. Crear movimiento de cuenta corriente (Pago)
        var movimiento = new MovimientoCuentaCorriente
        {
            CuentaCorrienteId = cuenta.Id,
            ComandaId = null,
            Tipo = TipoMovimientoCuentaCorriente.Pago,
            Monto = request.Monto,
            Detalle = $"Abono a cuenta corriente vía {metodoPago.Nombre} — {cuenta.Cliente.Nombre} {cuenta.Cliente.Apellido}",
            SyncEstado = SyncEstado.Pendiente
        };
        await _movimientoService.CreateAsync(movimiento, ct);

        // 2. Actualizar saldo
        cuenta.SaldoActual -= request.Monto;
        cuenta.UpdatedAt = DateTime.UtcNow;
        await _cuentaCorrienteService.UpdateAsync(cuenta, ct);

        // 3. Reflejar en arqueo de caja SI el cobro fue en Efectivo
        if (metodoPago.Nombre.Equals("Efectivo", StringComparison.OrdinalIgnoreCase))
        {
            await _movimientoCajaService.CreateAsync(new MovimientoCaja
            {
                TurnoCajaId = request.TurnoCajaId,
                Tipo = TipoMovimientoCaja.Ingreso,
                Monto = request.Monto,
                Concepto = $"Cobro Cta. Cte. — {cuenta.Cliente.Nombre} {cuenta.Cliente.Apellido}",
                SyncEstado = SyncEstado.Pendiente
            }, ct);
        }

        _logger.LogInformation("Abono de Cta. Cte. registrado. Cliente {ClienteId}. Monto {Monto}. Nuevo saldo {Saldo}", clienteId, request.Monto, cuenta.SaldoActual);

        return Ok(new {
            saldoAnterior = cuenta.SaldoActual + request.Monto,
            saldoActual = cuenta.SaldoActual,
            montoAbonado = request.Monto
        });
    }
}

public record AbonarCuentaCorrienteRequest(Guid TurnoCajaId, Guid MetodoPagoId, decimal Monto);
