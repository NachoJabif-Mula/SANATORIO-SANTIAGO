using BaresFamilia.Core.Models.Entities.Inventario;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para Insumo.
/// Extiende IRepository con operaciones del módulo de inventario.
/// </summary>
public interface IInsumoRepository : IRepository<Insumo>
{
    /// <summary>
    /// Obtiene los insumos cuyo stock actual está por debajo del mínimo en una sucursal.
    /// </summary>
    Task<IEnumerable<Insumo>> GetConStockBajoAsync(Guid sucursalId, CancellationToken ct = default);
}
