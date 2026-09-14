using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Core.Models.Entities.Fiscal;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Cliente de WSAA (autenticación de ARCA). Firma un Login Ticket Request con el certificado
/// de la sucursal, lo envía como CMS y guarda el Ticket de Acceso resultante.
/// </summary>
public class WsaaClient : IWsaaClient
{
    /// <summary>
    /// Un TA se renueva un poco antes de vencer para que ninguna facturación en curso quede
    /// con un ticket que expira a mitad del pedido a WSFE.
    /// </summary>
    private static readonly TimeSpan MargenRenovacion = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan DuracionSolicitada = TimeSpan.FromHours(12);

    /// <summary>
    /// Zona horaria argentina: ARCA espera los tiempos del Login Ticket Request con este offset.
    /// </summary>
    private static readonly TimeSpan OffsetArgentina = TimeSpan.FromHours(-3);

    /// <summary>
    /// WSAA rechaza un segundo pedido mientras el TA anterior siga vigente, así que dos cobros
    /// simultáneos de la misma sucursal no pueden solicitarlo a la vez.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _cerrojos = new();

    private readonly HttpClient _http;
    private readonly IService<ConfiguracionFiscalSucursal> _configuracionService;
    private readonly IService<TicketAccesoWsaa> _ticketService;
    private readonly IProtectorFiscal _protector;
    private readonly AfipSettings _settings;
    private readonly ILogger<WsaaClient> _logger;

