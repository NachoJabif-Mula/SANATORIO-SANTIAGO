using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Core.Models.Services;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para Producto.
/// Flujo: ProductoController → IProductoService → ProductoService → IProductoRepository → ProductoRepository.
/// </summary>
public class ProductoService : GenericService<Producto>, IProductoService
{
    private readonly IProductoRepository _productoRepository;

    public ProductoService(IProductoRepository repository) : base(repository)
    {
        _productoRepository = repository;
    }

    public async Task<IEnumerable<Producto>> GetByCategoriaAsync(Guid categoriaId, CancellationToken ct = default)
        => await _productoRepository.GetByCategoriaAsync(categoriaId, ct);

    public async Task<Producto?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _productoRepository.GetWithDetailsAsync(id, ct);
}
