using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador en la Nube para la consulta de comandas / reportes de ventas.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComandaController : ControllerBase
{
    private readonly NubeContext _context;

    public ComandaController(NubeContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene todas las comandas sincronizadas en la Nube con su detalle proyectado.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var comandas = await _context.Comandas
            .AsNoTracking()
            .Include(c => c.Mesa)
                .ThenInclude(m => m.Sucursal)
            .Include(c => c.Usuario)
                .ThenInclude(u => u.Sucursal)
            .Include(c => c.TipoVenta)
            .Include(c => c.Items)
            .Include(c => c.Pagos)
                .ThenInclude(p => p.MetodoPago)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        var report = comandas.Select(c =>
        {
            var sucursalNombre = c.Mesa?.Sucursal?.Nombre ?? c.Usuario?.Sucursal?.Nombre ?? "Nube";
            var metodoPagoNombre = c.Pagos.FirstOrDefault()?.MetodoPago?.Nombre ?? "Efectivo";
            // Generar un número de comanda determinista a partir del Guid
            var comandaNumero = Math.Abs(c.Id.GetHashCode()) % 9000 + 1000;

            return new
            {
                id = c.Id.ToString(),
                fecha = c.CreatedAt.ToString("o"),
                sucursal = sucursalNombre,
                comandaNumero = comandaNumero,
                tipoVenta = c.TipoVenta?.Nombre ?? "Mostrador",
                items = c.Items.Sum(i => i.Cantidad),
                subtotal = c.Subtotal,
                descuento = c.Descuento,
                total = c.Total,
                metodoPago = metodoPagoNombre,
                estado = c.Estado.ToString(),
                syncEstado = "Sincronizado"
            };
        });

        return Ok(report);
    }
}
