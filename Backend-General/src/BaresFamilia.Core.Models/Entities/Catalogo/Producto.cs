using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Producto del menú/carta con configuración visual y operativa.
/// </summary>
public class Producto : BaseEntity
{
    /// <summary>
    /// Sucursal dueña de este producto: el catálogo (categorías y productos) es
    /// propio de cada sucursal, no compartido globalmente. Debe coincidir con la
    /// sucursal de <see cref="CategoriaId"/>.
    /// </summary>
    public Guid SucursalId { get; set; }

    public Guid CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Color hexadecimal para la UI del punto de venta (ej: "#FF6B35").
    /// </summary>
    public string ColorUi { get; set; } = "#FFFFFF";

    /// <summary>
    /// Indica si el producto requiere pasar por cocina (genera ticket de cocina).
    /// </summary>
    public bool RequiereCocina { get; set; }

    /// <summary>
    /// Alícuota de IVA aplicable para facturación fiscal.
    /// Determina la tasa impositiva al emitir comprobantes en impresoras fiscales o AFIP.
    /// </summary>
    public AlicuotaIva AlicuotaIva { get; set; } = AlicuotaIva.Iva21;

    // Navegación
    public Sucursal Sucursal { get; set; } = null!;
    public Categoria Categoria { get; set; } = null!;
    public ICollection<ProductoPrecio> ProductoPrecios { get; set; } = [];
    public ICollection<ComandaItem> ComandaItems { get; set; } = [];
    public ICollection<Receta> Recetas { get; set; } = [];
}
