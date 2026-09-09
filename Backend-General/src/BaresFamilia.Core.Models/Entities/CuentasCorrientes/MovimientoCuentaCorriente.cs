using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.CuentasCorrientes;

/// <summary>
/// Movimiento de cuenta corriente: cargo (consumo) o pago (abono/liquidación).
/// Registra el detalle para dar seguimiento a la cuenta del cliente.
/// </summary>
public class MovimientoCuentaCorriente : BaseEntity
{
    public Guid CuentaCorrienteId { get; set; }

    /// <summary>Comanda que originó el cargo. Null para un abono/pago (no está atado a una orden).</summary>
    public Guid? ComandaId { get; set; }

    public TipoMovimientoCuentaCorriente Tipo { get; set; }

    /// <summary>Monto siempre positivo; el signo respecto del saldo lo determina Tipo.</summary>
    public decimal Monto { get; set; }

    /// <summary>Descripción/detalle de la orden o del abono, para el seguimiento en Backoffice.</summary>
    public string Detalle { get; set; } = string.Empty;

    public SyncEstado SyncEstado { get; set; } = SyncEstado.Pendiente;

    // Navegación
    public CuentaCorriente CuentaCorriente { get; set; } = null!;
    public Comanda? Comanda { get; set; }
}
