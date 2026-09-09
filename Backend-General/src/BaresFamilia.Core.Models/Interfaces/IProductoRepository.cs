using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para Producto.
/// Extiende IRepository con operaciones propias del dominio.
/// </summary>
public interface IProductoRepository : IRepository<Producto>
{
    /// <summary>
    /// Obtiene productos activos por categoría, incluyendo la navegación a Categoria.
    /// </summary>
    Task<IEnumerable<Producto>> GetByCategoriaAsync(Guid categoriaId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un producto con su categoría y precios incluidos.
    /// </summary>
    Task<Producto?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
}
