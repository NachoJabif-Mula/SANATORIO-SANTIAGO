namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Tabla intermedia que define qué tipo de ticket está habilitado
/// para qué impresora. Relación N:M entre Impresora y TipoTicket.
/// </summary>
public class ImpresoraTicketTipo : BaseEntity
{
    public Guid ImpresoraId { get; set; }
    public Guid TipoTicketId { get; set; }

    // Navegación
    public Impresora Impresora { get; set; } = null!;
    public TipoTicket TipoTicket { get; set; } = null!;
}
