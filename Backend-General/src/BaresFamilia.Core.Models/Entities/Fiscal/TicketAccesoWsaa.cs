using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Fiscal;

/// <summary>
/// Ticket de Acceso (TA) devuelto por WSAA para un servicio de ARCA.
/// Se persiste porque el TA dura unas 12 horas y ARCA rechaza pedidos de un TA nuevo
/// mientras el anterior siga vigente: mantenerlo solo en memoria haría fallar la
/// facturación tras cada reinicio del proceso.
/// </summary>
public class TicketAccesoWsaa : BaseEntity
{
    public Guid SucursalId { get; set; }

    public AmbienteFiscal Ambiente { get; set; } = AmbienteFiscal.Homologacion;

    /// <summary>
    /// Servicio de ARCA para el que se solicitó el TA (ej: "wsfe").
    /// </summary>
    public string Servicio { get; set; } = "wsfe";

    public string Token { get; set; } = string.Empty;
    public string Sign { get; set; } = string.Empty;

    public DateTime GeneradoEn { get; set; }
    public DateTime ExpiraEn { get; set; }

    // Navegación
    public Sucursal? Sucursal { get; set; }
}
