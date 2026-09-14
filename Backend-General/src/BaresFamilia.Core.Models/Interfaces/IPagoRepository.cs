using BaresFamilia.Core.Models.Contratos.Cajas;
using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para Pago.
/// </summary>
public interface IPagoRepository : IRepository<Pago>
{
    /// <summary>
    /// Agrupa los pagos de un turno por método de pago. La agrupación se resuelve
    /// en la base de datos para no traer todos los pagos a memoria.
    /// </summary>
    Task<List<DesglosePorMetodo>> GetDesglosePorMetodoDeTurnoAsync(Guid turnoCajaId, CancellationToken ct = default);

    /// <summary>
    /// Agrupa por método de pago los pagos de un conjunto de turnos (el día contable).
    /// </summary>
    Task<List<DesglosePorMetodo>> GetDesglosePorMetodoDeTurnosAsync(IReadOnlyCollection<Guid> turnoCajaIds, CancellationToken ct = default);

    /// <summary>
    /// Suma el total cobrado en un conjunto de turnos.
    /// </summary>
    Task<decimal> GetTotalDeTurnosAsync(IReadOnlyCollection<Guid> turnoCajaIds, CancellationToken ct = default);
}
