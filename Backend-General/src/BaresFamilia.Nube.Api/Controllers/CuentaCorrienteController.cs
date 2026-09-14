using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Consulta consolidada de cuentas corrientes para el Backoffice.
/// Flujo: CuentaCorrienteController → ICuentaCorrienteService → CuentaCorrienteService → I*Repository → *Repository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Backoffice")]
public class CuentaCorrienteController : ControllerBase
{
    private readonly ICuentaCorrienteService _cuentaCorrienteService;

    public CuentaCorrienteController(ICuentaCorrienteService cuentaCorrienteService)
    {
        _cuentaCorrienteService = cuentaCorrienteService;
    }

    /// <summary>Obtiene el historial de movimientos de un cliente (para reporte en Backoffice).</summary>
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
}
