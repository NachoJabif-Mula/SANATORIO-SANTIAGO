using BaresFamilia.Core.Models.Contratos.Fiscal;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Cliente de WSFEv1, el servicio de Factura Electrónica de ARCA que autoriza comprobantes
/// y devuelve el CAE.
/// </summary>
public interface IWsfeClient
{
    /// <summary>
    /// Último número autorizado por ARCA para ese punto de venta y tipo de comprobante.
    /// Es la fuente de verdad de la numeración: el próximo comprobante es este número + 1.
    /// Devuelve 0 cuando todavía no se emitió ninguno.
    /// </summary>
    Task<long> ConsultarUltimoAutorizadoAsync(
        Guid sucursalId, int puntoVenta, TipoComprobanteAfip tipoComprobante, CancellationToken ct = default);

    /// <summary>
    /// Solicita el CAE para un comprobante (FECAESolicitar).
    /// </summary>
    Task<RespuestaCae> SolicitarCaeAsync(Guid sucursalId, SolicitudCae solicitud, CancellationToken ct = default);

    /// <summary>
    /// Consulta el estado de los servidores de ARCA (FEDummy). No requiere autenticación.
    /// </summary>
    Task<EstadoServiciosArca> ConsultarEstadoServiciosAsync(AmbienteFiscal ambiente, CancellationToken ct = default);
}
