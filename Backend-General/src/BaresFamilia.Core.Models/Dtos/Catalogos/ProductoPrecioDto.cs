using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Dtos.Catalogos;

/// <summary>
/// Precio configurado de un producto para un tipo de venta.
/// </summary>
public record ProductoPrecioDto(
    Guid Id,
    Guid ProductoId,
    Guid SucursalId,
    Guid TipoVentaId,
    decimal PrecioVenta,
    bool IsActive
)
{
    public static ProductoPrecioDto Desde(ProductoPrecio precio)
        => new(precio.Id, precio.ProductoId, precio.SucursalId, precio.TipoVentaId, precio.PrecioVenta, precio.IsActive);
}
