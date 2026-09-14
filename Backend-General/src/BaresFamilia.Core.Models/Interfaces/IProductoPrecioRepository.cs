using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para ProductoPrecio (precio por producto y tipo de venta).
/// </summary>
public interface IProductoPrecioRepository : IRepository<ProductoPrecio>
{
    /// <summary>
    /// Obtiene los precios activos de un producto.
    /// </summary>
    Task<IEnumerable<ProductoPrecio>> GetActivosPorProductoAsync(Guid productoId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los precios (activos e inactivos) de un producto en una sucursal.
    /// Se usa para el upsert en lote, que necesita ver también los dados de baja
    /// para poder reactivarlos en lugar de duplicarlos.
    /// </summary>
    Task<IEnumerable<ProductoPrecio>> GetTodosPorProductoYSucursalAsync(Guid productoId, Guid sucursalId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los precios (activos e inactivos) de una sucursal.
    /// Endpoint de sincronización del POS local.
    /// </summary>
    Task<IEnumerable<ProductoPrecio>> GetTodosPorSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>
    /// Persiste en una sola transacción el reemplazo del juego de precios de un
    /// producto: inserta los nuevos y guarda los modificados (tanto los que
    /// cambiaron de precio como los que quedaron desactivados por no venir en el lote).
    /// </summary>
    Task GuardarLoteAsync(IEnumerable<ProductoPrecio> nuevos, IEnumerable<ProductoPrecio> modificados, CancellationToken ct = default);
}
