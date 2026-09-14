namespace BaresFamilia.Core.Models.Enums;

/// <summary>
/// Tipo de comprobante según la tabla oficial de ARCA (ex-AFIP).
/// Los valores numéricos son los códigos que espera WSFEv1 en el campo CbteTipo.
/// </summary>
public enum TipoComprobanteAfip
{
    FacturaA = 1,
    NotaDebitoA = 2,
    NotaCreditoA = 3,
    FacturaB = 6,
    NotaDebitoB = 7,
    NotaCreditoB = 8,
    FacturaC = 11,
    NotaDebitoC = 12,
    NotaCreditoC = 13
}
