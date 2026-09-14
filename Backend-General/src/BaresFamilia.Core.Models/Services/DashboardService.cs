using BaresFamilia.Core.Models.Contratos.Dashboard;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de métricas del dashboard del Backoffice.
/// Cada métrica se acompaña de su variación contra el período inmediatamente
/// anterior de igual duración.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _dashboardRepository;

    public DashboardService(IDashboardRepository dashboardRepository)
    {
        _dashboardRepository = dashboardRepository;
    }

    public async Task<EstadisticasDashboard> GetEstadisticasAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default)
    {
        var rangoPrevio = rango.PeriodoPrevio();

        var ventas = await _dashboardRepository.GetTotalVentasAsync(sucursalId, rango, ct);
        var ventasPrevias = await _dashboardRepository.GetTotalVentasAsync(sucursalId, rangoPrevio, ct);

        var comandas = await _dashboardRepository.ContarComandasNoAnuladasAsync(sucursalId, rango, ct);
        var comandasPrevias = await _dashboardRepository.ContarComandasNoAnuladasAsync(sucursalId, rangoPrevio, ct);

        var anuladas = await _dashboardRepository.ContarComandasAnuladasAsync(sucursalId, rango, ct);
        var anuladasPrevias = await _dashboardRepository.ContarComandasAnuladasAsync(sucursalId, rangoPrevio, ct);

        var cobradas = await _dashboardRepository.ContarComandasCobradasAsync(sucursalId, rango, ct);
        var cobradasPrevias = await _dashboardRepository.ContarComandasCobradasAsync(sucursalId, rangoPrevio, ct);

        var ticketPromedio = Promedio(ventas, cobradas);
        var ticketPromedioPrevio = Promedio(ventasPrevias, cobradasPrevias);

        return new EstadisticasDashboard(
            Ventas: new Metrica(ventas, Variacion(ventas, ventasPrevias)),
            Comandas: new Metrica(comandas, Variacion(comandas, comandasPrevias)),
            ComandasAnuladas: new Metrica(anuladas, Variacion(anuladas, anuladasPrevias)),
            TicketPromedio: new Metrica(ticketPromedio, Variacion(ticketPromedio, ticketPromedioPrevio)),
            Sucursales: await ResolverAlcanceSucursalesAsync(sucursalId, ct));
    }

    /// <summary>
    /// En la vista de una sucursal se muestra su nombre; en la consolidada, cuántas
    /// sucursales activas hay sobre el total.
    /// </summary>
    private async Task<AlcanceSucursales> ResolverAlcanceSucursalesAsync(Guid? sucursalId, CancellationToken ct)
    {
        if (!sucursalId.HasValue)
            return await _dashboardRepository.ContarSucursalesAsync(ct);

        var nombre = await _dashboardRepository.GetNombreSucursalAsync(sucursalId.Value, ct);
        return new AlcanceSucursales(nombre, Activas: 0, Totales: 0);
    }

    public async Task<IEnumerable<VentasDeTurno>> GetVentasPorTurnoAsync(Guid? sucursalId, RangoFechas rango, CancellationToken ct = default)
        => await _dashboardRepository.GetVentasPorTurnoAsync(sucursalId, rango, ct);

    public async Task<IReadOnlyList<int>> GetDistribucionHorariaAsync(
        Guid? sucursalId, RangoFechas rango, IReadOnlyList<int> horas, CancellationToken ct = default)
    {
        var horasConVenta = (await _dashboardRepository.GetHorasDeComandasCobradasAsync(sucursalId, rango, ct)).ToList();
        if (horasConVenta.Count == 0)
            return horas.Select(_ => 0).ToList();

        var conteoPorHora = horasConVenta
            .GroupBy(hora => hora)
            .ToDictionary(g => g.Key, g => g.Count());

        // El gráfico es relativo: la hora pico vale 100 y el resto se escala contra ella.
        var pico = conteoPorHora.Values.Max();

        return horas
            .Select(hora => conteoPorHora.TryGetValue(hora, out var cantidad) ? cantidad : 0)
            .Select(cantidad => (int)Math.Round((double)cantidad / pico * 100))
            .ToList();
    }

    public async Task<IEnumerable<AnulacionAuditada>> GetUltimasAnulacionesAsync(
        Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default)
        => await _dashboardRepository.GetUltimasAnulacionesAsync(sucursalId, rango, cantidad, ct);

    public async Task<IEnumerable<DescuentoAuditado>> GetUltimosDescuentosAsync(
        Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default)
        => await _dashboardRepository.GetUltimosDescuentosAsync(sucursalId, rango, cantidad, ct);

    public async Task<IEnumerable<TotalPorMedioPago>> GetTotalesPorMedioPagoAsync(
        Guid? sucursalId, RangoFechas rango, CancellationToken ct = default)
        => await _dashboardRepository.GetTotalesPorMedioPagoAsync(sucursalId, rango, ct);

    public async Task<IEnumerable<ProductoVendido>> GetTopProductosAsync(
        Guid? sucursalId, RangoFechas rango, int cantidad, CancellationToken ct = default)
        => await _dashboardRepository.GetTopProductosAsync(sucursalId, rango, cantidad, ct);

    /// <summary>
    /// Variación porcentual contra el período previo. Sin base de comparación
    /// (período anterior en cero) la variación se informa como 0.
    /// </summary>
    private static double Variacion(decimal actual, decimal previo)
        => previo > 0 ? Math.Round((double)((actual - previo) / previo) * 100, 1) : 0.0;

    private static decimal Promedio(decimal total, int cantidad)
        => cantidad > 0 ? total / cantidad : 0m;
}
