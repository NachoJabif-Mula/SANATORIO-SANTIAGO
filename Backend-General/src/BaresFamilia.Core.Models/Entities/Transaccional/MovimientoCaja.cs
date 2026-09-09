using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Transaccional;

/// <summary>
/// Movimiento de caja (ingreso/egreso) fuera del flujo de ventas.
/// Ejemplo: retiro para proveedor, depósito de cambio, etc.
/// </summary>
public class MovimientoCaja : BaseEntity
{
    public Guid TurnoCajaId { get; set; }
    public TipoMovimientoCaja Tipo { get; set; }
    public decimal Monto { get; set; }
    public string Concepto { get; set; } = string.Empty;

    /// <summary>
    /// Referencia al comprobante externo (número de factura, recibo, etc.).
    /// </summary>
    public string? ReferenciaComprobante { get; set; }

    public SyncEstado SyncEstado { get; set; } = SyncEstado.Pendiente;

    // Navegación
    public TurnoCaja TurnoCaja { get; set; } = null!;
}
