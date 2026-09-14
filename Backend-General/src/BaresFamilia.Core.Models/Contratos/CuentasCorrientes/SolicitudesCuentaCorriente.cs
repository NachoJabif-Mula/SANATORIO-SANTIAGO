namespace BaresFamilia.Core.Models.Contratos.CuentasCorrientes;

/// <summary>
/// Alta de cliente de cuenta corriente. Teléfono, email y límite de crédito son
/// datos propios de la sucursal: a la Nube solo se sincroniza el nombre.
/// </summary>
public record CreateClienteRequest(
    string Nombre,
    string Apellido,
    string? Telefono,
    string? Email,
    decimal LimiteCredito
);

public record UpdateClienteRequest(
    string Nombre,
    string Apellido,
    string? Telefono,
    string? Email,
    decimal LimiteCredito
);

/// <summary>
/// Liquidación total o parcial del saldo adeudado por un cliente. El turno de caja
/// es obligatorio porque un cobro en efectivo impacta el arqueo.
/// </summary>
public record AbonarCuentaCorrienteRequest(Guid TurnoCajaId, Guid MetodoPagoId, decimal Monto);
