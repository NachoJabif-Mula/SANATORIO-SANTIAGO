using BaresFamilia.Core.Models.Contratos.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio de cuentas corrientes de clientes.
/// Flujo: CuentaCorrienteController → ICuentaCorrienteService → CuentaCorrienteService → I*Repository → *Repository.
/// </summary>
public interface ICuentaCorrienteService : IService<CuentaCorriente>
{
    /// <summary>
    /// Obtiene el saldo y el historial de movimientos de un cliente.
    /// </summary>
    Task<ResumenCuentaCorriente> GetResumenPorClienteAsync(Guid clienteId, CancellationToken ct = default);

    /// <summary>
    /// Liquida (total o parcialmente) el saldo adeudado por un cliente. El cobro se
    /// registra como movimiento de la cuenta y, si fue en efectivo, también impacta
    /// en el arqueo del turno de caja indicado.
    /// </summary>
    Task<ResultadoAbono> AbonarAsync(Guid clienteId, Guid turnoCajaId, Guid metodoPagoId, decimal monto, CancellationToken ct = default);
}
