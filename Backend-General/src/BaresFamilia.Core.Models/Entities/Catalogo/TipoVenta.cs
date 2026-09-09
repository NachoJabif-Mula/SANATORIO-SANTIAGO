using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Tipo de venta (Salón, Delivery, Mostrador, etc.) con configuración de recargo.
/// </summary>
public class TipoVenta : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Indica si este tipo de venta aplica un recargo adicional al precio.
    /// </summary>
    public bool AplicaRecargo { get; set; }

    // Navegación
    public ICollection<ProductoPrecio> ProductoPrecios { get; set; } = [];
    public ICollection<Comanda> Comandas { get; set; } = [];
}
