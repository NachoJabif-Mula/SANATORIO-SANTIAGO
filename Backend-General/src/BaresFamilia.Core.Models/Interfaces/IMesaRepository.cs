using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para Mesa.
/// </summary>
public interface IMesaRepository : IRepository<Mesa>
{
    /// <summary>
    /// Obtiene las mesas activas ordenadas por etiqueta, tal como se muestran en el salón.
    /// </summary>
    Task<IEnumerable<Mesa>> GetActivasOrdenadasAsync(CancellationToken ct = default);
}
