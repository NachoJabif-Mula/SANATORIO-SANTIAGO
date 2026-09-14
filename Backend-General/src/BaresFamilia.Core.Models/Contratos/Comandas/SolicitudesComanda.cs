namespace BaresFamilia.Core.Models.Contratos.Comandas;

/// <summary>
/// Ítem de una comanda tal como lo envía el punto de venta.
/// </summary>
public record CrearComandaItemRequest(Guid ProductoId, int Cantidad, decimal PrecioUnitario, string? Notas);

/// <summary>
/// Alta de una comanda. ClienteId solo viaja en las cuentas corrientes, que están
/// exentas del ciclo de turnos.
/// </summary>
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

/// <summary>
/// Modificación de una comanda abierta: se reemplaza el juego de ítems vigentes.
/// </summary>
public record ActualizarComandaRequest(
    Guid? UsuarioId,
    decimal Subtotal,
    decimal Descuento,
    decimal Total,
    List<CrearComandaItemRequest> Items
);

/// <summary>
/// Cobro de una comanda, que admite pago dividido entre varios métodos.
/// </summary>
public record CobrarComandaRequest(Guid TurnoCajaId, List<PagoItemDto> Pagos);

/// <summary>
/// Parte de un cobro imputada a un método de pago. ClienteId se usa cuando el
/// método es cuenta corriente y hay que saber a quién cargarle el consumo.
/// </summary>
public record PagoItemDto(Guid MetodoPagoId, decimal Monto, Guid? ClienteId = null);

/// <summary>
/// Anulación de una comanda o de uno de sus ítems, con su auditoría.
/// </summary>
public record AnularRequest(Guid UsuarioId, string Motivo);
