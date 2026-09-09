namespace BaresFamilia.Core.Models.Enums;

/// <summary>
/// Tipo de dispositivo de impresión. Determina el driver y protocolo de comunicación a utilizar.
/// </summary>
public enum TipoDispositivoImpresora
{
    /// <summary>
    /// Impresora térmica de cocina/barra (ESC/POS). Solo imprime tickets no fiscales.
    /// </summary>
    Comandera,

    /// <summary>
    /// Controlador fiscal Epson (TM-T900FA / TM-T88).
    /// Comunicación vía DLL nativa oficial de Epson (P/Invoke). Conexión USB o puerto serie (COM).
    /// </summary>
    FiscalEpson,

    /// <summary>
    /// Controlador fiscal Hasar (SMH/PT-250AF 2G, P-HAS-250F).
    /// Comunicación vía HTTP POST con XML al servidor web embebido de la impresora.
    /// </summary>
    FiscalHasar,

    /// <summary>
    /// Controlador fiscal Moretti (Genesis).
    /// Comunicación vía protocolo binario serie. Fase futura de implementación.
    /// </summary>
    FiscalMoretti,

    /// <summary>
    /// Factura electrónica AFIP (sin hardware fiscal).
    /// Se emite vía WSFE de AFIP y se imprime en ticketera común o se genera PDF.
    /// </summary>
    Electronica
}
