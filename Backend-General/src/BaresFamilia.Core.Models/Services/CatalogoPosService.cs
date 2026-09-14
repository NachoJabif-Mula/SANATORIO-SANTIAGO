using BaresFamilia.Core.Models.Dtos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de lectura del catálogo local del punto de venta.
/// Flujo: CatalogoController → ICatalogoPosService → CatalogoPosService → I*Repository → *Repository.
/// </summary>
public class CatalogoPosService : ICatalogoPosService
{
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IProductoRepository _productoRepository;
    private readonly IMetodoPagoRepository _metodoPagoRepository;
    private readonly ITipoVentaRepository _tipoVentaRepository;
    private readonly IMesaRepository _mesaRepository;

    public CatalogoPosService(
        ICategoriaRepository categoriaRepository,
        IProductoRepository productoRepository,
        IMetodoPagoRepository metodoPagoRepository,
        ITipoVentaRepository tipoVentaRepository,
        IMesaRepository mesaRepository)
    {
        _categoriaRepository = categoriaRepository;
        _productoRepository = productoRepository;
        _metodoPagoRepository = metodoPagoRepository;
        _tipoVentaRepository = tipoVentaRepository;
        _mesaRepository = mesaRepository;
    }

    public async Task<IEnumerable<Categoria>> GetCategoriasActivasAsync(CancellationToken ct = default)
        => await _categoriaRepository.GetPorSucursalAsync(sucursalId: null, incluirInactivas: false, ct);

    public async Task<IEnumerable<ProductoDisponible>> GetProductosDisponiblesAsync(CancellationToken ct = default)
        => await _productoRepository.GetDisponiblesConPrecioAsync(ct);

    public async Task<IEnumerable<MetodoPago>> GetMetodosPagoActivosAsync(CancellationToken ct = default)
        => await _metodoPagoRepository.GetOrdenadosPorNombreAsync(incluirInactivos: false, ct);

    public async Task<IEnumerable<TipoVenta>> GetTiposVentaActivosAsync(CancellationToken ct = default)
        => await _tipoVentaRepository.GetOrdenadosPorNombreAsync(incluirInactivos: false, ct);

    public async Task<IEnumerable<Mesa>> GetMesasActivasAsync(CancellationToken ct = default)
        => await _mesaRepository.GetActivasOrdenadasAsync(ct);
}
