using BaresFamilia.Core.Models.Contratos.Cajas;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Endpoints para gestión de turnos de caja, egresos, cierre de turno y cierre diario.
/// Los turnos de caja son gestionados por usuarios con rol "encargado".
/// Flujo: CajaController → ICajaService → CajaService → I*Repository → *Repository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CajaController : ControllerBase
{
    private readonly ICajaService _cajaService;

    public CajaController(ICajaService cajaService)
    {
        _cajaService = cajaService;
    }

    /// <summary>
    /// Endpoint para consultar el estado actual contable y turno disponible de la caja.
    /// </summary>
    [HttpGet("estado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEstadoCaja(CancellationToken ct)
    {
        var estado = await _cajaService.GetEstadoAsync(ct);
        return Ok(new { fechaContable = estado.FechaContable, turno = estado.Turno, estado = estado.Estado });
    }

    /// <summary>
    /// Obtiene el turno de caja activo (sin FechaCierre). Retorna 204 si no hay turno abierto.
    /// </summary>
    [HttpGet("turno-activo")]
    [ProducesResponseType(typeof(TurnoActivoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetTurnoActivo(CancellationToken ct)
    {
        var turno = await _cajaService.GetTurnoActivoAsync(ct);
        return turno is null ? NoContent() : Ok(turno);
    }

    /// <summary>
    /// Abre un nuevo turno de caja. Solo permitido si no hay otro turno abierto para la misma caja.
    /// Requiere usuario con permiso de encargado.
    /// </summary>
    [HttpPost("abrir-turno")]
    [ProducesResponseType(typeof(TurnoActivoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AbrirTurno([FromBody] AbrirTurnoRequest request, CancellationToken ct)
    {
        var turno = await _cajaService.AbrirTurnoAsync(request.UsuarioId, request.FondoInicial, ct);
        return Created(string.Empty, turno);
    }

    /// <summary>
    /// Registra un egreso de caja (retiro de efectivo para pago a proveedores,
    /// gastos operativos, etc.) en el turno de caja indicado.
    /// </summary>
    [HttpPost("egresos")]
    [ProducesResponseType(typeof(MovimientoCaja), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarEgreso([FromBody] RegistrarEgresoRequest request, CancellationToken ct)
    {
        var movimiento = await _cajaService.RegistrarEgresoAsync(
            request.TurnoCajaId, request.Monto, request.Concepto, request.ReferenciaComprobante, ct);

        return Created(string.Empty, movimiento);
    }

    /// <summary>
    /// Obtiene el resumen parcial de un turno de caja (sin cerrarlo).
    /// </summary>
    [HttpGet("resumen-turno/{turnoId:guid}")]
    [ProducesResponseType(typeof(ResultadoCierreTurno), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResumenTurno(Guid turnoId, CancellationToken ct)
        => Ok(await _cajaService.GetResumenTurnoAsync(turnoId, ct));

    /// <summary>
    /// Realiza el cierre de turno (caja ciega). El encargado declara el efectivo que tiene
    /// sin ver el total calculado. El sistema calcula la diferencia (arqueo).
    /// Incluye desglose por método de pago, verificación de mesas abiertas y cierre diario automático si es PM.
    /// </summary>
    [HttpPost("cerrar-turno")]
    [ProducesResponseType(typeof(ResultadoCierreTurno), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CerrarTurno([FromBody] CerrarTurnoRequest request, CancellationToken ct)
        => Ok(await _cajaService.CerrarTurnoAsync(
            request.TurnoCajaId,
            request.MontoDeclarado,
            request.Observaciones,
            request.TransferirMesasAbiertas == true,
            ct));

    /// <summary>
    /// Genera el cierre diario consolidando todos los turnos del día para una caja.
    /// Requiere que todos los turnos del día estén cerrados.
    /// </summary>
    [HttpPost("cierre-diario")]
    [ProducesResponseType(typeof(ResultadoCierreDiario), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CierreDiario([FromBody] CierreDiarioRequest request, CancellationToken ct)
        => Ok(await _cajaService.GenerarCierreDiarioAsync(request.UsuarioId, request.Observaciones, ct));

    /// <summary>
    /// Obtiene el resumen del día actual (turnos, ventas, egresos).
    /// </summary>
    [HttpGet("resumen-dia")]
    [ProducesResponseType(typeof(ResumenDia), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResumenDia(CancellationToken ct)
        => Ok(await _cajaService.GetResumenDiaAsync(ct));
}
