namespace BaresFamilia.Fiscal.Engine;

/// <summary>
/// Interfaz unificada para controladores de impresoras fiscales físicas (Epson, Hasar, etc.).
/// </summary>
public interface IFiscalDriver
{
    /// <summary>
    /// Nombre identificador del driver ("Epson", "Hasar", "Moretti").
    /// </summary>
    string NombreDriver { get; }

    /// <summary>
    /// Consulta el estado físico y fiscal de la impresora.
    /// </summary>
    Task<FiscalResult> ConsultarEstadoAsync(FiscalDeviceConfig config, CancellationToken ct = default);

    /// <summary>
    /// Emite una Factura fiscal (A, B o C).
    /// </summary>
    Task<FiscalResult> ImprimirFacturaAsync(FiscalDeviceConfig config, FiscalFacturaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Emite una Nota de Crédito fiscal (A, B o C).
    /// </summary>
    Task<FiscalResult> ImprimirNotaCreditoAsync(FiscalDeviceConfig config, FiscalFacturaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Realiza un Cierre Z (cierre de jornada fiscal).
    /// </summary>
    Task<FiscalResult> EjecutarCierreZAsync(FiscalDeviceConfig config, CancellationToken ct = default);

    /// <summary>
    /// Realiza un Cierre X (informe parcial sin cierre).
    /// </summary>
    Task<FiscalResult> EjecutarCierreXAsync(FiscalDeviceConfig config, CancellationToken ct = default);

    /// <summary>
    /// Cancela cualquier comprobante en curso que haya quedado abierto o interrumpido.
    /// </summary>
    Task<FiscalResult> CancelarComprobanteAsync(FiscalDeviceConfig config, CancellationToken ct = default);
}
