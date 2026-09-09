namespace BaresFamilia.Core.Models.Entities.Inventario;

/// <summary>
/// Insumo/materia prima utilizada en la elaboración de productos.
/// Controla stock mínimo para alertas de reposición.
/// </summary>
public class Insumo : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Unidad de medida del insumo (ej: "Kg", "Lt", "Unidad", "Gr").
    /// </summary>
    public string UnidadMedida { get; set; } = string.Empty;

    /// <summary>
    /// Cantidad mínima aceptable antes de generar alerta de reposición.
    /// </summary>
    public decimal StockMinimo { get; set; }

    // Navegación
    public ICollection<Receta> Recetas { get; set; } = [];
    public ICollection<StockSucursal> StockSucursales { get; set; } = [];
}
