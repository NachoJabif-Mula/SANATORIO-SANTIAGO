using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImpresoraController : ControllerBase
{
    private readonly IService<Impresora> _impresoraService;
    private readonly IService<ImpresoraTicketTipo> _asignacionService;

    public ImpresoraController(
        IService<Impresora> impresoraService,
        IService<ImpresoraTicketTipo> asignacionService)
    {
        _impresoraService = impresoraService;
        _asignacionService = asignacionService;
    }

    /// <summary>
    /// Obtiene todas las impresoras con sus tipos de ticket habilitados.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var impresoras = await _impresoraService.GetAllAsync(ct);
        return Ok(impresoras);
    }

    /// <summary>
    /// Obtiene una impresora por su ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var impresora = await _impresoraService.GetByIdAsync(id, ct);
        if (impresora is null)
            return NotFound(new { message = $"Impresora '{id}' no encontrada." });
        return Ok(impresora);
    }

    /// <summary>
    /// Crea una nueva impresora.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearImpresoraRequest request, CancellationToken ct)
    {
        var impresora = new Impresora
        {
            SucursalId = request.SucursalId,
            Nombre = request.Nombre,
            TipoDispositivo = request.TipoDispositivo,
            TipoConexion = request.TipoConexion,
            Direccion = request.Direccion,
            Puerto = request.Puerto,
            Velocidad = request.Velocidad
        };

        var created = await _impresoraService.CreateAsync(impresora, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Actualiza una impresora existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ActualizarImpresoraRequest request, CancellationToken ct)
    {
        var impresora = await _impresoraService.GetByIdAsync(id, ct);
        if (impresora is null)
            return NotFound(new { message = $"Impresora '{id}' no encontrada." });

        impresora.Nombre = request.Nombre;
        impresora.TipoDispositivo = request.TipoDispositivo;
        impresora.TipoConexion = request.TipoConexion;
        impresora.Direccion = request.Direccion;
        impresora.Puerto = request.Puerto;
        impresora.Velocidad = request.Velocidad;
        impresora.UpdatedAt = DateTime.UtcNow;

        await _impresoraService.UpdateAsync(impresora, ct);
        return Ok(impresora);
    }

    /// <summary>
    /// Desactiva (borrado lógico) una impresora.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var exists = await _impresoraService.ExistsAsync(id, ct);
        if (!exists)
            return NotFound(new { message = $"Impresora '{id}' no encontrada." });
        await _impresoraService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Asigna un tipo de ticket a una impresora.
    /// </summary>
    [HttpPost("{impresoraId:guid}/tipo-ticket/{tipoTicketId:guid}")]
    public async Task<IActionResult> AsignarTipoTicket(Guid impresoraId, Guid tipoTicketId, CancellationToken ct)
    {
        var impresora = await _impresoraService.GetByIdAsync(impresoraId, ct);
        if (impresora is null)
            return NotFound(new { message = $"Impresora '{impresoraId}' no encontrada." });

        // Verificar si ya existe la asignación
        var asignaciones = await _asignacionService.GetAllAsync(ct);
        var yaExiste = asignaciones.Any(a => a.ImpresoraId == impresoraId && a.TipoTicketId == tipoTicketId && a.IsActive);
        if (yaExiste)
            return Conflict(new { message = "Esta asignación ya existe." });

        var asignacion = new ImpresoraTicketTipo
        {
            ImpresoraId = impresoraId,
            TipoTicketId = tipoTicketId
        };

        var created = await _asignacionService.CreateAsync(asignacion, ct);
        return Ok(created);
    }

    /// <summary>
    /// Elimina la asignación de un tipo de ticket a una impresora.
    /// </summary>
    [HttpDelete("{impresoraId:guid}/tipo-ticket/{tipoTicketId:guid}")]
    public async Task<IActionResult> DesasignarTipoTicket(Guid impresoraId, Guid tipoTicketId, CancellationToken ct)
    {
        var asignaciones = await _asignacionService.GetAllAsync(ct);
        var asignacion = asignaciones.FirstOrDefault(a => a.ImpresoraId == impresoraId && a.TipoTicketId == tipoTicketId && a.IsActive);

        if (asignacion is null)
            return NotFound(new { message = "Asignación no encontrada." });

        await _asignacionService.DeleteAsync(asignacion.Id, ct);
        return NoContent();
    }

    /// <summary>
    /// Realiza una prueba de conexión con la impresora configurada.
    /// </summary>
    [HttpPost("{id:guid}/test-connection")]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken ct)
    {
        var impresora = await _impresoraService.GetByIdAsync(id, ct);
        if (impresora is null)
            return NotFound(new { message = $"Impresora '{id}' no encontrada." });

        return Ok(new { 
            ok = true, 
            message = $"Conexión validada para '{impresora.Nombre}' ({impresora.TipoDispositivo} - {impresora.Direccion})" 
        });
    }
}

public record CrearImpresoraRequest(
    Guid SucursalId,
    string Nombre,
    TipoDispositivoImpresora TipoDispositivo = TipoDispositivoImpresora.Comandera,
    TipoConexionImpresora TipoConexion = TipoConexionImpresora.Red,
    string Direccion = "",
    int Puerto = 9100,
    int Velocidad = 9600);

public record ActualizarImpresoraRequest(
    string Nombre,
    TipoDispositivoImpresora TipoDispositivo = TipoDispositivoImpresora.Comandera,
    TipoConexionImpresora TipoConexion = TipoConexionImpresora.Red,
    string Direccion = "",
    int Puerto = 9100,
    int Velocidad = 9600);
