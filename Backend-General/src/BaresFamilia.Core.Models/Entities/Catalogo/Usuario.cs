using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Usuario del sistema con autenticación dual (password + PIN rápido).
/// </summary>
public class Usuario : BaseEntity
{
    public Guid RolId { get; set; }
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// PIN de acceso rápido para operaciones en el punto de venta.
    /// </summary>
    public string PinAcceso { get; set; } = string.Empty;

    // Navegación
    public Rol Rol { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<TurnoCaja> TurnosCaja { get; set; } = [];
    public ICollection<Comanda> Comandas { get; set; } = [];
}
