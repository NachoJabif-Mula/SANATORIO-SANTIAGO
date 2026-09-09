using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para Receta.
/// Flujo: RecetaController → IRecetaService → RecetaService → IRecetaRepository → RecetaRepository.
/// </summary>
public class RecetaService : GenericService<Receta>, IRecetaService
{
    private readonly IRecetaRepository _recetaRepository;

    public RecetaService(IRecetaRepository repository) : base(repository)
    {
        _recetaRepository = repository;
    }

    public async Task<IEnumerable<Receta>> GetByProductoAsync(Guid productoId, CancellationToken ct = default)
        => await _recetaRepository.GetByProductoAsync(productoId, ct);
}
