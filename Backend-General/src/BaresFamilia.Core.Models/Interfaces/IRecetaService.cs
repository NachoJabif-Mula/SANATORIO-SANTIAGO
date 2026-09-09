using BaresFamilia.Core.Models.Entities.Inventario;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio específico para Receta.
/// Extiende IService con operaciones del módulo de inventario.
/// </summary>
public interface IRecetaService : IService<Receta>
{
    /// <summary>
    /// Obtiene todas las recetas (insumos necesarios) de un producto.
    /// </summary>
    Task<IEnumerable<Receta>> GetByProductoAsync(Guid productoId, CancellationToken ct = default);
}
