namespace BaresFamilia.Core.Models.Enums;

/// <summary>
/// Tipo de movimiento en una cuenta corriente de cliente.
/// </summary>
public enum TipoMovimientoCuentaCorriente
{
    /// <summary>Consumo cargado a la cuenta (aumenta la deuda del cliente).</summary>
    Cargo = 0,

    /// <summary>Abono o liquidación (disminuye la deuda del cliente).</summary>
    Pago = 1
}
