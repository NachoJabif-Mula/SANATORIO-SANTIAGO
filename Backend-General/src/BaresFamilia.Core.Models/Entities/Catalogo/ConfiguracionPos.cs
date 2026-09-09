namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Configuración visual del POS por sucursal.
/// Almacena un payload JSON con coordenadas de mesas, colores de botones,
/// y cualquier personalización visual del punto de venta.
/// </summary>
public class ConfiguracionPos : BaseEntity
{
    public Guid SucursalId { get; set; }

    /// <summary>
    /// Nombre descriptivo de la configuración (ej: "Layout Principal", "Patio Externo").
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// JSON serializado con la configuración visual:
    /// coordenadas de mesas, colores de botones, zonas del salón, etc.
    /// </summary>
    public string ConfiguracionJson { get; set; } = "{}";

    // Navegación
    public Sucursal Sucursal { get; set; } = null!;
}
