namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Precio de un producto segmentado por sucursal y tipo de venta.
/// Permite precios diferenciados por local y modalidad.
/// </summary>
public class ProductoPrecio : BaseEntity
{
    public Guid ProductoId { get; set; }
    public Guid SucursalId { get; set; }
    public Guid TipoVentaId { get; set; }

    /// <summary>
    /// Precio de venta con precisión de 2 decimales.
    /// </summary>
    public decimal PrecioVenta { get; set; }

    // Navegación
    public Producto Producto { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public TipoVenta TipoVenta { get; set; } = null!;
}
