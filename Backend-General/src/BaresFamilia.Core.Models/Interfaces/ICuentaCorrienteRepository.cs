using BaresFamilia.Core.Models.Entities.CuentasCorrientes;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para CuentaCorriente.
/// La cuenta y sus movimientos forman un mismo agregado, por eso ambos se
/// consultan desde acá.
/// </summary>
public interface ICuentaCorrienteRepository : IRepository<CuentaCorriente>
{
    /// <summary>
    /// Obtiene la cuenta corriente de un cliente, o null si no tiene.
    /// </summary>
    Task<CuentaCorriente?> GetPorClienteAsync(Guid clienteId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene la cuenta corriente de un cliente con los datos del cliente cargados.
    /// </summary>
    Task<CuentaCorriente?> GetPorClienteConClienteAsync(Guid clienteId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los movimientos activos de una cuenta, del más reciente al más antiguo.
    /// </summary>
    Task<IEnumerable<MovimientoCuentaCorriente>> GetMovimientosAsync(Guid cuentaCorrienteId, CancellationToken ct = default);
}
