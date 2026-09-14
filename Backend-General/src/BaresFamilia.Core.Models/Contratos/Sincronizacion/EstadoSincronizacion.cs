namespace BaresFamilia.Core.Models.Contratos.Sincronizacion;

/// <summary>
/// Estado de sincronización de una sucursal tal como lo muestra el panel del Backoffice.
/// </summary>
public record EstadisticasSincronizacion(
    Guid SucursalId,
    string SucursalNombre,
    ConteosSincronizacion Conteos,
    EstadoSincronizacionSucursal UltimaActividad,
    int DispositivosConectados,
    bool SincronizacionForzadaPendiente,
    int IntervaloSegundos);

/// <summary>
/// Resultado de consultar si hay que sincronizar ya.
/// </summary>
public record ChequeoSincronizacion(bool ForzarSincronizacion, int IntervaloSegundos);

/// <summary>
/// Entrada del historial local de sincronización de la sucursal.
/// </summary>
public class RegistroSincronizacionLocal
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }

    /// <summary>"PUSH", "PULL", "CONFIG", "ERROR" o "HEARTBEAT".</summary>
    public string Tipo { get; set; } = string.Empty;

    public string Mensaje { get; set; } = string.Empty;
    public bool Exitoso { get; set; }
}

/// <summary>
/// Última actividad de sincronización registrada para una sucursal.
/// </summary>
public class EstadoSincronizacionSucursal
{
    public Guid SucursalId { get; set; }
    public string SucursalNombre { get; set; } = "Sucursal";
    public DateTime? LastPushComandas { get; set; }
    public DateTime? LastPushPagos { get; set; }
    public DateTime? LastPushMovimientos { get; set; }
    public DateTime? LastPushCierresDiarios { get; set; }
    public DateTime? LastPullConfig { get; set; }
    public DateTime? LastPullMesas { get; set; }
    public DateTime? LastPullRoles { get; set; }
    public DateTime? LastPullUsuarios { get; set; }
}

/// <summary>
/// Entrada del historial reciente de sincronización.
/// </summary>
public class RegistroSincronizacion
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    /// <summary>"PUSH", "PULL", "CONFIG" o "ERROR".</summary>
    public string Tipo { get; set; } = string.Empty;

    public string Mensaje { get; set; } = string.Empty;
    public bool Exitoso { get; set; }
}
