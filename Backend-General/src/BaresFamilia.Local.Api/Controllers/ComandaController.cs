using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ComandaController : ControllerBase
{
    private readonly IComandaService _comandaService;

    public ComandaController(IComandaService comandaService)
    {
        _comandaService = comandaService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var comandas = await _comandaService.GetAllWithDetailsAsync(ct);
        return Ok(comandas);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var comanda = await _comandaService.GetWithDetailsAsync(id, ct);
        if (comanda is null)
            return NotFound(new { message = $"Comanda '{id}' no encontrada." });
        return Ok(comanda);
    }

    [HttpGet("mesa/{mesaId:guid}")]
    public async Task<IActionResult> GetByMesa(Guid mesaId, CancellationToken ct)
    {
        var comandas = await _comandaService.GetAbierdasPorMesaAsync(mesaId, ct);
        return Ok(comandas);
    }

    /// <summary>
    /// Obtiene las cuentas corrientes abiertas (comandas exentas de turno) de un cliente,
    /// para continuar cargando consumo a una cuenta ya iniciada en lugar de abrir una nueva.
    /// </summary>
    [HttpGet("cliente/{clienteId:guid}")]
    public async Task<IActionResult> GetByCliente(Guid clienteId, CancellationToken ct)
    {
        var comandas = await _comandaService.GetAbiertasPorClienteAsync(clienteId, ct);
        return Ok(comandas);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearComandaRequest request, CancellationToken ct)
    {
        var comanda = new Comanda
        {
            TipoVentaId = request.TipoVentaId,
            MesaId = request.MesaId,
            ClienteId = request.ClienteId,
            UsuarioId = request.UsuarioId,
            Subtotal = request.Subtotal,
            Descuento = request.Descuento,
            Total = request.Total,
            Items = request.Items.Select(i => new ComandaItem
            {
                ProductoId = i.ProductoId,
                Cantidad = i.Cantidad,
                PrecioUnitario = i.PrecioUnitario,
                Notas = i.Notas,
                EstadoPreparacion = BaresFamilia.Core.Models.Enums.EstadoPreparacion.Pendiente
            }).ToList()
        };
        var created = await _comandaService.CreateAsync(comanda, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ActualizarComandaRequest request, CancellationToken ct)
    {
        var comanda = new Comanda
        {
            UsuarioId = request.UsuarioId ?? Guid.Empty,
            Subtotal = request.Subtotal,
            Descuento = request.Descuento,
            Total = request.Total,
            Items = request.Items.Select(i => new ComandaItem
            {
                ProductoId = i.ProductoId,
                Cantidad = i.Cantidad,
                PrecioUnitario = i.PrecioUnitario,
                Notas = i.Notas,
                EstadoPreparacion = BaresFamilia.Core.Models.Enums.EstadoPreparacion.Pendiente
            }).ToList()
        };
        
        try
        {
            var updated = await _comandaService.UpdateComandaAsync(id, comanda, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Envía la comanda a preparación e imprime el ticket en la
    /// Impresora 2 de Cocina/Producción (USB o Red).
    /// </summary>
    [HttpPost("{id:guid}/enviar-cocina")]
    [ProducesResponseType(typeof(ResultadoImpresion), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnviarACocina(Guid id, CancellationToken ct)
    {
        var exists = await _comandaService.ExistsAsync(id, ct);
        if (!exists)
            return NotFound(new { message = $"Comanda '{id}' no encontrada." });

        var resultado = await _comandaService.EnviarAPreparacionAsync(id, ct);
        if (!resultado.Exitoso)
            return BadRequest(resultado);

        return Ok(resultado);
    }

    /// <summary>
    /// Cobra una comanda. Si el MetodoPago tiene RequiereFacturaAfip=true,
    /// simula WSFEv1, obtiene CAE y emite orden de impresión USB.
    /// </summary>
    [HttpPost("{id:guid}/cobrar")]
    public async Task<IActionResult> Cobrar(Guid id, [FromBody] CobrarComandaRequest request, CancellationToken ct)
    {
        var resultado = await _comandaService.CobrarComandaAsync(
            id, request.TurnoCajaId, request.Pagos, ct);
        if (!resultado.Exitoso)
            return BadRequest(resultado);
        return Ok(resultado);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var exists = await _comandaService.ExistsAsync(id, ct);
        if (!exists)
            return NotFound(new { message = $"Comanda '{id}' no encontrada." });
        await _comandaService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/anular")]
    public async Task<IActionResult> Anular(Guid id, [FromBody] AnularRequest request, CancellationToken ct)
    {
        try
        {
            await _comandaService.AnularComandaAsync(id, request.UsuarioId, request.Motivo, ct);
            return Ok(new { message = "Comanda anulada con éxito." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{comandaId:guid}/items/{itemId:guid}/anular")]
    public async Task<IActionResult> AnularItem(Guid comandaId, Guid itemId, [FromBody] AnularRequest request, CancellationToken ct)
    {
        try
        {
            await _comandaService.AnularItemComandaAsync(comandaId, itemId, request.UsuarioId, request.Motivo, ct);
            return Ok(new { message = "Ítem anulado con éxito." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/imprimir-no-fiscal")]
    public async Task<IActionResult> ImprimirNoFiscal(Guid id, CancellationToken ct)
    {
        var exists = await _comandaService.ExistsAsync(id, ct);
        if (!exists)
            return NotFound(new { message = $"Comanda '{id}' no encontrada." });

        var resultado = await _comandaService.ImprimirTicketNoFiscalAsync(id, ct);
        if (!resultado.Exitoso)
            return BadRequest(resultado);

        return Ok(resultado);
    }
}

public record CrearComandaItemRequest(Guid ProductoId, int Cantidad, decimal PrecioUnitario, string? Notas);
public record CrearComandaRequest(
    Guid TipoVentaId,
    Guid? MesaId,
    Guid UsuarioId,
    decimal Subtotal,
    decimal Descuento,
    decimal Total,
    List<CrearComandaItemRequest> Items,
    Guid? ClienteId = null
);
public record ActualizarComandaRequest(
    Guid? UsuarioId,
    decimal Subtotal, 
    decimal Descuento, 
    decimal Total,
    List<CrearComandaItemRequest> Items
);
public record CobrarComandaRequest(Guid TurnoCajaId, List<PagoItemDto> Pagos);
public record AnularRequest(Guid UsuarioId, string Motivo);
