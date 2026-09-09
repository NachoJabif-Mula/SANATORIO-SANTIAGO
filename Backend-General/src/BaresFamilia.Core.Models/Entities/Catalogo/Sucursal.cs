using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Representa una sucursal/local del negocio.
/// </summary>
public class Sucursal : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;

    // ═══════════════════════════════════════════════════════════
    // Datos Fiscales del Emisor (AFIP/ARCA)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// CUIT del contribuyente emisor (ej: "20-12345678-9"). Null si no se factura.
    /// </summary>
    public string? Cuit { get; set; }

    /// <summary>
    /// Razón social o nombre legal del contribuyente.
    /// </summary>
    public string? RazonSocial { get; set; }

    /// <summary>
    /// Domicilio fiscal del emisor (para el pie de factura).
    /// </summary>
    public string? DomicilioFiscal { get; set; }

    /// <summary>
    /// Condición frente al IVA del emisor.
    /// 1 = Responsable Inscripto, 4 = Exento, 5 = Consumidor Final, 6 = Monotributista.
    /// </summary>
    public int? CondicionIva { get; set; }

    /// <summary>
    /// Número de punto de venta habilitado en AFIP (ej: 1, 2, 3).
    /// </summary>
    public int PuntoDeVenta { get; set; } = 1;

    /// <summary>
    /// Número de inscripción en Ingresos Brutos (para el pie de factura).
    /// </summary>
    public string? NumeroIIBB { get; set; }

    /// <summary>
    /// Fecha de inicio de actividades ante AFIP (para el pie de factura).
    /// </summary>
    public DateTime? FechaInicioActividades { get; set; }

    // Navegación
    public ICollection<Usuario> Usuarios { get; set; } = [];
    public ICollection<ProductoPrecio> ProductoPrecios { get; set; } = [];
    public ICollection<Mesa> Mesas { get; set; } = [];
    public ICollection<Caja> Cajas { get; set; } = [];
    public ICollection<StockSucursal> StockSucursales { get; set; } = [];
    public ICollection<ConfiguracionPos> ConfiguracionesPos { get; set; } = [];
    public ICollection<Impresora> Impresoras { get; set; } = [];
}
