using BaresFamilia.Core.Models.Contratos.Fiscal;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Configuración fiscal en la base local de la sucursal.
///
/// La sucursal factura contra ARCA en el momento del cobro, así que necesita su certificado
/// en su propia base: el que se carga en el Backoffice queda en la Nube y todavía no baja
/// solo. Este endpoint permite cargarlo directamente en la sucursal.
/// </summary>
[ApiController]
[Route("api/configuracion-fiscal")]
public class ConfiguracionFiscalController : ControllerBase
{
    private readonly IConfiguracionFiscalService _configuracionFiscalService;

    public ConfiguracionFiscalController(IConfiguracionFiscalService configuracionFiscalService)
    {
        _configuracionFiscalService = configuracionFiscalService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EstadoFiscalSucursal>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await _configuracionFiscalService.ObtenerEstadosAsync(ct));

    [HttpGet("{sucursalId:guid}")]
    [ProducesResponseType(typeof(EstadoFiscalSucursal), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPorSucursal(Guid sucursalId, CancellationToken ct)
    {
        var estado = await _configuracionFiscalService.ObtenerEstadoAsync(sucursalId, ct);
        return estado is null
            ? NotFound(new { message = "Sucursal no encontrada." })
            : Ok(estado);
    }

    /// <summary>
    /// Genera la solicitud de certificado (CSR) para subir al portal de ARCA.
    /// </summary>
    [HttpPost("{sucursalId:guid}/solicitud")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerarSolicitud(Guid sucursalId, CancellationToken ct)
    {
        try
        {
            var solicitud = await _configuracionFiscalService.GenerarSolicitudCertificadoAsync(sucursalId, ct);
            return File(System.Text.Encoding.ASCII.GetBytes(solicitud.ContenidoPem), "application/pkcs10", solicitud.NombreArchivo);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Carga el certificado (.crt) emitido por ARCA para la solicitud generada.
    /// </summary>
    [HttpPost("{sucursalId:guid}/certificado-emitido")]
    [ProducesResponseType(typeof(EstadoFiscalSucursal), StatusCodes.Status200OK)]
    public async Task<IActionResult> CargarCertificadoEmitido(
        Guid sucursalId,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Se requiere el archivo del certificado que emitió ARCA." });

        using var memoria = new MemoryStream();
        await file.CopyToAsync(memoria, ct);

        return await EjecutarAsync(() => _configuracionFiscalService.CargarCertificadoEmitidoAsync(
            sucursalId, memoria.ToArray(), file.FileName, ct));
    }

    /// <summary>
    /// Carga un PKCS#12 (.pfx/.p12) ya armado por fuera del sistema.
    /// </summary>
    [HttpPost("{sucursalId:guid}/certificado")]
    [ProducesResponseType(typeof(EstadoFiscalSucursal), StatusCodes.Status200OK)]
    public async Task<IActionResult> CargarCertificado(
        Guid sucursalId,
        [FromForm] IFormFile? file,
        [FromForm] string? password,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Se requiere el archivo del certificado (.pfx o .p12)." });

        using var memoria = new MemoryStream();
        await file.CopyToAsync(memoria, ct);

        return await EjecutarAsync(() => _configuracionFiscalService.CargarCertificadoAsync(
            sucursalId, memoria.ToArray(), file.FileName, password, ct));
    }

    [HttpPut("{sucursalId:guid}/ambiente")]
    [ProducesResponseType(typeof(EstadoFiscalSucursal), StatusCodes.Status200OK)]
    public async Task<IActionResult> CambiarAmbiente(
        Guid sucursalId,
        [FromBody] CambiarAmbienteRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<AmbienteFiscal>(request.Ambiente, ignoreCase: true, out var ambiente))
            return BadRequest(new { message = "Ambiente inválido. Valores admitidos: Homologacion, Produccion." });

        return await EjecutarAsync(() => _configuracionFiscalService.CambiarAmbienteAsync(sucursalId, ambiente, ct));
    }

    [HttpPost("{sucursalId:guid}/verificar")]
    [ProducesResponseType(typeof(EstadoFiscalSucursal), StatusCodes.Status200OK)]
    public async Task<IActionResult> Verificar(Guid sucursalId, CancellationToken ct) =>
        await EjecutarAsync(() => _configuracionFiscalService.VerificarAsync(sucursalId, ct));

    private async Task<IActionResult> EjecutarAsync(Func<Task<EstadoFiscalSucursal>> operacion)
    {
        try
        {
            return Ok(await operacion());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
