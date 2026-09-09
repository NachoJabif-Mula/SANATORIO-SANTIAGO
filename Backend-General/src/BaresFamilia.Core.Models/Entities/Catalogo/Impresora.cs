using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Impresora térmica configurada por sucursal.
/// Puede ser de tipo Red (TCP/IP) o USB (spooler Windows).
/// Soporta dispositivos fiscales (Epson, Hasar, Moretti) y no fiscales (comanderas).
/// </summary>
public class Impresora : BaseEntity
{
    public Guid SucursalId { get; set; }

    /// <summary>
    /// Nombre descriptivo (ej: "Caja Principal", "Cocina", "Fiscal Epson").
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de dispositivo: determina el driver y protocolo de comunicación.
    /// </summary>
    public TipoDispositivoImpresora TipoDispositivo { get; set; } = TipoDispositivoImpresora.Comandera;

    /// <summary>
    /// Tipo de conexión física: Red o USB.
    /// </summary>
    public TipoConexionImpresora TipoConexion { get; set; } = TipoConexionImpresora.Red;

    /// <summary>
    /// Dirección de la impresora: IP para Red, nombre de impresora para USB,
    /// "COM3" o "USB" para impresoras fiscales Epson.
    /// </summary>
    public string Direccion { get; set; } = string.Empty;

    /// <summary>
    /// Puerto TCP para impresoras de red (por defecto 9100).
    /// Para Hasar fiscal es típicamente 80 o 5000.
    /// </summary>
    public int Puerto { get; set; } = 9100;

    /// <summary>
    /// Velocidad del puerto serie (BaudRate) para impresoras fiscales Epson/Moretti.
    /// Valores típicos: 9600, 115200.
    /// </summary>
    public int Velocidad { get; set; } = 9600;

    // Navegación
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<ImpresoraTicketTipo> TicketTiposHabilitados { get; set; } = [];
}
