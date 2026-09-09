using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para la visualización de cierres diarios consolidados desde el backoffice.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CierreDiarioController : ControllerBase
{
    private readonly NubeContext _context;

    public CierreDiarioController(NubeContext context)
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
    /// Obtiene todos los cierres diarios consolidados.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CierreDiarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? sucursalId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var sucId = ResolveSucursalId(sucursalId);

        var query = _context.CierresDiarios
            .Include(c => c.Caja)
            .Include(c => c.UsuarioCierre)
            .Where(c => c.IsActive);

        if (sucId.HasValue)
            query = query.Where(c => c.Caja.SucursalId == sucId.Value);
        // Los DateTime? de query string llegan con Kind=Unspecified; Npgsql exige Utc contra timestamptz.
        if (desde.AsUtc() is DateTime desdeUtc)
            query = query.Where(c => c.Fecha >= desdeUtc);
        if (hasta.AsUtc() is DateTime hastaUtc)
            query = query.Where(c => c.Fecha <= hastaUtc);

        var cierres = await query
            .OrderByDescending(c => c.Fecha)
            .Select(c => new CierreDiarioDto
            {
                Id = c.Id,
                CajaId = c.CajaId,
                CajaNombre = c.Caja.Nombre,
                Fecha = c.Fecha,
                UsuarioCierreId = c.UsuarioCierreId,
                UsuarioCierreNombre = c.UsuarioCierre.Nombre,
                TotalVentas = c.TotalVentas,
                TotalEgresos = c.TotalEgresos,
                TotalNeto = c.TotalNeto,
                Observaciones = c.Observaciones,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(cierres);
    }

    /// <summary>
    /// Obtiene el detalle de un cierre diario específico.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CierreDiarioDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var closure = await _context.CierresDiarios
            .Include(c => c.Caja)
            .Include(c => c.UsuarioCierre)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);

        if (closure is null)
            return NotFound(new { message = "Cierre diario no encontrado." });

        return Ok(new CierreDiarioDetalleDto
        {
            Id = closure.Id,
            CajaId = closure.CajaId,
            CajaNombre = closure.Caja.Nombre,
            Fecha = closure.Fecha,
            UsuarioCierreId = closure.UsuarioCierreId,
            UsuarioCierreNombre = closure.UsuarioCierre.Nombre,
            TotalVentas = closure.TotalVentas,
            TotalEgresos = closure.TotalEgresos,
            TotalNeto = closure.TotalNeto,
            ResumenJson = closure.ResumenJson,
            Observaciones = closure.Observaciones,
            CreatedAt = closure.CreatedAt
        });
    }
}

public class CierreDiarioDto
{
    public Guid Id { get; set; }
    public Guid CajaId { get; set; }
    public string CajaNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public Guid UsuarioCierreId { get; set; }
    public string UsuarioCierreNombre { get; set; } = string.Empty;
    public decimal TotalVentas { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal TotalNeto { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CierreDiarioDetalleDto : CierreDiarioDto
{
    public string? ResumenJson { get; set; }
}
