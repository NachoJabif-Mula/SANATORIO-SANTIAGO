using BaresFamilia.Core.Models.Contratos.Dashboard;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador en la Nube para calcular estadísticas y métricas del Dashboard.
/// Flujo: DashboardController → IDashboardService → DashboardService → IDashboardRepository → DashboardRepository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Backoffice")]
public class DashboardController : ControllerBase
{
    /// <summary>Franja horaria nocturna que muestra el gráfico de volumen del Backoffice.</summary>
    private static readonly int[] HorasDelGrafico = [18, 19, 20, 21, 22, 23, 0, 1];
    private static readonly string[] EtiquetasDelGrafico =
        ["18:00", "19:00", "20:00", "21:00", "22:00", "23:00", "00:00", "01:00"];

    private const int RegistrosDeAuditoriaPorTipo = 3;
    private const int RegistrosDeAuditoriaMostrados = 5;
    private const int TopProductos = 10;

    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Resuelve el sucursalId efectivo a aplicar en las consultas: si el usuario
    /// no tiene alcance global, se ignora cualquier valor recibido y se fuerza
    /// su propia sucursal. Un usuario global sin filtro recibe null (todas).
    /// </summary>
    private Guid? ResolveSucursalId(Guid? requested)
        => User.IsGlobal() ? requested : User.GetSucursalId();

    /// <summary>
    /// Resuelve el rango de fechas efectivo: si no se recibe alguno de los extremos,
    /// se usa el día de hoy (UTC) completo como valor por defecto.
    /// </summary>
    private static RangoFechas ResolveRango(DateTime? desde, DateTime? hasta)
    {
        var now = DateTime.UtcNow;
        var inicioDeHoy = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var finDeHoy = inicioDeHoy.AddDays(1).AddTicks(-1);

        // Los DateTime? que llegan por query string tienen Kind=Unspecified; hay que forzar Utc
        // antes de usarlos en comparaciones EF Core contra columnas timestamptz.
        return new RangoFechas(desde.AsUtc() ?? inicioDeHoy, hasta.AsUtc() ?? finDeHoy);
    }

    /// <summary>
    /// Obtiene las tarjetas de estadísticas principales.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var sucId = ResolveSucursalId(sucursalId);
        var stats = await _dashboardService.GetEstadisticasAsync(sucId, ResolveRango(desde, hasta), ct);

        var tarjetaSucursales = sucId.HasValue
            ? new
            {
                title = "Sucursal",
                value = stats.Sucursales.SucursalNombre ?? "-",
                change = 0.0,
                iconName = "Building2",
                color = "#8b5cf6"
            }
            : new
            {
                title = "Sucursales Activas",
                value = $"{stats.Sucursales.Activas} / {stats.Sucursales.Totales}",
                change = 0.0,
                iconName = "Building2",
                color = "#8b5cf6"
            };

