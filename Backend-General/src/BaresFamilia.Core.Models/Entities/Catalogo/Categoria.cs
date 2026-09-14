namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Categoría de agrupación de productos para la carta/menú.
/// </summary>
public class Categoria : BaseEntity
{
    /// <summary>
    /// Sucursal dueña de esta categoría: el catálogo (categorías y productos) es
    /// propio de cada sucursal, no compartido globalmente.
    /// </summary>
    public Guid SucursalId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Orden de visualización en la interfaz (menor = primero).
    /// </summary>
    public int OrdenVisual { get; set; }

    // Navegación
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<Producto> Productos { get; set; } = [];
}
