using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de consulta de cierres diarios consolidados en la Nube.
/// Flujo: CierreDiarioController → ICierreDiarioService → CierreDiarioService → ICierreDiarioRepository → CierreDiarioRepository.
/// </summary>
public interface ICierreDiarioService : IService<CierreDiario>
{
    /// <summary>
    /// Obtiene los cierres diarios con caja y usuario de cierre cargados, filtrando
    /// por sucursal y rango de fechas, del más nuevo al más viejo.
    /// </summary>
    Task<IEnumerable<CierreDiario>> GetConDetallesAsync(Guid? sucursalId, DateTime? desde, DateTime? hasta, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el detalle de un cierre diario.
    /// </summary>
    Task<CierreDiario?> GetPorIdConDetallesAsync(Guid id, CancellationToken ct = default);
}
