namespace BaresFamilia.Core.Models.Contratos.Dashboard;

/// <summary>
/// Rango de fechas contables sobre el que se calculan las métricas.
/// </summary>
public record RangoFechas(DateTime Desde, DateTime Hasta)
{
    /// <summary>
    /// Período inmediatamente anterior de igual duración, contra el que se compara
    /// para obtener la variación porcentual de cada métrica.
    /// </summary>
    public RangoFechas PeriodoPrevio()
    {
        var duracion = Hasta - Desde;

        // El extremo superior se corre un tick hacia atrás para que el instante
        // inicial del rango actual no quede contado también en el período previo.
        return new RangoFechas(Desde - duracion, Desde.AddTicks(-1));
    }
}

/// <summary>
/// Una métrica del dashboard junto con su variación respecto al período anterior.
/// </summary>
public record Metrica(decimal Valor, double VariacionPorcentual);

/// <summary>
/// Métricas principales del dashboard para un rango y una sucursal (o todas).
/// </summary>
public record EstadisticasDashboard(
    Metrica Ventas,
    Metrica Comandas,
    Metrica ComandasAnuladas,
    Metrica TicketPromedio,
    AlcanceSucursales Sucursales);

/// <summary>
/// Contexto de sucursal de las métricas: o bien se está viendo una sucursal puntual
/// (y se muestra su nombre) o la vista consolidada (y se muestra cuántas hay activas).
/// </summary>
public record AlcanceSucursales(string? SucursalNombre, int Activas, int Totales);

/// <summary>
/// Ventas y cantidad de comandas de un turno. Turno queda en null para las
/// comandas que se sincronizaron sin haber quedado vinculadas a uno.
/// </summary>
public record VentasDeTurno(string? Turno, decimal Ventas, int Comandas);

/// <summary>
/// Total cobrado con un medio de pago.
/// </summary>
public record TotalPorMedioPago(string MedioPago, decimal Monto, int Operaciones);

/// <summary>
/// Producto vendido con su volumen y facturación.
/// </summary>
public record ProductoVendido(string Producto, int Cantidad, decimal Monto);

/// <summary>
/// Anulación registrada, para el panel de auditoría.
/// </summary>
public record AnulacionAuditada(Guid ComandaId, string? Motivo, string? UsuarioNombre, DateTime Fecha);

/// <summary>
/// Comanda con descuento aplicado, para el panel de auditoría.
/// </summary>
public record DescuentoAuditado(Guid ComandaId, decimal Descuento, decimal Subtotal, DateTime Fecha);
