using BaresFamilia.Core.Models.Entities.CuentasCorrientes;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio específico para Cliente.
/// Extiende IService con operaciones del módulo de cuentas corrientes.
/// </summary>
public interface IClienteService : IService<Cliente>
{
    /// <summary>
    /// Obtiene un cliente con su cuenta corriente cargada.
    /// </summary>
    Task<Cliente?> GetWithCuentaCorrienteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Busca clientes por nombre (búsqueda parcial, case-insensitive).
    /// </summary>
    Task<IEnumerable<Cliente>> BuscarPorNombreAsync(string nombre, CancellationToken ct = default);
}
