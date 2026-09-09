using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CuentaCorrienteController : ControllerBase
{
    private readonly NubeContext _context;
    private readonly ILogger<CuentaCorrienteController> _logger;

    public CuentaCorrienteController(NubeContext context, ILogger<CuentaCorrienteController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Obtiene el historial de movimientos de un cliente (para reporte en Backoffice).</summary>
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

        _logger.LogInformation("Historial de movimientos obtenido para cliente {ClienteId}. Total movimientos: {Count}", clienteId, movimientos.Count);

        return Ok(new { saldoActual = cuenta.SaldoActual, movimientos });
    }
}
