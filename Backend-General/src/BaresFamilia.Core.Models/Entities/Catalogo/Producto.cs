using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Producto del menú/carta con configuración visual y operativa.
/// </summary>
public class Producto : BaseEntity
{
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
    public Categoria Categoria { get; set; } = null!;
    public ICollection<ProductoPrecio> ProductoPrecios { get; set; } = [];
    public ICollection<ComandaItem> ComandaItems { get; set; } = [];
    public ICollection<Receta> Recetas { get; set; } = [];
}
