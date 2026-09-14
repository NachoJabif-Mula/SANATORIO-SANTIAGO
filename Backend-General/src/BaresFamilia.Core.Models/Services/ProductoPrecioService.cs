using BaresFamilia.Core.Models.Contratos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para ProductoPrecio.
/// Resuelve el alta/baja/modificación en bloque de la grilla de precios que el
/// backoffice edita por producto.
/// </summary>
public class ProductoPrecioService : GenericService<ProductoPrecio>, IProductoPrecioService
{
    private readonly IProductoPrecioRepository _productoPrecioRepository;
    private readonly ITipoVentaRepository _tipoVentaRepository;

    public ProductoPrecioService(
        IProductoPrecioRepository productoPrecioRepository,
        ITipoVentaRepository tipoVentaRepository)
        : base(productoPrecioRepository)
    {
        _productoPrecioRepository = productoPrecioRepository;
        _tipoVentaRepository = tipoVentaRepository;
    }

    public async Task<IEnumerable<ProductoPrecio>> GetActivosPorProductoAsync(Guid productoId, CancellationToken ct = default)
        => await _productoPrecioRepository.GetActivosPorProductoAsync(productoId, ct);

    public async Task<IEnumerable<ProductoPrecio>> GetTodosPorSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _productoPrecioRepository.GetTodosPorSucursalAsync(sucursalId, ct);

    public async Task ReemplazarPreciosDeProductoAsync(
        Guid productoId,
        Guid sucursalId,
        IEnumerable<PrecioPorTipoVenta> precios,
        CancellationToken ct = default)
    {
        var existentes = (await _productoPrecioRepository
            .GetTodosPorProductoYSucursalAsync(productoId, sucursalId, ct))
            .ToList();

        var porTipoVenta = existentes.ToDictionary(p => p.TipoVentaId);
        var tipoVentaIdsProcesados = new HashSet<Guid>();

        // Se traen los tipos de venta una sola vez (incluidos los dados de baja, que
        // siguen siendo válidos para un precio ya cargado) en lugar de consultar por ítem.
        var tipoVentaIdsValidos = (await _tipoVentaRepository.GetOrdenadosPorNombreAsync(incluirInactivos: true, ct))
            .Select(t => t.Id)
            .ToHashSet();

        var nuevos = new List<ProductoPrecio>();
        var modificados = new List<ProductoPrecio>();
        var ahora = DateTime.UtcNow;

        foreach (var precio in precios)
        {
            // Una entrada con un tipo de venta inexistente se saltea en lugar de
            // abortar el lote: así un catálogo desactualizado en el cliente no
            // impide guardar el resto de los precios.
            if (!tipoVentaIdsValidos.Contains(precio.TipoVentaId))
                continue;

            if (porTipoVenta.TryGetValue(precio.TipoVentaId, out var existente))
            {
                existente.PrecioVenta = precio.PrecioVenta;
                existente.IsActive = true;
                existente.UpdatedAt = ahora;
                modificados.Add(existente);
            }
            else
            {
                nuevos.Add(new ProductoPrecio
                {
                    Id = Guid.NewGuid(),
                    ProductoId = productoId,
                    SucursalId = sucursalId,
                    TipoVentaId = precio.TipoVentaId,
                    PrecioVenta = precio.PrecioVenta,
                    IsActive = true,
                    CreatedAt = ahora,
                    UpdatedAt = ahora
                });
            }

            tipoVentaIdsProcesados.Add(precio.TipoVentaId);
        }

        // Los precios que dejaron de enviarse se dan de baja lógica.
        foreach (var existente in existentes)
        {
            if (tipoVentaIdsProcesados.Contains(existente.TipoVentaId) || !existente.IsActive)
                continue;

            existente.IsActive = false;
            existente.UpdatedAt = ahora;
            modificados.Add(existente);
        }

        await _productoPrecioRepository.GuardarLoteAsync(nuevos, modificados, ct);
    }
}
