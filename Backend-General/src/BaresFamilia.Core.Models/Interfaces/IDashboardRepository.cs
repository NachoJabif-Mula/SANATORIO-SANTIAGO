using BaresFamilia.Core.Models.Contratos.Dashboard;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Consultas de agregación del dashboard del Backoffice. Todas filtran por fecha
/// contable del turno y, opcionalmente, por sucursal.
/// Es de solo lectura y cruza varias entidades, por eso no extiende IRepository.
/// </summary>
public interface IDashboardRepository
{
    /// <summary>Suma el total de las comandas cobradas del rango.</summary>
    Task<decimal> GetTotalVentasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default);

    /// <summary>Cuenta las comandas del rango excluyendo las anuladas.</summary>
    Task<int> ContarComandasNoAnuladasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default);

    /// <summary>Cuenta las comandas anuladas del rango.</summary>
    Task<int> ContarComandasAnuladasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default);

    /// <summary>Cuenta las comandas cobradas del rango, base del ticket promedio.</summary>
    Task<int> ContarComandasCobradasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default);

    /// <summary>Ventas y comandas cobradas agrupadas por turno.</summary>
    Task<IEnumerable<VentasDeTurno>> GetVentasPorTurnoAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default);

    /// <summary>
    /// Hora de creación de cada comanda cobrada del rango, para la distribución horaria.
    /// El rango filtra por fecha contable, pero la hora sale del timestamp real.
    /// </summary>
    Task<IEnumerable<int>> GetHorasDeComandasCobradasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default);

    /// <summary>Últimas anulaciones del rango, de la más reciente a la más antigua.</summary>
    Task<IEnumerable<AnulacionAuditada>> GetUltimasAnulacionesAsync(Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default);

    /// <summary>Últimas comandas con descuento del rango.</summary>
    Task<IEnumerable<DescuentoAuditado>> GetUltimosDescuentosAsync(Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default);

    /// <summary>Total cobrado por medio de pago, de mayor a menor.</summary>
    Task<IEnumerable<TotalPorMedioPago>> GetTotalesPorMedioPagoAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default);

    /// <summary>Productos más vendidos por cantidad, sin contar ítems anulados.</summary>
    Task<IEnumerable<ProductoVendido>> GetTopProductosAsync(Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default);

    /// <summary>Nombre de una sucursal, incluso si está dada de baja. Null si no existe.</summary>
    Task<string?> GetNombreSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>Cantidad de sucursales activas y totales.</summary>
    Task<AlcanceSucursales> ContarSucursalesAsync(CancellationToken ct = default);
}
