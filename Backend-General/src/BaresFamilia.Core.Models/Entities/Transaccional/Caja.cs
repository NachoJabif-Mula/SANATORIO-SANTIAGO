using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Transaccional;

/// <summary>
/// Caja registradora física o virtual asociada a una sucursal.
/// </summary>
public class Caja : BaseEntity
{
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoCaja TipoCaja { get; set; } = TipoCaja.Principal;
    public SyncEstado SyncEstado { get; set; } = SyncEstado.Pendiente;

    // Navegación
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<TurnoCaja> TurnosCaja { get; set; } = [];
}
