using BaresFamilia.Core.Models.Entities.Inventario;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para Receta.
/// Extiende IRepository con operaciones del módulo de inventario.
/// </summary>
public interface IRecetaRepository : IRepository<Receta>
{
    /// <summary>
    /// Obtiene todas las recetas de un producto con sus insumos incluidos.
    /// </summary>
    Task<IEnumerable<Receta>> GetByProductoAsync(Guid productoId, CancellationToken ct = default);
}
