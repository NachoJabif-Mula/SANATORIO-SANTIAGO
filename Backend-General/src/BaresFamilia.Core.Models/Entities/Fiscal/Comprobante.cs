using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Fiscal;

/// <summary>
/// Comprobante electrónico emitido (o pendiente de emitir) ante ARCA vía WSFEv1.
///
/// Es el registro fiscal legal de la venta: sostiene la correlatividad de numeración por
/// punto de venta, permite reimprimir, auditar contra lo que ARCA autorizó y alimenta el
/// reporte de IVA Ventas. Los datos del emisor y del receptor se congelan al momento de
/// emitir, porque la ficha de la sucursal o del cliente puede cambiar después y el
/// comprobante debe conservar lo que efectivamente se declaró.
/// </summary>
public class Comprobante : BaseEntity
{
    public Guid SucursalId { get; set; }

    /// <summary>
    /// Comanda que originó el comprobante. Nulo en comprobantes emitidos fuera del flujo de venta.
    /// </summary>
    public Guid? ComandaId { get; set; }

    public TipoComprobanteAfip TipoComprobante { get; set; } = TipoComprobanteAfip.FacturaB;

    public AmbienteFiscal Ambiente { get; set; } = AmbienteFiscal.Homologacion;

    public int PuntoVenta { get; set; }

    /// <summary>
    /// Número correlativo asignado por punto de venta y tipo de comprobante.
    /// Vale 0 mientras el comprobante está pendiente de autorización.
    /// </summary>
    public long NumeroComprobante { get; set; }

    /// <summary>
    /// Fecha del comprobante (campo CbteFch de WSFEv1), en la fecha local de la sucursal.
    /// </summary>
    public DateTime FechaEmision { get; set; }

    // ── Emisor (congelado al emitir) ───────────────────────────
    public string CuitEmisor { get; set; } = string.Empty;
    public string RazonSocialEmisor { get; set; } = string.Empty;
    public int CondicionIvaEmisor { get; set; }

    // ── Receptor ───────────────────────────────────────────────

    /// <summary>
    /// Código de tipo de documento de ARCA: 80 = CUIT, 86 = CUIL, 96 = DNI,
    /// 99 = consumidor final sin identificar.
    /// </summary>
    public int TipoDocumentoReceptor { get; set; } = 99;

    public string? NumeroDocumentoReceptor { get; set; }
    public string? RazonSocialReceptor { get; set; }

    /// <summary>
    /// Condición frente al IVA del receptor (1 = RI, 4 = Exento, 5 = Consumidor Final, 6 = Monotributo).
    /// Determina junto con la del emisor si corresponde comprobante A, B o C.
    /// </summary>
    public int CondicionIvaReceptor { get; set; }

    // ── Importes (desglose exigido por WSFEv1 y por el Libro IVA Ventas) ──
    public decimal ImporteNeto { get; set; }
    public decimal ImporteIva { get; set; }
    public decimal ImporteExento { get; set; }
    public decimal ImporteNoGravado { get; set; }
    public decimal ImporteTotal { get; set; }

    // ── Respuesta de ARCA ──────────────────────────────────────
    public EstadoComprobante Estado { get; set; } = EstadoComprobante.Pendiente;

    public string? Cae { get; set; }
    public DateTime? CaeVencimiento { get; set; }

    /// <summary>
    /// Contenido ya codificado del QR obligatorio (RG 4291) para imprimir en el ticket.
    /// </summary>
    public string? QrPayload { get; set; }

    /// <summary>
    /// Observaciones devueltas por WSFEv1 (comprobante autorizado pero con advertencias).
    /// </summary>
    public string? ObservacionesArca { get; set; }

    // ── Reintento y auditoría ──────────────────────────────────
    public int IntentosEmision { get; set; }
    public DateTime? FechaUltimoIntento { get; set; }
    public string? UltimoError { get; set; }

    /// <summary>
    /// Sobres SOAP crudos del intento de autorización, para poder reconciliar contra ARCA
    /// cuando hay discrepancias.
    /// </summary>
    public string? RequestXml { get; set; }
    public string? ResponseXml { get; set; }

    /// <summary>
    /// Estado de sincronización hacia la Nube, que consolida los comprobantes de todas
    /// las sucursales para los reportes del Backoffice.
    /// </summary>
    public SyncEstado SyncEstado { get; set; } = SyncEstado.Pendiente;

    // Navegación
    public Sucursal? Sucursal { get; set; }
    public Comanda? Comanda { get; set; }
    public ICollection<ComprobanteAlicuota> Alicuotas { get; set; } = [];
}
