using BaresFamilia.Core.Models.Entities.Fiscal;
using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Emisión de comprobantes electrónicos: arma el comprobante a partir de la venta,
/// le pide el CAE a ARCA y deja el resultado persistido.
/// </summary>
public interface IFacturacionElectronicaService
{
    /// <summary>
    /// Emite el comprobante correspondiente a una comanda cobrada.
    ///
    /// Siempre devuelve un <see cref="Comprobante"/> persistido: si ARCA no estuvo
    /// disponible queda en estado Pendiente para que se reintente, y si ARCA rechazó
    /// el pedido queda en Rechazado con el detalle del error.
    /// </summary>
    Task<Comprobante> EmitirDesdeComandaAsync(Comanda comanda, Guid sucursalId, CancellationToken ct = default);

    /// <summary>
    /// Reintenta autorizar un comprobante que quedó pendiente por falta de conectividad.
    /// </summary>
    Task<Comprobante> ReintentarAsync(Guid comprobanteId, CancellationToken ct = default);
}
