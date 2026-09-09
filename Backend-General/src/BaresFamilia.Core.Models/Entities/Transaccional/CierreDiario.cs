using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Transaccional;

/// <summary>
/// Cierre diario consolidado de una caja. Registra el resumen de todos los turnos
/// del día, incluyendo ventas, egresos y desglose por método de pago.
/// Es generado por el último encargado que cierra su turno del día.
/// </summary>
public class CierreDiario : BaseEntity
{
    public Guid CajaId { get; set; }

    /// <summary>
    /// Fecha del cierre (solo día, sin hora). Clave lógica junto con CajaId.
    /// </summary>
    public DateTime Fecha { get; set; }

    /// <summary>
    /// Usuario (encargado) que realizó el cierre diario.
    /// </summary>
    public Guid UsuarioCierreId { get; set; }

    /// <summary>
    /// Total de ventas cobradas durante el día.
    /// </summary>
    public decimal TotalVentas { get; set; }

    /// <summary>
    /// Total de egresos registrados durante el día.
    /// </summary>
    public decimal TotalEgresos { get; set; }

    /// <summary>
    /// Total neto del día (ventas - egresos).
    /// </summary>
    public decimal TotalNeto { get; set; }

    /// <summary>
    /// JSON con desglose detallado: totales por turno, por método de pago, diferencias de arqueo, etc.
    /// </summary>
    public string? ResumenJson { get; set; }

    /// <summary>
    /// Observaciones del encargado al realizar el cierre diario.
    /// </summary>
    public string? Observaciones { get; set; }

    public SyncEstado SyncEstado { get; set; } = SyncEstado.Pendiente;

    // Navegación
    public Caja Caja { get; set; } = null!;
    public Usuario UsuarioCierre { get; set; } = null!;
}
