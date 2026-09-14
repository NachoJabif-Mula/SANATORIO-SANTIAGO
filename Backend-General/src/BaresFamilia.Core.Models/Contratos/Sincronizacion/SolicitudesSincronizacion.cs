namespace BaresFamilia.Core.Models.Contratos.Sincronizacion;

/// <summary>
/// Cambio del intervalo con el que una sucursal sincroniza.
/// </summary>
public class SetSyncIntervalRequest
{
    public int IntervalSeconds { get; set; }
}

/// <summary>
/// Reporte de actividad que una terminal envía al historial de la Nube.
/// </summary>
public class ReportLogRequest
{
    public Guid SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;

    /// <summary>"PUSH", "PULL" o "ERROR".</summary>
    public string Tipo { get; set; } = string.Empty;

    public string Mensaje { get; set; } = string.Empty;
    public bool Exitoso { get; set; }
}

/// <summary>
/// Conteos consolidados de una sucursal, para el panel de sincronización.
/// </summary>
public record ConteosSincronizacion(
    int Comandas,
    int Pagos,
    int Movimientos,
    int CierresDiarios,
    int Mesas,
    int Configuraciones,
    int Usuarios,
    int Roles);
