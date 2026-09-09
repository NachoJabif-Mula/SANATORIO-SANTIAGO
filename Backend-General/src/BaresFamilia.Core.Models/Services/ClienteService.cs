using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para Cliente.
/// Flujo: ClienteController → IClienteService → ClienteService → IClienteRepository → ClienteRepository.
/// </summary>
public class ClienteService : GenericService<Cliente>, IClienteService
{
    private readonly IClienteRepository _clienteRepository;

    public ClienteService(IClienteRepository repository) : base(repository)
    {
        _clienteRepository = repository;
    }

    public async Task<Cliente?> GetWithCuentaCorrienteAsync(Guid id, CancellationToken ct = default)
        => await _clienteRepository.GetWithCuentaCorrienteAsync(id, ct);

    public async Task<IEnumerable<Cliente>> BuscarPorNombreAsync(string nombre, CancellationToken ct = default)
        => await _clienteRepository.BuscarPorNombreAsync(nombre, ct);
}
