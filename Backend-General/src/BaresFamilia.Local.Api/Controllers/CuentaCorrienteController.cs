using BaresFamilia.Core.Models.Contratos.CuentasCorrientes;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Controlador local de cuentas corrientes de clientes.
/// Flujo: CuentaCorrienteController → ICuentaCorrienteService → CuentaCorrienteService → I*Repository → *Repository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CuentaCorrienteController : ControllerBase
{
    private readonly ICuentaCorrienteService _cuentaCorrienteService;

    public CuentaCorrienteController(ICuentaCorrienteService cuentaCorrienteService)
    {
        _cuentaCorrienteService = cuentaCorrienteService;
    }

    /// <summary>Obtiene el historial de movimientos de un cliente para seguimiento de cuenta corriente.</summary>
    [HttpGet("{clienteId:guid}/movimientos")]
    public async Task<IActionResult> GetMovimientos(Guid clienteId, CancellationToken ct)
    {
        var resumen = await _cuentaCorrienteService.GetResumenPorClienteAsync(clienteId, ct);

        return Ok(new
        {
            saldoActual = resumen.SaldoActual,
            movimientos = resumen.Movimientos.Select(m => new
            {
                id = m.Id,
                tipo = m.Tipo.ToString(),
                monto = m.Monto,
                detalle = m.Detalle,
                comandaId = m.ComandaId,
                fecha = m.CreatedAt
            })
        });
    }

    /// <summary>Liquida (total o parcialmente) el saldo de cuenta corriente de un cliente.</summary>
    [HttpPost("{clienteId:guid}/abonar")]
    public async Task<IActionResult> Abonar(Guid clienteId, [FromBody] AbonarCuentaCorrienteRequest request, CancellationToken ct)
    {
        var resultado = await _cuentaCorrienteService.AbonarAsync(
            clienteId, request.TurnoCajaId, request.MetodoPagoId, request.Monto, ct);

        return Ok(new
        {
            saldoAnterior = resultado.SaldoAnterior,
            saldoActual = resultado.SaldoActual,
            montoAbonado = resultado.MontoAbonado
        });
    }
}
