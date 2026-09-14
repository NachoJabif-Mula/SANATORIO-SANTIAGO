using BaresFamilia.Core.Models.Contratos.Sincronizacion;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Motor de sincronización de la sucursal, visto desde la API local.
///
/// Existe para que el controlador dependa de un contrato y no del BackgroundService
/// concreto: así el endpoint de sincronización manual se puede probar y sustituir.
/// </summary>
public interface IMotorSincronizacionLocal
{
    /// <summary>
    /// Dispara una sincronización completa fuera de ciclo. Devuelve false si ya hay
    /// una corriendo, en cuyo caso no se encola otra.
    /// </summary>
    Task<bool> DispararSincronizacionManualAsync(CancellationToken ct = default);

    /// <summary>Historial reciente de sincronización de esta terminal.</summary>
    IReadOnlyList<RegistroSincronizacionLocal> GetRegistrosRecientes();
}
