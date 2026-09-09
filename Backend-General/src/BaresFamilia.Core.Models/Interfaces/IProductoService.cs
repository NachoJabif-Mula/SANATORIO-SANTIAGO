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
}
