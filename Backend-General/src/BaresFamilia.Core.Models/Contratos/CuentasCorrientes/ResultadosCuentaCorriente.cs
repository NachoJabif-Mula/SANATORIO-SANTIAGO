using BaresFamilia.Core.Models.Entities.CuentasCorrientes;

namespace BaresFamilia.Core.Models.Contratos.CuentasCorrientes;

/// <summary>
/// Saldo y movimientos de la cuenta corriente de un cliente.
/// </summary>
public record ResumenCuentaCorriente(decimal SaldoActual, IReadOnlyList<MovimientoCuentaCorriente> Movimientos);

/// <summary>
/// Resultado de liquidar (total o parcialmente) una cuenta corriente.
/// </summary>
public record ResultadoAbono(decimal SaldoAnterior, decimal SaldoActual, decimal MontoAbonado);
