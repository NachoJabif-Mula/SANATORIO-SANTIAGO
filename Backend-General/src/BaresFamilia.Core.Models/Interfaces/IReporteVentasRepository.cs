using BaresFamilia.Core.Models.Contratos;
using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio de consultas de reportería de ventas sobre los datos consolidados
/// en la Nube. Es de solo lectura y cruza varias entidades, por eso no extiende
/// IRepository de una entidad puntual.
/// </summary>
public interface IReporteVentasRepository
{
    /// <summary>
    /// Obtiene todas las comandas sincronizadas con mesa, sucursal, usuario, tipo de
    /// venta, ítems y pagos cargados, de la más reciente a la más antigua.
    /// </summary>
    Task<IEnumerable<Comanda>> GetComandasConDetallesAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene, paginados, los ítems anulados en el período indicado, con su producto
    /// y el usuario que los anuló cargados.
    /// </summary>
    Task<ResultadoPaginado<ComandaItem>> GetItemsAnuladosAsync(
        Guid? sucursalId, DateTime? desde, DateTime? hasta, int pagina, int tamanoPagina, CancellationToken ct = default);

    /// <summary>
    /// Obtiene, paginadas, las comandas anuladas por completo en el período indicado,
    /// con sus ítems y los usuarios que las anularon cargados.
    /// </summary>
    Task<ResultadoPaginado<Comanda>> GetComandasAnuladasAsync(
        Guid? sucursalId, DateTime? desde, DateTime? hasta, int pagina, int tamanoPagina, CancellationToken ct = default);
}
