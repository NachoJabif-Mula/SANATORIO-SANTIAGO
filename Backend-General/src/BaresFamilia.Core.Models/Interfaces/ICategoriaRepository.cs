using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para Categoria.
/// Extiende IRepository con las consultas propias del catálogo por sucursal.
/// </summary>
public interface ICategoriaRepository : IRepository<Categoria>
{
    /// <summary>
    /// Obtiene las categorías ordenadas por orden visual. Si sucursalId es null
    /// devuelve las de todas las sucursales (vista consolidada del backoffice);
    /// si incluirInactivas es true agrega las dadas de baja (usado por la sincronización).
    /// </summary>
    Task<IEnumerable<Categoria>> GetPorSucursalAsync(Guid? sucursalId, bool incluirInactivas, CancellationToken ct = default);
}
