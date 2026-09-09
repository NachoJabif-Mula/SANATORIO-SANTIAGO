using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Transaccional;

/// <summary>
/// Turno de operación de una caja. Registra apertura, cierre y diferencia de arqueo.
/// </summary>
public class TurnoCaja : BaseEntity
{
    public Guid CajaId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public DateTime FechaContable { get; set; }
    public string Turno { get; set; } = string.Empty;

    /// <summary>
    /// Monto inicial con el que se abre la caja.
    /// </summary>
    public decimal FondoInicial { get; set; }

    /// <summary>
    /// Diferencia entre el monto esperado y el efectivo real al cierre (puede ser negativo).
    /// </summary>
    public decimal? DiferenciaArqueo { get; set; }

    public SyncEstado SyncEstado { get; set; } = SyncEstado.Pendiente;

    // Navegación
    public Caja Caja { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public ICollection<Pago> Pagos { get; set; } = [];
    public ICollection<MovimientoCaja> MovimientosCaja { get; set; } = [];
}
