using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Configuration;

/// <summary>
/// Endpoints y tiempos de espera de los Web Services de ARCA (ex-AFIP).
/// Se mapea desde appsettings.json sección "Afip".
///
/// El ambiente no vive acá sino en la configuración fiscal de cada sucursal: dos sucursales
/// de la misma instalación pueden estar una en homologación y otra en producción.
/// </summary>
public class AfipSettings
{
    public const string SectionName = "Afip";

    public int TimeoutSegundos { get; set; } = 30;

    public string WsaaUrlHomologacion { get; set; } = "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";
    public string WsaaUrlProduccion { get; set; } = "https://wsaa.afip.gov.ar/ws/services/LoginCms";

    public string WsfeUrlHomologacion { get; set; } = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";
    public string WsfeUrlProduccion { get; set; } = "https://servicios1.afip.gov.ar/wsfev1/service.asmx";

    public string QrBaseUrl { get; set; } = "https://www.afip.gob.ar/fe/qr/?p=";

    public string UrlWsaa(AmbienteFiscal ambiente) =>
        ambiente == AmbienteFiscal.Produccion ? WsaaUrlProduccion : WsaaUrlHomologacion;

    public string UrlWsfe(AmbienteFiscal ambiente) =>
        ambiente == AmbienteFiscal.Produccion ? WsfeUrlProduccion : WsfeUrlHomologacion;
}
