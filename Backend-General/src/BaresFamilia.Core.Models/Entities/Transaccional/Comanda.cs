using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Transaccional;

/// <summary>
/// Comanda de venta. Cabecera del pedido con totales y estado.
/// </summary>
public class Comanda : BaseEntity
{
    public Guid TipoVentaId { get; set; }
    public Guid? MesaId { get; set; }

    /// <summary>
    /// Cliente al que pertenece esta comanda cuando se trata de una cuenta corriente
    /// abierta (orden a nombre de un cliente en lugar de una mesa). Null para ventas normales.
    /// </summary>
    public Guid? ClienteId { get; set; }

    public Guid UsuarioId { get; set; }
    public ComandaEstado Estado { get; set; } = ComandaEstado.Abierta;
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }

    /// <summary>
    /// Fecha contable y turno de caja al que pertenece la venta. Quedan en null mientras la
    /// comanda es una cuenta corriente abierta (exenta de turno); se asignan recién al cerrarla/cobrarla.
    /// </summary>
    public DateTime? FechaContable { get; set; }
    public string? Turno { get; set; }
    public SyncEstado SyncEstado { get; set; } = SyncEstado.Pendiente;

    // Navegación
    public TipoVenta TipoVenta { get; set; } = null!;
    public Mesa? Mesa { get; set; }
    public Cliente? Cliente { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public ICollection<ComandaItem> Items { get; set; } = [];
    public ICollection<Pago> Pagos { get; set; } = [];
}