        return Ok(new object[]
        {
            new
            {
                title = "Ventas Totales",
                value = $"${stats.Ventas.Valor:N0}",
                change = stats.Ventas.VariacionPorcentual,
                iconName = "DollarSign",
                color = "#10b981"
            },
            new
            {
                title = "Comandas",
                value = ((int)stats.Comandas.Valor).ToString(),
                change = stats.Comandas.VariacionPorcentual,
                iconName = "ShoppingCart",
                color = "#3b82f6"
            },
            new
            {
                title = "Ticket Promedio",
                value = $"${stats.TicketPromedio.Valor:N0}",
                change = stats.TicketPromedio.VariacionPorcentual,
                iconName = "TrendingUp",
                color = "#f59e0b"
            },
            new
            {
                title = "Comandas Anuladas",
                value = ((int)stats.ComandasAnuladas.Valor).ToString(),
                change = stats.ComandasAnuladas.VariacionPorcentual,
                iconName = "Ban",
                color = "#ef4444",
                // Acá subir es una mala noticia: el signo de "change" se interpreta al revés que en el resto de las tarjetas.
                invertirColor = true
            },
            tarjetaSucursales
        });
    }

    /// <summary>
    /// Obtiene el diferencial de ventas entre el turno AM y el turno PM dentro del
    /// rango de fechas contables seleccionado.
    /// </summary>
    [HttpGet("turnos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVentasPorTurno([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var porTurno = (await _dashboardService.GetVentasPorTurnoAsync(
            ResolveSucursalId(sucursalId), ResolveRango(desde, hasta), ct)).ToList();

        var am = porTurno.FirstOrDefault(t => t.Turno == "AM");
        var pm = porTurno.FirstOrDefault(t => t.Turno == "PM");

        double ventasAm = (double)(am?.Ventas ?? 0);
        double ventasPm = (double)(pm?.Ventas ?? 0);

        // Comandas sincronizadas antes de vincularse a un turno (dato legado o sin turno abierto al crearse).
        var sinTurno = porTurno.Where(t => t.Turno != "AM" && t.Turno != "PM").ToList();
        double ventasSinTurno = sinTurno.Sum(t => (double)t.Ventas);

        double total = ventasAm + ventasPm + ventasSinTurno;
        double diferencia = ventasAm - ventasPm;
        double diferenciaPorcentual = ventasPm > 0
            ? Math.Round(diferencia / ventasPm * 100, 1)
            : (ventasAm > 0 ? 100.0 : 0.0);

        return Ok(new
        {
            am = new { ventas = ventasAm, comandas = am?.Comandas ?? 0, porcentaje = Porcentaje(ventasAm, total) },
            pm = new { ventas = ventasPm, comandas = pm?.Comandas ?? 0, porcentaje = Porcentaje(ventasPm, total) },
            sinTurno = new { ventas = ventasSinTurno, comandas = sinTurno.Sum(t => t.Comandas) },
            diferencia,
            diferenciaPorcentual
        });
    }

    private static double Porcentaje(double parte, double total)
        => total > 0 ? Math.Round(parte / total * 100, 1) : 0.0;

    /// <summary>
    /// Obtiene la distribución horaria de volumen de ventas para el gráfico de barras.
    /// </summary>
    [HttpGet("heatmap")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHeatmap([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var valores = await _dashboardService.GetDistribucionHorariaAsync(
            ResolveSucursalId(sucursalId), ResolveRango(desde, hasta), HorasDelGrafico, ct);

        return Ok(EtiquetasDelGrafico.Select((etiqueta, i) => new { hora = etiqueta, valor = valores[i] }));
    }

    /// <summary>
    /// Obtiene las últimas alertas y auditorías de caja.
    /// </summary>
    [HttpGet("auditoria")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditoria([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var sucId = ResolveSucursalId(sucursalId);
        var rango = ResolveRango(desde, hasta);

        var anulaciones = await _dashboardService.GetUltimasAnulacionesAsync(sucId, rango, RegistrosDeAuditoriaPorTipo, ct);
        var descuentos = await _dashboardService.GetUltimosDescuentosAsync(sucId, rango, RegistrosDeAuditoriaPorTipo, ct);

        var auditorias = anulaciones
            .Select(a => new
            {
                tipo = "Anulación",
                comanda = $"#{a.ComandaId.ToString()[..8]}",
                detalle = a.Motivo ?? "",
                gerente = a.UsuarioNombre ?? "—",
                hora = a.Fecha.ToString("HH:mm")
            })
            .Concat(descuentos.Select(d => new
            {
                tipo = "Descuento",
                comanda = $"#{Math.Abs(d.ComandaId.GetHashCode()) % 9000 + 1000}",
                detalle = $"{Math.Round(d.Descuento / (d.Subtotal > 0 ? d.Subtotal : 1) * 100)}% Cortesía / Descuento",
                gerente = "A. García",
                hora = d.Fecha.ToString("HH:mm")
            }))
            .OrderByDescending(a => a.hora)
            .Take(RegistrosDeAuditoriaMostrados)
            .ToList();

        return Ok(auditorias);
    }

    /// <summary>
    /// Obtiene el total cobrado por cada medio de pago dentro del rango de fechas contables seleccionado.
    /// </summary>
    [HttpGet("medios-pago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMediosPago([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var totales = await _dashboardService.GetTotalesPorMedioPagoAsync(
            ResolveSucursalId(sucursalId), ResolveRango(desde, hasta), ct);

        return Ok(totales.Select(t => new { medioPago = t.MedioPago, monto = t.Monto, operaciones = t.Operaciones }));
    }

    /// <summary>
    /// Obtiene los productos más vendidos (por cantidad) dentro del rango de fechas contables
    /// seleccionado. No cuenta ítems anulados.
    /// </summary>
    [HttpGet("top-productos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopProductos([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var productos = await _dashboardService.GetTopProductosAsync(
            ResolveSucursalId(sucursalId), ResolveRango(desde, hasta), TopProductos, ct);

        return Ok(productos.Select(p => new { producto = p.Producto, cantidad = p.Cantidad, monto = p.Monto }));
    }
}
