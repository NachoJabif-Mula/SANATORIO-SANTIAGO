using BaresFamilia.Core.Models.Entities.Inventario;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio específico para Insumo.
/// Extiende IService con operaciones del módulo de inventario.
/// </summary>
public interface IInsumoService : IService<Insumo>
{
    /// <summary>
    /// Obtiene los insumos cuyo stock actual está por debajo del mínimo en una sucursal.
    /// </summary>
    Task<IEnumerable<Insumo>> GetConStockBajoAsync(Guid sucursalId, CancellationToken ct = default);
}
