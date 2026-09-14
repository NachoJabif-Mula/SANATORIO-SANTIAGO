namespace BaresFamilia.Core.Models.Contratos.Cajas;

/// <summary>
/// Apertura de un turno de caja.
/// </summary>
public record AbrirTurnoRequest(
    Guid UsuarioId,
    decimal FondoInicial
);

/// <summary>
/// Retiro de efectivo de la caja (pago a proveedores, gastos operativos).
/// </summary>
public record RegistrarEgresoRequest(
    Guid TurnoCajaId,
    decimal Monto,
    string Concepto,
    string? ReferenciaComprobante
);

/// <summary>
/// Cierre de turno con arqueo a ciegas. TransferirMesasAbiertas confirma que las
/// comandas que sigan abiertas pasen al turno siguiente.
/// </summary>
public record CerrarTurnoRequest(
    Guid TurnoCajaId,
    decimal MontoDeclarado,
    string? Observaciones,
    bool? TransferirMesasAbiertas = false
);

/// <summary>
/// Cierre diario consolidado de la fecha contable en curso.
/// </summary>
public record CierreDiarioRequest(
    Guid UsuarioId,
    string? Observaciones
);
