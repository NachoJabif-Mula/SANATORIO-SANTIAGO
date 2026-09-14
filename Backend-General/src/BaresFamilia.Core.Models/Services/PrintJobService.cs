using System.Text.Json;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Cierra el ciclo de vida de los trabajos de impresión reportados por el bridge local.
/// </summary>
public class PrintJobService : IPrintJobService
{
    private readonly IPrintJobRepository _printJobRepository;

    public PrintJobService(IPrintJobRepository printJobRepository)
    {
        _printJobRepository = printJobRepository;
    }

    public async Task<bool> RegistrarResultadoImpresionAsync(
        Guid jobId, bool exitoso, string? mensajeError, string? resultadoJson, CancellationToken ct = default)
    {
        var job = await _printJobRepository.GetPorIdIncluyendoInactivosAsync(jobId, ct);
        if (job is null)
            return false;

        // Sin resultado estructurado se guarda el mensaje de error como traza.
        await CerrarTrabajoAsync(job, exitoso, resultadoJson ?? mensajeError, mensajeError, ct);
        return true;
    }

    public async Task<bool> RegistrarResultadoFiscalAsync(
        Guid jobId, bool exitoso, string? cae, string? nroComprobante, string? mensajeError, CancellationToken ct = default)
    {
        var job = await _printJobRepository.GetPorIdIncluyendoInactivosAsync(jobId, ct);
        if (job is null)
            return false;

        var resultado = exitoso
            ? JsonSerializer.Serialize(new { cae, comprobante = nroComprobante })
            : JsonSerializer.Serialize(new { error = mensajeError });

        await CerrarTrabajoAsync(job, exitoso, resultado, mensajeError, ct);
        return true;
    }

    private async Task CerrarTrabajoAsync(PrintJob job, bool exitoso, string? resultadoJson, string? mensajeError, CancellationToken ct)
    {
        job.Estado = exitoso ? EstadoPrintJob.Impreso : EstadoPrintJob.Fallo;
        job.ResultadoJson = resultadoJson;
        job.UltimoError = exitoso ? null : mensajeError;
        job.FechaProcesado = DateTime.UtcNow;

        await _printJobRepository.UpdateAsync(job, ct);
    }
}
