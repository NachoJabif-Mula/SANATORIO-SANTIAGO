using BaresFamilia.Core.Models.Dtos.Catalogos;
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

    /// <summary>
    /// Obtiene los productos de una sucursal ordenados por nombre. Si sucursalId es
    /// null devuelve los de todas (vista consolidada); si incluirInactivos es true
    /// agrega los dados de baja (usado por la sincronización).
    /// </summary>
    Task<IEnumerable<Producto>> GetPorSucursalAsync(Guid? sucursalId, bool incluirInactivos, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un producto por ID sin filtrar por IsActive. Lo usa la consulta de
    /// precios, que debe seguir respondiendo para productos dados de baja.
    /// </summary>
    Task<Producto?> GetPorIdIncluyendoInactivosAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los productos activos junto con su precio de venta, tal como los
    /// consume la grilla del punto de venta.
    /// </summary>
    Task<IEnumerable<ProductoDisponible>> GetDisponiblesConPrecioAsync(CancellationToken ct = default);
}
