namespace BaresFamilia.Core.Models.Contratos.Comandas;

/// <summary>
/// Resultado del proceso de cobro con información de facturación AFIP.
/// </summary>
public class ResultadoCobro
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public Guid? PagoId { get; set; }

    // Datos AFIP (cuando aplica)
    public bool FacturaAfipEmitida { get; set; }
    public string? CaeNumero { get; set; }
    public string? CaeVencimiento { get; set; }
    public string? ComprobanteNumero { get; set; }
    public string? OrdenImpresionUsb { get; set; }

    /// <summary>
    /// True cuando la factura se generó en modo simulador (sucursal sin impresora física
    /// configurada): OrdenImpresionUsb contiene el ticket para mostrar en pantalla.
    /// </summary>
    public bool Simulado { get; set; }
}

/// <summary>
/// Resultado de una operación de impresión.
/// </summary>
public class ResultadoImpresion
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public string? ImpresoraUtilizada { get; set; }
    public string? TicketContenido { get; set; }

    /// <summary>
    /// True cuando el ticket no se envió a una impresora física porque la sucursal
    /// tiene activo el modo simulador (Sucursal.ImpresionSimulada).
    /// </summary>
    public bool Simulado { get; set; }
}
