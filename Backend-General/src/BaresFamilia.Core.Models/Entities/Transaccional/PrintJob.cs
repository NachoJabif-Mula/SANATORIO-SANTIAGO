using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Entities.Transaccional;

/// <summary>
/// Representa un trabajo de impresión (no fiscal o fiscal) encolado para su envío vía SignalR al bridge local.
/// </summary>
public class PrintJob : BaseEntity
{
    public Guid SucursalId { get; set; }
    public Guid ImpresoraId { get; set; }

    /// <summary>
    /// Estado del trabajo: "Pendiente", "Enviado", "Impreso", "Fallo".
    /// </summary>
    public string Estado { get; set; } = "Pendiente";

    /// <summary>
    /// Tipo de documento: "Comanda", "FacturaA", "FacturaB", "FacturaC", "CierreZ", "CierreX".
    /// </summary>
    public string TipoDocumento { get; set; } = "Comanda";

    /// <summary>
    /// Payload JSON con la estructura completa requerida por el CLI de impresión o fiscal.
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// Resultado JSON devuelto por la ejecución del CLI.
    /// </summary>
    public string? ResultadoJson { get; set; }

    /// <summary>
    /// Cantidad de intentos de envío/impresión.
    /// </summary>
    public int Intentos { get; set; } = 0;

    // Navegación
    public Sucursal? Sucursal { get; set; }
    public Impresora? Impresora { get; set; }
}
