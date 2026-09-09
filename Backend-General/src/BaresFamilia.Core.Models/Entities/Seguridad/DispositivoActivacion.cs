using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Entities.Seguridad;

/// <summary>
/// Registro de activación de un dispositivo POS vinculado a una sucursal.
/// Almacena el código de activación y el estado de vinculación M2M.
/// </summary>
public class DispositivoActivacion : BaseEntity
{
    public Guid SucursalId { get; set; }

    /// <summary>
    /// Código alfanumérico único de activación (ej: BAR-7X9P-M2A1).
    /// Se genera en la Nube y se ingresa manualmente en el POS local.
    /// </summary>
    public string CodigoActivacion { get; set; } = string.Empty;

    /// <summary>
    /// Nombre descriptivo del dispositivo (ej: "POS Caja Principal").
    /// </summary>
    public string NombreDispositivo { get; set; } = string.Empty;

    /// <summary>
    /// Indica si el código ya fue canjeado por un JWT M2M.
    /// </summary>
    public bool Activado { get; set; }

    /// <summary>
    /// Fecha y hora UTC en que se activó el dispositivo.
    /// </summary>
    public DateTime? FechaActivacion { get; set; }

    /// <summary>
    /// Fecha de expiración del código de activación (antes de ser usado).
    /// </summary>
    public DateTime ExpiraCodigo { get; set; }

    /// <summary>
    /// Hash del JWT M2M emitido. Permite revocar tokens específicos.
    /// </summary>
    public string? TokenHash { get; set; }

    // Navegación
    public Sucursal Sucursal { get; set; } = null!;
}
