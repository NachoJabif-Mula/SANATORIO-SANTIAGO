using BaresFamilia.Core.Models.Entities.Fiscal;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Cliente de WSAA, el servicio de autenticación de ARCA. Entrega el Ticket de Acceso (TA)
/// que WSFE exige en cada pedido de CAE.
/// </summary>
public interface IWsaaClient
{
    /// <summary>
    /// Devuelve un TA vigente para la sucursal: reutiliza el que esté cacheado y solo pide
    /// uno nuevo a WSAA cuando está por vencer, porque ARCA rechaza un segundo pedido
    /// mientras el anterior siga válido.
    /// </summary>
    Task<TicketAccesoWsaa> ObtenerTicketAccesoAsync(Guid sucursalId, string servicio = "wsfe", CancellationToken ct = default);
}
