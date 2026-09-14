using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para PrintJob (cola de impresión no fiscal y fiscal).
/// </summary>
public interface IPrintJobRepository : IRepository<PrintJob>
{
    /// <summary>
    /// Obtiene un trabajo por ID sin filtrar por IsActive: el bridge puede reportar
    /// el resultado de un trabajo que ya fue dado de baja lógica.
    /// </summary>
    Task<PrintJob?> GetPorIdIncluyendoInactivosAsync(Guid id, CancellationToken ct = default);
}