    public WsaaClient(
        HttpClient http,
        IService<ConfiguracionFiscalSucursal> configuracionService,
        IService<TicketAccesoWsaa> ticketService,
        IProtectorFiscal protector,
        IOptions<AfipSettings> settings,
        ILogger<WsaaClient> logger)
    {
        _http = http;
        _configuracionService = configuracionService;
        _ticketService = ticketService;
        _protector = protector;
        _settings = settings.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSegundos);
    }

    public async Task<TicketAccesoWsaa> ObtenerTicketAccesoAsync(
        Guid sucursalId, string servicio = "wsfe", CancellationToken ct = default)
    {
        var configuraciones = await _configuracionService.FindAsync(c => c.SucursalId == sucursalId, ct);
        var configuracion = configuraciones.FirstOrDefault()
            ?? throw new InvalidOperationException("La sucursal no tiene configuración fiscal cargada.");

        if (configuracion.CertificadoCifrado is null)
            throw new InvalidOperationException("La sucursal no tiene certificado digital cargado.");

        var ambiente = configuracion.Ambiente;

        var vigente = await BuscarTicketVigenteAsync(sucursalId, ambiente, servicio, ct);
        if (vigente is not null)
            return vigente;

        var cerrojo = _cerrojos.GetOrAdd($"{sucursalId}|{ambiente}|{servicio}", _ => new SemaphoreSlim(1, 1));
        await cerrojo.WaitAsync(ct);
        try
        {
            // Otro pedido pudo haber renovado el TA mientras esperábamos el cerrojo.
            vigente = await BuscarTicketVigenteAsync(sucursalId, ambiente, servicio, ct);
            if (vigente is not null)
                return vigente;

            return await SolicitarTicketAsync(configuracion, servicio, ct);
        }
        finally
        {
            cerrojo.Release();
        }
    }

    private async Task<TicketAccesoWsaa?> BuscarTicketVigenteAsync(
        Guid sucursalId, AmbienteFiscal ambiente, string servicio, CancellationToken ct)
    {
        var tickets = await _ticketService.FindAsync(
            t => t.SucursalId == sucursalId && t.Ambiente == ambiente && t.Servicio == servicio, ct);

        var ticket = tickets.FirstOrDefault();
        if (ticket is null || ticket.ExpiraEn - MargenRenovacion <= DateTime.UtcNow)
            return null;

        return ticket;
    }

    private async Task<TicketAccesoWsaa> SolicitarTicketAsync(
        ConfiguracionFiscalSucursal configuracion, string servicio, CancellationToken ct)
    {
        using var certificado = CargarCertificado(configuracion);

        var loginTicketRequest = ConstruirLoginTicketRequest(servicio);
        var cmsFirmado = FirmarCms(loginTicketRequest, certificado);

        var url = _settings.UrlWsaa(configuracion.Ambiente);
        _logger.LogInformation(
            "Solicitando TA a WSAA para la sucursal {SucursalId} (servicio {Servicio}, ambiente {Ambiente}).",
            configuracion.SucursalId, servicio, configuracion.Ambiente);

        var respuesta = await InvocarLoginCmsAsync(url, cmsFirmado, ct);
        var (token, sign, generadoEn, expiraEn) = ParsearLoginTicketResponse(respuesta);

        var tickets = await _ticketService.FindAsync(
            t => t.SucursalId == configuracion.SucursalId
                 && t.Ambiente == configuracion.Ambiente
                 && t.Servicio == servicio,
            ct);

        var ticket = tickets.FirstOrDefault();
        var esNuevo = ticket is null;
        ticket ??= new TicketAccesoWsaa
        {
            SucursalId = configuracion.SucursalId,
            Ambiente = configuracion.Ambiente,
            Servicio = servicio
        };

        ticket.Token = token;
        ticket.Sign = sign;
        ticket.GeneradoEn = generadoEn;
        ticket.ExpiraEn = expiraEn;

        if (esNuevo)
            await _ticketService.CreateAsync(ticket, ct);
        else
            await _ticketService.UpdateAsync(ticket, ct);

        _logger.LogInformation(
            "TA obtenido para la sucursal {SucursalId}: vence {ExpiraEn:yyyy-MM-dd HH:mm} UTC.",
            configuracion.SucursalId, expiraEn);

        return ticket;
    }

    private X509Certificate2 CargarCertificado(ConfiguracionFiscalSucursal configuracion)
    {
        try
        {
            var contenido = _protector.Desproteger(configuracion.CertificadoCifrado!);
            var password = configuracion.CertificadoPasswordCifrada is null
                ? string.Empty
                : _protector.DesprotegerTexto(configuracion.CertificadoPasswordCifrada);

            // La clave privada debe ser exportable para poder firmar el CMS en memoria.
            return X509CertificateLoader.LoadPkcs12(
                contenido, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "No se pudo abrir el certificado almacenado de la sucursal. Volvé a cargarlo desde el Backoffice.", ex);
        }
    }

    /// <summary>
    /// Arma el Login Ticket Request que exige WSAA. El uniqueId identifica el pedido y los
    /// tiempos se envían con el offset argentino, tal como los valida ARCA.
    /// </summary>
    private static string ConstruirLoginTicketRequest(string servicio)
    {
        var ahora = DateTimeOffset.UtcNow.ToOffset(OffsetArgentina);

        var documento = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("loginTicketRequest",
                new XAttribute("version", "1.0"),
                new XElement("header",
                    new XElement("uniqueId", ahora.ToUnixTimeSeconds() % uint.MaxValue),
                    new XElement("generationTime", ahora.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)),
                    new XElement("expirationTime", ahora.Add(DuracionSolicitada).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture))),
                new XElement("service", servicio)));

        return documento.ToString(SaveOptions.DisableFormatting);
    }

    private static string FirmarCms(string loginTicketRequest, X509Certificate2 certificado)
    {
        var contenido = new ContentInfo(Encoding.UTF8.GetBytes(loginTicketRequest));
        var cms = new SignedCms(contenido);

        var firmante = new CmsSigner(certificado)
        {
            DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"), // SHA-256
            IncludeOption = X509IncludeOption.EndCertOnly
        };

        cms.ComputeSignature(firmante, silent: true);
        return Convert.ToBase64String(cms.Encode());
    }

    private async Task<string> InvocarLoginCmsAsync(string url, string cmsBase64, CancellationToken ct)
    {
        var sobre = $"""
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:wsaa="http://wsaa.view.sua.dvadac.desarrollo.afip.gov">
              <soapenv:Header/>
              <soapenv:Body>
                <wsaa:loginCms>
                  <wsaa:in0>{cmsBase64}</wsaa:in0>
                </wsaa:loginCms>
              </soapenv:Body>
            </soapenv:Envelope>
            """;

        using var contenido = new StringContent(sobre, Encoding.UTF8, "text/xml");
        contenido.Headers.Add("SOAPAction", "");

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.PostAsync(url, contenido, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"No se pudo contactar a WSAA en {url}. Verificá la conexión a internet de la sucursal.", ex);
        }

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        // WSAA devuelve los errores de negocio como SOAP Fault con status 500.
        var falla = ExtraerFaultString(cuerpo);
        if (falla is not null)
            throw new InvalidOperationException(TraducirFalla(falla));

        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"WSAA respondió {(int)respuesta.StatusCode}: {cuerpo}");

        return cuerpo;
    }

    private static string? ExtraerFaultString(string respuestaSoap)
    {
        try
        {
            return XDocument.Parse(respuestaSoap)
                            .Descendants()
                            .FirstOrDefault(e => e.Name.LocalName == "faultstring")
                            ?.Value;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string TraducirFalla(string faultString)
    {
        if (faultString.Contains("ya posee un TA valido", StringComparison.OrdinalIgnoreCase))
            return "ARCA informa que ya existe un Ticket de Acceso vigente para este CUIT y servicio, " +
                   "emitido por otro equipo o instalación. Esperá a que venza o usá el mismo ticket.";

        if (faultString.Contains("Computador no autorizado", StringComparison.OrdinalIgnoreCase))
            return "El certificado no está autorizado para este servicio en ARCA. Verificá que esté " +
                   "asociado al Web Service de Facturación Electrónica en el Administrador de Relaciones.";

        if (faultString.Contains("certificado", StringComparison.OrdinalIgnoreCase) ||
            faultString.Contains("CMS", StringComparison.OrdinalIgnoreCase))
            return $"ARCA rechazó el certificado: {faultString}";

        return $"WSAA rechazó la solicitud: {faultString}";
    }

    private static (string Token, string Sign, DateTime GeneradoEn, DateTime ExpiraEn) ParsearLoginTicketResponse(string respuestaSoap)
    {
        var retorno = XDocument.Parse(respuestaSoap)
                               .Descendants()
                               .FirstOrDefault(e => e.Name.LocalName == "loginCmsReturn")
                               ?.Value
            ?? throw new InvalidOperationException("La respuesta de WSAA no contiene el Ticket de Acceso.");

        var ticket = XDocument.Parse(retorno);

        var credenciales = ticket.Descendants("credentials").FirstOrDefault()
            ?? throw new InvalidOperationException("El Ticket de Acceso devuelto por WSAA no contiene credenciales.");

        var token = credenciales.Element("token")?.Value
            ?? throw new InvalidOperationException("El Ticket de Acceso devuelto por WSAA no contiene token.");
        var sign = credenciales.Element("sign")?.Value
            ?? throw new InvalidOperationException("El Ticket de Acceso devuelto por WSAA no contiene sign.");

        var encabezado = ticket.Descendants("header").FirstOrDefault();
        var generadoEn = LeerFecha(encabezado?.Element("generationTime")?.Value) ?? DateTime.UtcNow;
        var expiraEn = LeerFecha(encabezado?.Element("expirationTime")?.Value) ?? DateTime.UtcNow.Add(DuracionSolicitada);

        return (token, sign, generadoEn, expiraEn);
    }

    private static DateTime? LeerFecha(string? valor) =>
        DateTimeOffset.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha.UtcDateTime
            : null;
}
