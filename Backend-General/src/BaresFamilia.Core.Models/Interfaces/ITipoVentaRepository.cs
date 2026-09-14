using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para TipoVenta.
/// </summary>
public interface ITipoVentaRepository : IRepository<TipoVenta>
{
    /// <summary>
    /// Obtiene los tipos de venta ordenados por nombre. Si incluirInactivos es true
    /// agrega los dados de baja (usado por la sincronización de la sucursal).
    /// </summary>
    Task<IEnumerable<TipoVenta>> GetOrdenadosPorNombreAsync(bool incluirInactivos, CancellationToken ct = default);

    /// <summary>
    /// Indica si existe al menos un tipo de venta cargado (activo o inactivo).
    /// </summary>
    Task<bool> ExisteAlgunoAsync(CancellationToken ct = default);
}
