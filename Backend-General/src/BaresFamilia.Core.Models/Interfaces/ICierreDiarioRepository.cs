using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para CierreDiario.
/// Lo usan tanto la sucursal (que genera el cierre) como la Nube
/// (que lo consulta consolidado en el backoffice).
/// </summary>
public interface ICierreDiarioRepository : IRepository<CierreDiario>
{
    /// <summary>
    /// Indica si una caja ya tiene cierre diario para esa fecha contable.
    /// </summary>
    Task<bool> ExisteParaCajaYFechaAsync(Guid cajaId, DateTime fecha, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los cierres diarios con su caja y usuario de cierre cargados,
    /// filtrando opcionalmente por sucursal y rango de fechas, del más nuevo al más viejo.
    /// </summary>
    Task<IEnumerable<CierreDiario>> GetConDetallesAsync(Guid? sucursalId, DateTime? desde, DateTime? hasta, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un cierre diario por ID con su caja y usuario de cierre cargados.
    /// </summary>
    Task<CierreDiario?> GetPorIdConDetallesAsync(Guid id, CancellationToken ct = default);
}
