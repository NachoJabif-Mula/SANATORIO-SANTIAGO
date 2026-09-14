using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.Fiscal;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Transaccional;

/// <summary>
/// Representa un trabajo de impresión (no fiscal o fiscal) encolado para su envío vía SignalR al bridge local.
/// Persistir cada intento permite revisar desde el Backoffice qué se imprimió y qué falló,
/// separando el fallo de impresión del fallo de autorización fiscal: un comprobante puede
/// tener CAE de ARCA y aun así no haber salido por la impresora.
/// </summary>
public class PrintJob : BaseEntity
{
    public Guid SucursalId { get; set; }
    public Guid ImpresoraId { get; set; }

    /// <summary>
    /// Comprobante fiscal asociado, cuando el trabajo corresponde a la impresión de una factura.
    /// </summary>
    public Guid? ComprobanteId { get; set; }

    public EstadoPrintJob Estado { get; set; } = EstadoPrintJob.Pendiente;

    /// <summary>
    /// Tipo de documento: "Comanda", "FacturaA", "FacturaB", "FacturaC", "CierreZ", "CierreX".
    /// Coincide con el código del <see cref="TipoTicket"/> usado para renderizarlo.
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

    /// <summary>
    /// Mensaje del último fallo, para mostrarlo en la pantalla de logs sin tener que
    /// interpretar el JSON de resultado.
    /// </summary>
    public string? UltimoError { get; set; }

    /// <summary>
    /// Momento en que el trabajo terminó (impreso o fallido).
    /// </summary>
    public DateTime? FechaProcesado { get; set; }

    /// <summary>
    /// Estado de sincronización hacia la Nube, que consolida los logs de impresión de
    /// todas las sucursales para el Backoffice.
    /// </summary>
    public SyncEstado SyncEstado { get; set; } = SyncEstado.Pendiente;

    // Navegación
    public Sucursal? Sucursal { get; set; }
    public Impresora? Impresora { get; set; }
    public Comprobante? Comprobante { get; set; }
}
