using BaresFamilia.Core.Models.Dtos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de lectura del catálogo que consume el punto de venta de la sucursal.
///
/// A diferencia del catálogo de la Nube, acá nunca se siembran datos por defecto:
/// todo el catálogo local llega exclusivamente por sincronización, y sembrarlo
/// localmente generaría filas con Id distinto al de la Nube que romperían el pull.
/// </summary>
public interface ICatalogoPosService
{
    Task<IEnumerable<Categoria>> GetCategoriasActivasAsync(CancellationToken ct = default);

    Task<IEnumerable<ProductoDisponible>> GetProductosDisponiblesAsync(CancellationToken ct = default);

    Task<IEnumerable<MetodoPago>> GetMetodosPagoActivosAsync(CancellationToken ct = default);

    Task<IEnumerable<TipoVenta>> GetTiposVentaActivosAsync(CancellationToken ct = default);

    Task<IEnumerable<Mesa>> GetMesasActivasAsync(CancellationToken ct = default);
}
