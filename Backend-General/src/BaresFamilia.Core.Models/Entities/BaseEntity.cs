namespace BaresFamilia.Core.Models.Entities;

/// <summary>
/// Clase base abstracta para todas las entidades del dominio.
/// Proporciona campos de auditoría y borrado lógico estándar.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Identificador único universal. Generado como GUID para compatibilidad
    /// con sincronización offline y evitar colisiones entre nodos.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Fecha y hora UTC de creación del registro.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha y hora UTC de la última modificación del registro.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicador de borrado lógico. False = eliminado/inactivo.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
