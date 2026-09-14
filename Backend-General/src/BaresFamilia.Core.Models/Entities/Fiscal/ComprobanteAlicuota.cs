using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Fiscal;

/// <summary>
/// Desglose de IVA de un comprobante por alícuota. WSFEv1 exige enviar una entrada por
/// cada alícuota presente en la venta, y el reporte de IVA Ventas se arma agrupando por acá.
/// </summary>
public class ComprobanteAlicuota : BaseEntity
{
    public Guid ComprobanteId { get; set; }

    public AlicuotaIva Alicuota { get; set; } = AlicuotaIva.Iva21;

    /// <summary>
    /// Importe neto gravado por esta alícuota.
    /// </summary>
    public decimal BaseImponible { get; set; }

    /// <summary>
    /// IVA liquidado sobre la base imponible.
    /// </summary>
    public decimal Importe { get; set; }

    // Navegación
    public Comprobante? Comprobante { get; set; }
}
