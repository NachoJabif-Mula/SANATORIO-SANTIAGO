using BaresFamilia.Core.Models.Contratos.Sincronizacion;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Tipos de datos que una sucursal descarga de la Nube.
/// </summary>
public enum TipoPull
{
    Config,
    Mesas,
    Roles,
    Usuarios
}

/// <summary>
/// Tipos de datos que una sucursal sube a la Nube.
/// </summary>
public enum TipoPush
{
    Comandas,
    Pagos,
    Movimientos,
    CierresDiarios
}

/// <summary>
/// Monitor en memoria de la actividad de sincronización de las sucursales.
///
/// Es estado operativo, no de negocio: vive mientras corre el proceso de la Nube
/// (salvo los intervalos, que sí se persisten a disco para sobrevivir un reinicio).
/// Se registra como singleton porque lo comparten todas las requests.
/// </summary>
public interface IMonitorSincronizacion
{
    /// <summary>Registra que una sucursal subió datos.</summary>
    void RegistrarPush(Guid sucursalId, string sucursalNombre, TipoPush tipo);

    /// <summary>Registra que una sucursal descargó datos.</summary>
    void RegistrarPull(Guid sucursalId, TipoPull tipo);

    /// <summary>Agrega una entrada al historial reciente, descartando las más viejas.</summary>
    void AgregarRegistro(Guid sucursalId, string sucursalNombre, string tipo, string mensaje, bool exitoso);

    /// <summary>Historial reciente, del más nuevo al más viejo.</summary>
    IReadOnlyList<RegistroSincronizacion> GetRegistrosRecientes(int cantidad);

    /// <summary>Estado de sincronización de una sucursal, creándolo si es la primera vez.</summary>
    EstadoSincronizacionSucursal GetEstado(Guid sucursalId, string sucursalNombre);

    /// <summary>Marca que la sucursal debe sincronizar en cuanto consulte.</summary>
    void SolicitarSincronizacionForzada(Guid sucursalId);

    /// <summary>Indica si hay una sincronización forzada pendiente, sin consumirla.</summary>
    bool TieneSincronizacionForzadaPendiente(Guid sucursalId);

    /// <summary>Consume la solicitud de sincronización forzada, si la hay.</summary>
    bool ConsumirSincronizacionForzada(Guid sucursalId);

    /// <summary>Intervalo de sincronización configurado para la sucursal, en segundos.</summary>
    int GetIntervaloSegundos(Guid sucursalId);

    /// <summary>Configura el intervalo de sincronización y lo persiste.</summary>
    void SetIntervaloSegundos(Guid sucursalId, int segundos);

    /// <summary>Registra que un dispositivo POS sigue en línea.</summary>
    void RegistrarLatidoDeDispositivo(Guid sucursalId, Guid dispositivoId);

    /// <summary>
    /// Cantidad de dispositivos de la sucursal vistos dentro de la ventana de actividad,
    /// calculada a partir del intervalo configurado.
    /// </summary>
    int ContarDispositivosConectados(Guid sucursalId);
}
