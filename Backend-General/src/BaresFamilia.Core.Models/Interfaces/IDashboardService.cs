using BaresFamilia.Core.Models.Contratos.Dashboard;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de métricas del dashboard del Backoffice.
/// Flujo: DashboardController → IDashboardService → DashboardService → IDashboardRepository → DashboardRepository.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Calcula las métricas principales del rango junto con su variación respecto
    /// al período inmediatamente anterior de igual duración.
    /// </summary>
    Task<EstadisticasDashboard> GetEstadisticasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default);

    /// <summary>Ventas y comandas cobradas agrupadas por turno.</summary>
    Task<IEnumerable<VentasDeTurno>> GetVentasPorTurnoAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default);

    /// <summary>
    /// Distribución del volumen de ventas por hora, en porcentaje relativo a la hora pico.
    /// Devuelve un valor por cada hora solicitada, con 0 donde no hubo ventas.
    /// </summary>
    Task<IReadOnlyList<int>> GetDistribucionHorariaAsync(Guid? sucursalId, RangoFechas rango, IReadOnlyList<int> horas, CancellationToken ct = default);

    /// <summary>Últimas anulaciones del rango.</summary>
    Task<IEnumerable<AnulacionAuditada>> GetUltimasAnulacionesAsync(Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default);

    /// <summary>Últimas comandas con descuento del rango.</summary>
    Task<IEnumerable<DescuentoAuditado>> GetUltimosDescuentosAsync(Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default);

    /// <summary>Total cobrado por medio de pago.</summary>
    Task<IEnumerable<TotalPorMedioPago>> GetTotalesPorMedioPagoAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default);

    /// <summary>Productos más vendidos del rango.</summary>
    Task<IEnumerable<ProductoVendido>> GetTopProductosAsync(Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default);
}
