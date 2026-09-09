using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador en la Nube para calcular estadísticas y métricas del Dashboard.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly NubeContext _context;

    public DashboardController(NubeContext context)
    {
        _context = context;
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
    private static (DateTime desde, DateTime hasta) ResolveRango(DateTime? desde, DateTime? hasta)
    {
        var now = DateTime.UtcNow;
        var startOfToday = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var endOfToday = startOfToday.AddDays(1).AddTicks(-1);
        // Los DateTime? que llegan por query string tienen Kind=Unspecified; hay que forzar Utc
        // antes de usarlos en comparaciones EF Core contra columnas timestamptz.
        return (desde.AsUtc() ?? startOfToday, hasta.AsUtc() ?? endOfToday);
    }

    /// <summary>
    /// Obtiene las 4 tarjetas de estadísticas principales.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var sucId = ResolveSucursalId(sucursalId);
        var comandas = _context.Comandas.AsQueryable();
        if (sucId.HasValue)
            comandas = comandas.Where(c => c.Usuario.SucursalId == sucId.Value);

        var (rangoDesde, rangoHasta) = ResolveRango(desde, hasta);
        // Período previo de igual duración, inmediatamente anterior al rango elegido, para calcular variación %.
        var duracion = rangoHasta - rangoDesde;
        var rangoPrevioDesde = rangoDesde - duracion;
        var rangoPrevioHasta = rangoDesde;

        // 1. Ventas Totales (en el rango, por fecha contable del turno)
        var totalSales = await comandas
            .Where(c => c.Estado == ComandaEstado.Cobrada && c.FechaContable >= rangoDesde && c.FechaContable <= rangoHasta)
            .SumAsync(c => (double?)c.Total, ct) ?? 0.0;

        var previousRangeSales = await comandas
            .Where(c => c.Estado == ComandaEstado.Cobrada && c.FechaContable >= rangoPrevioDesde && c.FechaContable < rangoPrevioHasta)
            .SumAsync(c => (double?)c.Total, ct) ?? 0.0;

        double salesChange = 0.0;
        if (previousRangeSales > 0)
        {
            salesChange = Math.Round(((totalSales - previousRangeSales) / previousRangeSales) * 100, 1);
        }

        // 2. Comandas en el rango (por fecha contable del turno) — no cuenta las anuladas, esas tienen su propio contador.
        var comandasRango = await comandas
            .CountAsync(c => c.Estado != ComandaEstado.Anulada && c.FechaContable >= rangoDesde && c.FechaContable <= rangoHasta, ct);

        var comandasRangoPrevio = await comandas
            .CountAsync(c => c.Estado != ComandaEstado.Anulada && c.FechaContable >= rangoPrevioDesde && c.FechaContable < rangoPrevioHasta, ct);

        double comandasChange = 0.0;
        if (comandasRangoPrevio > 0)
        {
            comandasChange = Math.Round(((double)(comandasRango - comandasRangoPrevio) / comandasRangoPrevio) * 100, 1);
        }

        // 2b. Comandas anuladas en el rango, con su propia variación vs. período anterior.
        var comandasAnuladasRango = await comandas
            .CountAsync(c => c.Estado == ComandaEstado.Anulada && c.FechaContable >= rangoDesde && c.FechaContable <= rangoHasta, ct);

        var comandasAnuladasRangoPrevio = await comandas
            .CountAsync(c => c.Estado == ComandaEstado.Anulada && c.FechaContable >= rangoPrevioDesde && c.FechaContable < rangoPrevioHasta, ct);

        double comandasAnuladasChange = 0.0;
        if (comandasAnuladasRangoPrevio > 0)
        {
            comandasAnuladasChange = Math.Round(((double)(comandasAnuladasRango - comandasAnuladasRangoPrevio) / comandasAnuladasRangoPrevio) * 100, 1);
        }

        // 3. Ticket Promedio (en el rango)
        var totalComandasCobradas = await comandas
            .CountAsync(c => c.Estado == ComandaEstado.Cobrada && c.FechaContable >= rangoDesde && c.FechaContable <= rangoHasta, ct);

        double ticketPromedio = totalComandasCobradas > 0 ? totalSales / totalComandasCobradas : 0.0;

        // Ticket Promedio del período anterior
        var previousRangeComandasCount = await comandas
            .CountAsync(c => c.Estado == ComandaEstado.Cobrada && c.FechaContable >= rangoPrevioDesde && c.FechaContable < rangoPrevioHasta, ct);
        double ticketPromedioPrevRango = previousRangeComandasCount > 0 ? previousRangeSales / previousRangeComandasCount : 0.0;

        double ticketChange = 0.0;
        if (ticketPromedioPrevRango > 0)
        {
            ticketChange = Math.Round(((ticketPromedio - ticketPromedioPrevRango) / ticketPromedioPrevRango) * 100, 1);
        }

        var displaySales = $"${totalSales:N0}";
        var displayComandas = comandasRango.ToString();
        var displayTicket = $"${ticketPromedio:N0}";

        // 4. Cuarta tarjeta: en vista global, "Sucursales Activas"; en vista de una sucursal, su nombre.
        object cuartaTarjeta;
        if (sucId.HasValue)
        {
            var sucursal = await _context.Sucursales.FirstOrDefaultAsync(s => s.Id == sucId.Value, ct);
            cuartaTarjeta = new
            {
                title = "Sucursal",
                value = sucursal?.Nombre ?? "-",
                change = 0.0,
                iconName = "Building2",
                color = "#8b5cf6"
            };
        }
        else
        {
            var totalSucursales = await _context.Sucursales.CountAsync(ct);
            var activasSucursales = await _context.Sucursales.CountAsync(s => s.IsActive, ct);
            cuartaTarjeta = new
            {
                title = "Sucursales Activas",
                value = $"{activasSucursales} / {totalSucursales}",
                change = 0.0,
                iconName = "Building2",
                color = "#8b5cf6"
            };
        }

        var response = new object[]
        {
            new
            {
                title = "Ventas Totales",
                value = displaySales,
                change = salesChange,
                iconName = "DollarSign",
                color = "#10b981"
            },
            new
            {
                title = "Comandas",
                value = displayComandas,
                change = comandasChange,
                iconName = "ShoppingCart",
                color = "#3b82f6"
            },
            new
            {
                title = "Ticket Promedio",
                value = displayTicket,
                change = ticketChange,
                iconName = "TrendingUp",
                color = "#f59e0b"
            },
            new
            {
                title = "Comandas Anuladas",
                value = comandasAnuladasRango.ToString(),
                change = comandasAnuladasChange,
                iconName = "Ban",
                color = "#ef4444",
                // Acá subir es una mala noticia: el signo de "change" se interpreta al revés que en el resto de las tarjetas.
                invertirColor = true
            },
            cuartaTarjeta
        };

        return Ok(response);
    }

    /// <summary>
    /// Obtiene el diferencial de ventas entre el turno AM y el turno PM dentro del
    /// rango de fechas contables seleccionado.
    /// </summary>
    [HttpGet("turnos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVentasPorTurno([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var sucId = ResolveSucursalId(sucursalId);
        var (rangoDesde, rangoHasta) = ResolveRango(desde, hasta);

        var comandas = _context.Comandas
            .Where(c => c.Estado == ComandaEstado.Cobrada && c.FechaContable >= rangoDesde && c.FechaContable <= rangoHasta);
        if (sucId.HasValue)
            comandas = comandas.Where(c => c.Usuario.SucursalId == sucId.Value);

        var porTurno = await comandas
            .GroupBy(c => c.Turno)
            .Select(g => new { turno = g.Key, ventas = g.Sum(c => c.Total), comandas = g.Count() })
            .ToListAsync(ct);

        double ventasAm = (double)(porTurno.FirstOrDefault(t => t.turno == "AM")?.ventas ?? 0);
        double ventasPm = (double)(porTurno.FirstOrDefault(t => t.turno == "PM")?.ventas ?? 0);
        int comandasAm = porTurno.FirstOrDefault(t => t.turno == "AM")?.comandas ?? 0;
        int comandasPm = porTurno.FirstOrDefault(t => t.turno == "PM")?.comandas ?? 0;
        // Comandas sincronizadas antes de vincularse a un turno (dato legado o sin turno abierto al crearse).
        double ventasSinTurno = porTurno.Where(t => t.turno != "AM" && t.turno != "PM").Sum(t => (double)t.ventas);
        int comandasSinTurno = porTurno.Where(t => t.turno != "AM" && t.turno != "PM").Sum(t => t.comandas);

        double total = ventasAm + ventasPm + ventasSinTurno;
        double diferencia = ventasAm - ventasPm;
        double diferenciaPorcentual = ventasPm > 0 ? Math.Round((diferencia / ventasPm) * 100, 1) : (ventasAm > 0 ? 100.0 : 0.0);

        return Ok(new
        {
            am = new { ventas = ventasAm, comandas = comandasAm, porcentaje = total > 0 ? Math.Round(ventasAm / total * 100, 1) : 0.0 },
            pm = new { ventas = ventasPm, comandas = comandasPm, porcentaje = total > 0 ? Math.Round(ventasPm / total * 100, 1) : 0.0 },
            sinTurno = new { ventas = ventasSinTurno, comandas = comandasSinTurno },
            diferencia,
            diferenciaPorcentual
        });
    }

    /// <summary>
    /// Obtiene la distribución horaria de volumen de ventas para el gráfico de barras.
    /// </summary>
    [HttpGet("heatmap")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHeatmap([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var sucId = ResolveSucursalId(sucursalId);
        var (rangoDesde, rangoHasta) = ResolveRango(desde, hasta);

        // El rango filtra por fecha contable del turno; la hora del gráfico usa el timestamp real de creación.
        var comandasQuery = _context.Comandas
            .Where(c => c.Estado == ComandaEstado.Cobrada && c.FechaContable >= rangoDesde && c.FechaContable <= rangoHasta);
        if (sucId.HasValue)
            comandasQuery = comandasQuery.Where(c => c.Usuario.SucursalId == sucId.Value);

        var comandas = await comandasQuery
            .Select(c => new { c.CreatedAt.Hour })
            .ToListAsync(ct);

        var defaultHours = new[] { "18:00", "19:00", "20:00", "21:00", "22:00", "23:00", "00:00", "01:00" };

        if (!comandas.Any())
        {
            var emptyResponse = defaultHours.Select(h => new { hora = h, valor = 0 });
            return Ok(emptyResponse);
        }

        // Agrupar por hora
        var hourlyCounts = comandas
            .GroupBy(c => c.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        // Mapear a intervalos del dashboard
        var result = new List<object>();
        var targetHours = new[] { 18, 19, 20, 21, 22, 23, 0, 1 };

        int maxCount = hourlyCounts.Values.Any() ? hourlyCounts.Values.Max() : 1;

        for (int i = 0; i < targetHours.Length; i++)
        {
            int hour = targetHours[i];
            int count = hourlyCounts.TryGetValue(hour, out var val) ? val : 0;
            // Calcular porcentaje relativo al pico
            int percentValue = (int)Math.Round((double)count / maxCount * 100);

            result.Add(new { hora = defaultHours[i], valor = percentValue });
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtiene las últimas alertas y auditorías de caja.
    /// </summary>
    [HttpGet("auditoria")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditoria([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var sucId = ResolveSucursalId(sucursalId);
        var (rangoDesde, rangoHasta) = ResolveRango(desde, hasta);
        var auditorias = new List<object>();

        // 1. Obtener anulaciones recientes (comanda completa o ítem individual): el
        // estado de anulación vive directamente en ComandaItem, no en una tabla aparte.
        // El rango filtra por la fecha contable del turno de la comanda a la que pertenece el ítem.
        var anulacionesQuery = _context.Set<ComandaItem>()
            .Include(i => i.AnuladoPorUsuario)
            .Where(i => i.Cancelado && i.IsActive && i.Comanda.FechaContable >= rangoDesde && i.Comanda.FechaContable <= rangoHasta);
        if (sucId.HasValue)
            anulacionesQuery = anulacionesQuery.Where(i => i.AnuladoPorUsuario != null && i.AnuladoPorUsuario.SucursalId == sucId.Value);

        var anulaciones = await anulacionesQuery
            .OrderByDescending(i => i.FechaAnulacion)
            .Take(3)
            .ToListAsync(ct);

        foreach (var i in anulaciones)
        {
            auditorias.Add(new
            {
                tipo = "Anulación",
                comanda = $"#{i.ComandaId.ToString()[..8]}",
                detalle = i.MotivoAnulacion ?? "",
                gerente = i.AnuladoPorUsuario?.Nombre ?? "—",
                hora = (i.FechaAnulacion ?? i.UpdatedAt).ToString("HH:mm")
            });
        }

        // 2. Obtener comandas con descuentos grandes (por fecha contable del turno)
        var descuentosQuery = _context.Comandas.Where(c => c.Descuento > 0 && c.FechaContable >= rangoDesde && c.FechaContable <= rangoHasta);
        if (sucId.HasValue)
            descuentosQuery = descuentosQuery.Where(c => c.Usuario.SucursalId == sucId.Value);

        var descuentos = await descuentosQuery
            .OrderByDescending(c => c.CreatedAt)
            .Take(3)
            .ToListAsync(ct);

        foreach (var c in descuentos)
        {
            auditorias.Add(new
            {
                tipo = "Descuento",
                comanda = $"#{Math.Abs(c.Id.GetHashCode()) % 9000 + 1000}",
                detalle = $"{Math.Round((c.Descuento / (c.Subtotal > 0 ? c.Subtotal : 1)) * 100)}% Cortesía / Descuento",
                gerente = "A. García",
                hora = c.CreatedAt.ToString("HH:mm")
            });
        }

        // 3. Si no hay auditorías reales, retornar vacío
        if (auditorias.Count == 0)
        {
            return Ok(new List<object>());
        }

        return Ok(auditorias.OrderByDescending(a => (string)a.GetType().GetProperty("hora")?.GetValue(a)!).Take(5));
    }

    /// <summary>
    /// Obtiene el total cobrado por cada medio de pago dentro del rango de fechas contables seleccionado.
    /// </summary>
    [HttpGet("medios-pago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMediosPago([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var sucId = ResolveSucursalId(sucursalId);
        var (rangoDesde, rangoHasta) = ResolveRango(desde, hasta);

        var pagos = _context.Pagos
            .Where(p => p.Comanda.Estado == ComandaEstado.Cobrada
                && p.Comanda.FechaContable >= rangoDesde && p.Comanda.FechaContable <= rangoHasta);
        if (sucId.HasValue)
            pagos = pagos.Where(p => p.Comanda.Usuario.SucursalId == sucId.Value);

        var result = await pagos
            .GroupBy(p => new { p.MetodoPagoId, p.MetodoPago.Nombre })
            .Select(g => new
            {
                medioPago = g.Key.Nombre,
                monto = g.Sum(p => p.Monto),
                operaciones = g.Count()
            })
            .OrderByDescending(g => g.monto)
            .ToListAsync(ct);

        return Ok(result);
    }

    /// <summary>
    /// Obtiene los 10 productos más vendidos (por cantidad) dentro del rango de fechas contables
    /// seleccionado. No cuenta ítems anulados.
    /// </summary>
    [HttpGet("top-productos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopProductos([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var sucId = ResolveSucursalId(sucursalId);
        var (rangoDesde, rangoHasta) = ResolveRango(desde, hasta);

        var items = _context.ComandaItems
            .Where(i => !i.Cancelado && i.IsActive
                && i.Comanda.Estado == ComandaEstado.Cobrada
                && i.Comanda.FechaContable >= rangoDesde && i.Comanda.FechaContable <= rangoHasta);
        if (sucId.HasValue)
            items = items.Where(i => i.Comanda.Usuario.SucursalId == sucId.Value);

        var result = await items
            .GroupBy(i => new { i.ProductoId, i.Producto.Nombre })
            .Select(g => new
            {
                producto = g.Key.Nombre,
                cantidad = g.Sum(i => i.Cantidad),
                monto = g.Sum(i => i.Cantidad * i.PrecioUnitario)
            })
            .OrderByDescending(g => g.cantidad)
            .Take(10)
            .ToListAsync(ct);

        return Ok(result);
    }
}
