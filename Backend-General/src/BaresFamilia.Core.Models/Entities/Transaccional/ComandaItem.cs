using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Transaccional;

/// <summary>
/// Línea/ítem individual dentro de una comanda con estado de preparación.
/// </summary>
public class ComandaItem : BaseEntity
{
    public Guid ComandaId { get; set; }
    public Guid ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    /// <summary>
    /// Notas especiales del cliente (ej: "sin sal", "bien cocido").
    /// </summary>
    public string? Notas { get; set; }

    /// <summary>
    /// Estado actual de preparación en cocina/barra.
    /// </summary>
    public EstadoPreparacion EstadoPreparacion { get; set; } = EstadoPreparacion.Pendiente;

    /// <summary>Indica si el ítem fue anulado (ya comandado, pero descontado de la comanda).</summary>
    public bool Cancelado { get; set; } = false;

    /// <summary>Motivo de anulación. Solo se completa cuando Cancelado es true.</summary>
    public string? MotivoAnulacion { get; set; }

    /// <summary>Usuario que anuló este ítem (o que anuló la comanda completa, si aplica a todos).</summary>
    public Guid? AnuladoPorUsuarioId { get; set; }

    /// <summary>Fecha/hora en que se anuló el ítem.</summary>
    public DateTime? FechaAnulacion { get; set; }

    // Navegación
    public Comanda Comanda { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public Usuario? AnuladoPorUsuario { get; set; }
}
