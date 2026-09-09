using BaresFamilia.Core.Models.Entities.CuentasCorrientes;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para Cliente.
/// Extiende IRepository con operaciones del módulo de cuentas corrientes.
/// </summary>
public interface IClienteRepository : IRepository<Cliente>
{
    /// <summary>
    /// Obtiene un cliente con su cuenta corriente incluida.
    /// </summary>
    Task<Cliente?> GetWithCuentaCorrienteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Busca clientes por nombre parcial (case-insensitive).
    /// </summary>
    Task<IEnumerable<Cliente>> BuscarPorNombreAsync(string nombre, CancellationToken ct = default);
}
