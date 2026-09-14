namespace BaresFamilia.Core.Models.Enums;

/// <summary>
/// Ambiente de emisión ante ARCA (ex-AFIP). Determina contra qué endpoints de
/// WSAA/WSFE se autentica y solicita el CAE cada sucursal.
/// </summary>
public enum AmbienteFiscal
{
    /// <summary>
    /// Homologación (testing). Los comprobantes emitidos no tienen validez fiscal.
    /// </summary>
    Homologacion = 0,

    /// <summary>
    /// Producción. Los comprobantes emitidos son reales ante ARCA.
    /// </summary>
    Produccion = 1
}
