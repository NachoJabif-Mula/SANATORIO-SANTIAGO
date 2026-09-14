using BaresFamilia.Core.Models.Contratos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio específico para ProductoPrecio.
/// Flujo: ProductoPrecioController → IProductoPrecioService → ProductoPrecioService → IProductoPrecioRepository → ProductoPrecioRepository.
/// </summary>
public interface IProductoPrecioService : IService<ProductoPrecio>
{
    /// <summary>
    /// Obtiene los precios activos configurados para un producto.
    /// </summary>
    Task<IEnumerable<ProductoPrecio>> GetActivosPorProductoAsync(Guid productoId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los precios (activos e inactivos) de una sucursal, para la
    /// sincronización del POS local.
    /// </summary>
    Task<IEnumerable<ProductoPrecio>> GetTodosPorSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>
    /// Reemplaza el juego de precios de un producto: da de alta o actualiza los
    /// enviados y desactiva los que no vengan en el lote. La sucursal del precio
    /// siempre es la del producto, nunca se recibe desde afuera.
    /// </summary>
    Task ReemplazarPreciosDeProductoAsync(Guid productoId, Guid sucursalId, IEnumerable<PrecioPorTipoVenta> precios, CancellationToken ct = default);
}
