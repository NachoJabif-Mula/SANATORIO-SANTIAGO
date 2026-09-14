using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para MovimientoCaja (ingresos y egresos de efectivo).
/// </summary>
public interface IMovimientoCajaRepository : IRepository<MovimientoCaja>
{
    /// <summary>
    /// Obtiene los movimientos de un turno.
    /// </summary>
    Task<IEnumerable<MovimientoCaja>> GetPorTurnoAsync(Guid turnoCajaId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los movimientos de un conjunto de turnos (el día contable).
    /// </summary>
    Task<IEnumerable<MovimientoCaja>> GetPorTurnosAsync(IReadOnlyCollection<Guid> turnoCajaIds, CancellationToken ct = default);
}
