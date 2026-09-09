namespace BaresFamilia.Core.Models.Entities.CuentasCorrientes;

/// <summary>
/// Cuenta corriente de un cliente. Relación 1:1 con Cliente.
/// Registra el saldo actual (positivo = deuda del cliente).
/// </summary>
public class CuentaCorriente : BaseEntity
{
    public Guid ClienteId { get; set; }

    /// <summary>
    /// Saldo actual de la cuenta. Positivo = el cliente debe. Negativo = saldo a favor.
    /// </summary>
    public decimal SaldoActual { get; set; }

    // Navegación
    public Cliente Cliente { get; set; } = null!;
}
