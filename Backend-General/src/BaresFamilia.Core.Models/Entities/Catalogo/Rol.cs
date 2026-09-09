namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Rol de usuario con permisos almacenados como JSON para máxima flexibilidad.
/// </summary>
public class Rol : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Lista de permisos serializada como JSON en la base de datos.
    /// Ejemplo: ["ventas.crear", "reportes.ver", "config.editar"]
    /// </summary>
    public List<string> Permisos { get; set; } = [];

    /// <summary>
    /// Indica si este rol tiene alcance global (todas las sucursales) en lugar de
    /// quedar acotado a la sucursal del Usuario que lo tiene asignado.
    /// </summary>
    public bool EsGlobal { get; set; }

    // Navegación
    public ICollection<Usuario> Usuarios { get; set; } = [];
}
