namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio que cierra el ciclo de vida de los trabajos de impresión: el bridge
/// local ejecuta el trabajo y reporta acá cómo le fue.
/// Flujo: PrintHub → IPrintJobService → PrintJobService → IPrintJobRepository → PrintJobRepository.
/// </summary>
public interface IPrintJobService
{
    /// <summary>
    /// Registra el resultado de un trabajo de impresión no fiscal.
    /// Devuelve false si el trabajo ya no existe.
    /// </summary>
    Task<bool> RegistrarResultadoImpresionAsync(Guid jobId, bool exitoso, string? mensajeError, string? resultadoJson, CancellationToken ct = default);

    /// <summary>
    /// Registra el resultado de una operación fiscal, guardando el CAE y el número
    /// de comprobante devueltos por la impresora o el servicio fiscal.
    /// Devuelve false si el trabajo ya no existe.
    /// </summary>
    Task<bool> RegistrarResultadoFiscalAsync(Guid jobId, bool exitoso, string? cae, string? nroComprobante, string? mensajeError, CancellationToken ct = default);
}
