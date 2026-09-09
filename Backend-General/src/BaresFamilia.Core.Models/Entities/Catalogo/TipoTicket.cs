namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Tipo de ticket imprimible con su template ESC/POS.
/// Códigos predefinidos: "Comanda", "FacturaA", "FacturaB".
/// </summary>
public class TipoTicket : BaseEntity
{
    /// <summary>
    /// Código único del tipo de ticket (ej: "Comanda", "FacturaA", "FacturaB").
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>
    /// Nombre descriptivo para mostrar en el Backoffice.
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Template de impresión en texto plano con placeholders.
    /// Placeholders soportados:
    ///   {{NEGOCIO}}, {{SUCURSAL}}, {{FECHA}}, {{HORA}},
    ///   {{COMANDA_ID}}, {{MESA}}, {{MOZO}},
    ///   {{ITEMS}}, {{TOTAL_ITEMS}},
    ///   {{SUBTOTAL}}, {{DESCUENTO}}, {{TOTAL}},
    ///   {{METODO_PAGO}}, {{MONTO_PAGADO}},
    ///   {{CAE}}, {{CAE_VTO}}, {{COMPROBANTE_NRO}}
    /// </summary>
    public string TemplateContenido { get; set; } = string.Empty;

    // Navegación
    public ICollection<ImpresoraTicketTipo> Impresoras { get; set; } = [];
}
