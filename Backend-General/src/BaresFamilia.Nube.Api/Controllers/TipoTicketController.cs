using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

[ApiController]
[Route("api/tipo-ticket")]
public class TipoTicketController : ControllerBase
{
    private readonly IService<TipoTicket> _tipoTicketService;

    public TipoTicketController(IService<TipoTicket> tipoTicketService)
    {
        _tipoTicketService = tipoTicketService;
    }

    /// <summary>
    /// Obtiene todos los tipos de ticket con su template.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tipos = await _tipoTicketService.GetAllAsync(ct);
        return Ok(tipos);
    }

    /// <summary>
    /// Obtiene un tipo de ticket por su ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tipo = await _tipoTicketService.GetByIdAsync(id, ct);
        if (tipo is null)
            return NotFound(new { message = $"Tipo de ticket '{id}' no encontrado." });
        return Ok(tipo);
    }

    /// <summary>
    /// Actualiza el template de un tipo de ticket.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ActualizarTipoTicketRequest request, CancellationToken ct)
    {
        var tipo = await _tipoTicketService.GetByIdAsync(id, ct);
        if (tipo is null)
            return NotFound(new { message = $"Tipo de ticket '{id}' no encontrado." });

        tipo.Nombre = request.Nombre;
        tipo.TemplateContenido = request.TemplateContenido;
        tipo.UpdatedAt = DateTime.UtcNow;

        await _tipoTicketService.UpdateAsync(tipo, ct);
        return Ok(tipo);
    }
}

public record ActualizarTipoTicketRequest(string Nombre, string TemplateContenido);
