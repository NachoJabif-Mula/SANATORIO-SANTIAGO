using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para Insumo.
/// Flujo: InsumoController → IInsumoService → InsumoService → IInsumoRepository → InsumoRepository.
/// </summary>
public class InsumoService : GenericService<Insumo>, IInsumoService
{
    private readonly IInsumoRepository _insumoRepository;

    public InsumoService(IInsumoRepository repository) : base(repository)
    {
        _insumoRepository = repository;
    }

    public async Task<IEnumerable<Insumo>> GetConStockBajoAsync(Guid sucursalId, CancellationToken ct = default)
        => await _insumoRepository.GetConStockBajoAsync(sucursalId, ct);
}
