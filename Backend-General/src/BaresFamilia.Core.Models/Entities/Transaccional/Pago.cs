using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Transaccional;

/// <summary>
/// Registro de pago asociado a una comanda y turno de caja.
/// </summary>
public class Pago : BaseEntity
{
    public Guid ComandaId { get; set; }
    public Guid TurnoCajaId { get; set; }
    public Guid MetodoPagoId { get; set; }
    public decimal Monto { get; set; }
    public SyncEstado SyncEstado { get; set; } = SyncEstado.Pendiente;

    // Navegación
    public Comanda Comanda { get; set; } = null!;
    public TurnoCaja TurnoCaja { get; set; } = null!;
    public MetodoPago MetodoPago { get; set; } = null!;
}
