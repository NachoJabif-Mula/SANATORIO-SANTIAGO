using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Entities.Inventario;

/// <summary>
/// Receta: relación Producto → Insumo con la cantidad necesaria
/// para producir una unidad del producto.
/// Permite el descuento automático de stock al vender.
/// </summary>
public class Receta : BaseEntity
{
    public Guid ProductoId { get; set; }
    public Guid InsumoId { get; set; }

    /// <summary>
    /// Cantidad de insumo necesaria para producir 1 unidad del producto.
    /// </summary>
    public decimal CantidadNecesaria { get; set; }

    // Navegación
    public Producto Producto { get; set; } = null!;
    public Insumo Insumo { get; set; } = null!;
}
