using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio específico para Producto.
/// Extiende IService con operaciones propias del dominio.
/// </summary>
public interface IProductoService : IService<Producto>
{
    /// <summary>
    /// Obtiene productos activos filtrados por categoría.
    /// </summary>
    Task<IEnumerable<Producto>> GetByCategoriaAsync(Guid categoriaId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un producto con todos sus detalles (categoría, precios).
    /// </summary>
    Task<Producto?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los productos de una sucursal (o de todas si sucursalId es null),
    /// ordenados por nombre.
    /// </summary>
    Task<IEnumerable<Producto>> GetPorSucursalAsync(Guid? sucursalId, bool incluirInactivos, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un producto por ID aunque esté dado de baja.
    /// </summary>
    Task<Producto?> GetPorIdIncluyendoInactivosAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Da de alta un producto validando nombre obligatorio y que la categoría exista
    /// y pertenezca a la misma sucursal que el producto.
    /// </summary>
    Task<Producto> CrearAsync(Producto producto, CancellationToken ct = default);

    /// <summary>
    /// Guarda los cambios de un producto existente aplicando las mismas validaciones
    /// de nombre y coherencia de categoría que el alta.
    /// </summary>
    Task ActualizarAsync(Producto producto, CancellationToken ct = default);
}
