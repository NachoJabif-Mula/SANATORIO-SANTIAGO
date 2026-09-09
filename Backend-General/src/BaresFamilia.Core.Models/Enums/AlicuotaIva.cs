namespace BaresFamilia.Core.Models.Enums;

/// <summary>
/// Alícuota de IVA aplicable a un producto, con su código AFIP correspondiente.
/// Los valores numéricos corresponden a los códigos de eIvaType usados por AFIP
/// y por las impresoras fiscales (Epson DLL, Hasar XML).
/// </summary>
public enum AlicuotaIva
{
    /// <summary>
    /// No gravado (código AFIP: 0).
    /// </summary>
    NoGravado = 0,

    /// <summary>
    /// Exento de IVA (código AFIP: 1).
    /// </summary>
    Exento = 1,

    /// <summary>
    /// IVA 10.5% (código AFIP: 4).
    /// </summary>
    Iva105 = 4,

    /// <summary>
    /// IVA 21% — alícuota general (código AFIP: 5).
    /// </summary>
    Iva21 = 5,

    /// <summary>
    /// IVA 27% — servicios públicos y telecomunicaciones (código AFIP: 6).
    /// </summary>
    Iva27 = 6
}
