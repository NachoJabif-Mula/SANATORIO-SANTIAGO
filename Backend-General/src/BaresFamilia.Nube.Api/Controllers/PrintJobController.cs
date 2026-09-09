using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// DTO provisorio para trabajos de impresión en cola (para ser reemplazado por entidad EF en Fase 5).
/// </summary>
public record PrintJobDto(
    Guid Id,
    Guid SucursalId,
    Guid ImpresoraId,
    string ImpresoraNombre,
    string Estado,
    string TipoDocumento,
    string PayloadJson,
    string? ResultadoJson,
    int Intentos,
    DateTime CreatedAt
);

/// <summary>
/// Controlador para monitoreo y gestión de la cola de trabajos de impresión.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrintJobController : ControllerBase
{
    private static readonly List<PrintJobDto> _memoriaJobs = new();

    /// <summary>
    /// Lista los trabajos de impresión en la cola.
    /// </summary>
    [HttpGet]
    public IActionResult GetAll([FromQuery] string? estado, [FromQuery] Guid? sucursalId)
    {
        var result = _memoriaJobs.AsEnumerable();
        if (!string.IsNullOrEmpty(estado) && estado.ToLower() != "todos")
        {
            result = result.Where(j => j.Estado.Equals(estado, StringComparison.OrdinalIgnoreCase));
        }
        if (sucursalId.HasValue)
        {
            result = result.Where(j => j.SucursalId == sucursalId.Value);
        }

        return Ok(result.OrderByDescending(j => j.CreatedAt));
    }

    /// <summary>
    /// Reintenta un trabajo de impresión fallido.
    /// </summary>
    [HttpPost("{id:guid}/reintentar")]
    public IActionResult Reintentar(Guid id)
    {
        var index = _memoriaJobs.FindIndex(j => j.Id == id);
        if (index >= 0)
        {
            var old = _memoriaJobs[index];
            _memoriaJobs[index] = old with { Estado = "Pendiente", Intentos = old.Intentos + 1 };
            return Ok(new { ok = true, message = "Trabajo reencolado para reintento." });
        }

        return NotFound(new { message = "Trabajo no encontrado en la cola." });
    }

    /// <summary>
    /// Limpia los trabajos impresos o finalizados de la cola.
    /// </summary>
    [HttpDelete("limpiar-finalizados")]
    public IActionResult LimpiarFinalizados()
    {
        int borrados = _memoriaJobs.RemoveAll(j => j.Estado == "Impreso");
        return Ok(new { ok = true, borrados });
    }
}
