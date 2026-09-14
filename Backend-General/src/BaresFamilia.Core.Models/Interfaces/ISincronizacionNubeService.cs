using BaresFamilia.Core.Models.Contratos.Sincronizacion;
using BaresFamilia.Core.Models.Dtos.Sincronizacion;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de ingesta y monitoreo de la sincronización en la Nube.
/// Flujo: SyncController → ISincronizacionNubeService → SincronizacionNubeService → ISincronizacionNubeRepository → SincronizacionNubeRepository.
/// </summary>
public interface ISincronizacionNubeService
{
    /// <summary>
    /// Procesa un lote enviado por una sucursal. sucursalIdDelToken es la sucursal
    /// declarada en el JWT; si no viene, se deduce del contenido del lote.
    /// </summary>
    Task RecibirPayloadAsync(SyncPayloadDto payload, Guid? sucursalIdDelToken, CancellationToken ct = default);

    /// <summary>Estado de sincronización de todas las sucursales activas.</summary>
    Task<IEnumerable<EstadisticasSincronizacion>> GetEstadisticasAsync(CancellationToken ct = default);

    /// <summary>Marca que una sucursal debe sincronizar en su próxima consulta.</summary>
    void ForzarSincronizacion(Guid sucursalId);

    /// <summary>
    /// Responde a la consulta periódica del POS: consume la solicitud de sincronización
    /// forzada (si la hay) y registra el latido del dispositivo.
    /// </summary>
    ChequeoSincronizacion ChequearSincronizacionForzada(Guid sucursalId, Guid? dispositivoId);

    /// <summary>Configura el intervalo de sincronización de una sucursal.</summary>
    Task ConfigurarIntervaloAsync(Guid sucursalId, int intervaloSegundos, CancellationToken ct = default);

    /// <summary>Registra en el historial un reporte enviado por una sucursal.</summary>
    Task RegistrarReporteAsync(Guid sucursalId, string? sucursalNombre, string? tipo, string? mensaje, bool exitoso, CancellationToken ct = default);

    /// <summary>
    /// Deja constancia en el historial de que falló el procesamiento de un lote.
    /// No valida la sucursal: se invoca justamente cuando la ingesta ya falló.
    /// </summary>
    void RegistrarErrorDeIngesta(string mensaje);

    /// <summary>Historial reciente de sincronización.</summary>
    IReadOnlyList<RegistroSincronizacion> GetRegistrosRecientes(int cantidad);
}
