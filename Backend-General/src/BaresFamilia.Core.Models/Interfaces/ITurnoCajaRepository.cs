using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para TurnoCaja.
/// Concentra las consultas del ciclo de vida del turno (apertura, turno en curso,
/// último turno cerrado y turnos de una fecha contable).
/// </summary>
public interface ITurnoCajaRepository : IRepository<TurnoCaja>
{
    /// <summary>
    /// Obtiene el turno abierto (sin fecha de cierre) de una caja, o null si no hay.
    /// </summary>
    Task<TurnoCaja?> GetAbiertoPorCajaAsync(Guid cajaId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el último turno cerrado de una caja, o null si nunca se cerró ninguno.
    /// Determina qué fecha contable y turno corresponde abrir a continuación.
    /// </summary>
    Task<TurnoCaja?> GetUltimoCerradoPorCajaAsync(Guid cajaId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el turno abierto más reciente de cualquier caja, con su caja y usuario cargados.
    /// </summary>
    Task<TurnoCaja?> GetAbiertoConDetallesAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene un turno activo por ID con su caja y usuario cargados.
    /// </summary>
    Task<TurnoCaja?> GetConDetallesAsync(Guid turnoId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un turno por ID con su caja y usuario cargados, sin filtrar por IsActive.
    /// El cierre de turno debe poder operar sobre un turno dado de baja lógica.
    /// </summary>
    Task<TurnoCaja?> GetConDetallesIncluyendoInactivosAsync(Guid turnoId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los turnos de una caja para una fecha contable, con su usuario cargado.
    /// </summary>
    Task<IEnumerable<TurnoCaja>> GetDeFechaContableAsync(Guid cajaId, DateTime fechaContable, CancellationToken ct = default);

    /// <summary>
    /// Indica si la caja ya tiene un turno abierto.
    /// </summary>
    Task<bool> ExisteAbiertoEnCajaAsync(Guid cajaId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un turno por ID solo si sigue abierto, o null en caso contrario.
    /// </summary>
    Task<TurnoCaja?> GetAbiertoPorIdAsync(Guid turnoId, CancellationToken ct = default);
}
