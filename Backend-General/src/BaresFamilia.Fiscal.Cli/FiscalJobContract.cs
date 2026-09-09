using BaresFamilia.Fiscal.Engine;

namespace BaresFamilia.Fiscal.Cli;

/// <summary>
/// Contrato de entrada para el ejecutable autónomo BaresFamilia.Fiscal.Cli.
/// </summary>
public class FiscalJobInput
{
    /// <summary>
    /// Operación a realizar: "Factura", "NotaCredito", "CierreZ", "CierreX", "Cancelar", "ConsultarEstado".
    /// </summary>
    public string Operacion { get; set; } = "Factura";

    /// <summary>
    /// Configuración del dispositivo fiscal físico.
    /// </summary>
    public FiscalDeviceConfig Config { get; set; } = new();

    /// <summary>
    /// Datos del comprobante a emitir.
    /// </summary>
    public FiscalFacturaRequest Request { get; set; } = new();
}

/// <summary>
/// Contrato de salida JSON generado por BaresFamilia.Fiscal.Cli.
/// </summary>
public class FiscalJobOutput
{
    public bool Success { get; set; }
    public string NumeroComprobante { get; set; } = string.Empty;
    public string Cae { get; set; } = string.Empty;
    public string VencimientoCae { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string RawResponseJson { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }

    public static FiscalJobOutput FromResult(FiscalResult res, long elapsedMs) => new()
    {
        Success = res.Success,
        NumeroComprobante = res.NumeroComprobante,
        Cae = res.Cae,
        VencimientoCae = res.VencimientoCae,
        ErrorCode = res.ErrorCode,
        ErrorMessage = res.ErrorMessage,
        RawResponseJson = res.RawResponseJson,
        ElapsedMs = elapsedMs
    };
}
