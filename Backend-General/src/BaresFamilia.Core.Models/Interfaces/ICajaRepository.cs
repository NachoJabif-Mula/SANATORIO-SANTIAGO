using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para Caja.
/// </summary>
public interface ICajaRepository : IRepository<Caja>
{
    /// <summary>
    /// Obtiene la caja activa de la sucursal. La API Local opera una única caja,
    /// así que devuelve la primera activa que encuentre (o null si no hay ninguna).
    /// </summary>
    Task<Caja?> GetActivaAsync(CancellationToken ct = default);
}
