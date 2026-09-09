namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Categoría de agrupación de productos para la carta/menú.
/// </summary>
public class Categoria : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Orden de visualización en la interfaz (menor = primero).
    /// </summary>
    public int OrdenVisual { get; set; }

    // Navegación
    public ICollection<Producto> Productos { get; set; } = [];
}
