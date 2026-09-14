using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para Producto.
/// Flujo: ProductoController → IProductoService → ProductoService → IProductoRepository → ProductoRepository.
/// </summary>
public class ProductoService : GenericService<Producto>, IProductoService
{
    private readonly IProductoRepository _productoRepository;
    private readonly ICategoriaRepository _categoriaRepository;

    public ProductoService(IProductoRepository productoRepository, ICategoriaRepository categoriaRepository)
        : base(productoRepository)
    {
        _productoRepository = productoRepository;
        _categoriaRepository = categoriaRepository;
    }

    public async Task<IEnumerable<Producto>> GetByCategoriaAsync(Guid categoriaId, CancellationToken ct = default)
        => await _productoRepository.GetByCategoriaAsync(categoriaId, ct);

    public async Task<Producto?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _productoRepository.GetWithDetailsAsync(id, ct);

    public async Task<IEnumerable<Producto>> GetPorSucursalAsync(Guid? sucursalId, bool incluirInactivos, CancellationToken ct = default)
        => await _productoRepository.GetPorSucursalAsync(sucursalId, incluirInactivos, ct);

    public async Task<Producto?> GetPorIdIncluyendoInactivosAsync(Guid id, CancellationToken ct = default)
        => await _productoRepository.GetPorIdIncluyendoInactivosAsync(id, ct);

    public async Task<Producto> CrearAsync(Producto producto, CancellationToken ct = default)
    {
        producto.Nombre = NormalizarNombre(producto.Nombre);
        await ValidarCategoriaDeLaMismaSucursalAsync(producto.CategoriaId, producto.SucursalId, ct);

        return await CreateAsync(producto, ct);
    }

    public async Task ActualizarAsync(Producto producto, CancellationToken ct = default)
    {
        producto.Nombre = NormalizarNombre(producto.Nombre);
        await ValidarCategoriaDeLaMismaSucursalAsync(producto.CategoriaId, producto.SucursalId, ct);

        await UpdateAsync(producto, ct);
    }

    /// <summary>
    /// Un producto solo puede colgar de una categoría activa de su propia sucursal:
    /// de lo contrario el POS de la sucursal recibiría un producto huérfano al sincronizar.
    /// </summary>
    private async Task ValidarCategoriaDeLaMismaSucursalAsync(Guid categoriaId, Guid sucursalId, CancellationToken ct)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(categoriaId, ct);
        if (categoria is null)
            throw new ReglaNegocioException("La categoría seleccionada no es válida.");

        if (categoria.SucursalId != sucursalId)
            throw new ReglaNegocioException("La categoría seleccionada pertenece a otra sucursal.");
    }

    private static string NormalizarNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre del producto es obligatorio.");

        return nombre.Trim();
    }
}
