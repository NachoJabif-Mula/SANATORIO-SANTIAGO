using System.Globalization;
using System.Text;
using System.Xml.Linq;
using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Core.Models.Contratos.Fiscal;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Cliente SOAP de WSFEv1 (Factura Electrónica de ARCA).
///
/// Se arma el XML a mano en vez de generar un proxy WCF: el contrato es estable, son tres
/// operaciones, y así se conserva el XML crudo de ida y vuelta para auditoría.
/// </summary>
public class WsfeClient : IWsfeClient
{
    private const string EspacioNombres = "http://ar.gov.afip.dif.FEV1/";

    private readonly HttpClient _http;
    private readonly IWsaaClient _wsaaClient;
    private readonly IRepository<Sucursal> _sucursalRepository;
    private readonly IRepository<Entities.Fiscal.ConfiguracionFiscalSucursal> _configuracionRepository;
    private readonly AfipSettings _settings;
    private readonly ILogger<WsfeClient> _logger;

    public WsfeClient(
        HttpClient http,
        IWsaaClient wsaaClient,
        IRepository<Sucursal> sucursalRepository,
        IRepository<Entities.Fiscal.ConfiguracionFiscalSucursal> configuracionRepository,
        IOptions<AfipSettings> settings,
        ILogger<WsfeClient> logger)
    {
        _http = http;
        _wsaaClient = wsaaClient;
        _sucursalRepository = sucursalRepository;
        _configuracionRepository = configuracionRepository;
        _settings = settings.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSegundos);
    }

    public async Task<long> ConsultarUltimoAutorizadoAsync(
        Guid sucursalId, int puntoVenta, TipoComprobanteAfip tipoComprobante, CancellationToken ct = default)
    {
        var (auth, ambiente) = await ConstruirAutenticacionAsync(sucursalId, ct);

        var cuerpo = new XElement(XName.Get("FECompUltimoAutorizado", EspacioNombres),
            auth,
            new XElement(XName.Get("PtoVta", EspacioNombres), puntoVenta),
            new XElement(XName.Get("CbteTipo", EspacioNombres), (int)tipoComprobante));

        var respuesta = await InvocarAsync(ambiente, "FECompUltimoAutorizado", cuerpo, ct);
        var documento = XDocument.Parse(respuesta);

        var errores = LeerErrores(documento);
        if (errores is not null)
            throw new InvalidOperationException($"ARCA rechazó la consulta de numeración: {errores}");

        var numero = BuscarValor(documento, "CbteNro");
        return long.TryParse(numero, out var ultimo) ? ultimo : 0;
    }

    public async Task<RespuestaCae> SolicitarCaeAsync(Guid sucursalId, SolicitudCae solicitud, CancellationToken ct = default)
    {
        var (auth, ambiente) = await ConstruirAutenticacionAsync(sucursalId, ct);

        var detalle = ConstruirDetalle(solicitud);

        var cuerpo = new XElement(XName.Get("FECAESolicitar", EspacioNombres),
            auth,
            new XElement(XName.Get("FeCAEReq", EspacioNombres),
                new XElement(XName.Get("FeCabReq", EspacioNombres),
                    new XElement(XName.Get("CantReg", EspacioNombres), 1),
                    new XElement(XName.Get("PtoVta", EspacioNombres), solicitud.PuntoVenta),
                    new XElement(XName.Get("CbteTipo", EspacioNombres), (int)solicitud.TipoComprobante)),
                new XElement(XName.Get("FeDetReq", EspacioNombres), detalle)));

        var sobreEnviado = ConstruirSobre(cuerpo);
        var respuesta = await InvocarAsync(ambiente, "FECAESolicitar", cuerpo, ct);

        return InterpretarRespuestaCae(sobreEnviado, respuesta);
    }

    public async Task<EstadoServiciosArca> ConsultarEstadoServiciosAsync(AmbienteFiscal ambiente, CancellationToken ct = default)
    {
        var cuerpo = new XElement(XName.Get("FEDummy", EspacioNombres));
        var respuesta = await InvocarAsync(ambiente, "FEDummy", cuerpo, ct);
        var documento = XDocument.Parse(respuesta);

        return new EstadoServiciosArca(
            BuscarValor(documento, "AppServer") ?? "?",
            BuscarValor(documento, "DbServer") ?? "?",
            BuscarValor(documento, "AuthServer") ?? "?");
    }

    /// <summary>
    /// El orden de los elementos es el de la definición del servicio: WSFEv1 valida contra
    /// el XSD y rechaza el pedido si se alteran.
    /// </summary>
    private static XElement ConstruirDetalle(SolicitudCae solicitud)
    {
        var detalle = new XElement(XName.Get("FECAEDetRequest", EspacioNombres),
            // 1 = Productos: en un bar no hay prestaciones de servicio con período a informar.
            new XElement(XName.Get("Concepto", EspacioNombres), 1),
            new XElement(XName.Get("DocTipo", EspacioNombres), solicitud.TipoDocumentoReceptor),
            new XElement(XName.Get("DocNro", EspacioNombres), solicitud.NumeroDocumentoReceptor),
            new XElement(XName.Get("CbteDesde", EspacioNombres), solicitud.NumeroComprobante),
            new XElement(XName.Get("CbteHasta", EspacioNombres), solicitud.NumeroComprobante),
            new XElement(XName.Get("CbteFch", EspacioNombres), solicitud.FechaComprobante.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
            new XElement(XName.Get("ImpTotal", EspacioNombres), Importe(solicitud.ImporteTotal)),
            new XElement(XName.Get("ImpTotConc", EspacioNombres), Importe(solicitud.ImporteNoGravado)),
            new XElement(XName.Get("ImpNeto", EspacioNombres), Importe(solicitud.ImporteNeto)),
            new XElement(XName.Get("ImpOpEx", EspacioNombres), Importe(solicitud.ImporteExento)),
            new XElement(XName.Get("ImpTrib", EspacioNombres), Importe(0m)),
            new XElement(XName.Get("ImpIVA", EspacioNombres), Importe(solicitud.ImporteIva)),
            new XElement(XName.Get("MonId", EspacioNombres), "PES"),
            new XElement(XName.Get("MonCotiz", EspacioNombres), Importe(1m)),
            new XElement(XName.Get("CondicionIVAReceptorId", EspacioNombres), solicitud.CondicionIvaReceptor));

        // Los comprobantes clase C no discriminan IVA: enviar el detalle hace que ARCA rechace.
        if (solicitud.Alicuotas.Count > 0 && !EsClaseC(solicitud.TipoComprobante))
        {
            detalle.Add(new XElement(XName.Get("Iva", EspacioNombres),
                solicitud.Alicuotas.Select(a =>
                    new XElement(XName.Get("AlicIva", EspacioNombres),
                        new XElement(XName.Get("Id", EspacioNombres), (int)a.Alicuota),
                        new XElement(XName.Get("BaseImp", EspacioNombres), Importe(a.BaseImponible)),
                        new XElement(XName.Get("Importe", EspacioNombres), Importe(a.Importe))))));
        }

        return detalle;
    }

    private static bool EsClaseC(TipoComprobanteAfip tipo) =>
        tipo is TipoComprobanteAfip.FacturaC or TipoComprobanteAfip.NotaDebitoC or TipoComprobanteAfip.NotaCreditoC;

    private static string Importe(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    private async Task<(XElement Auth, AmbienteFiscal Ambiente)> ConstruirAutenticacionAsync(Guid sucursalId, CancellationToken ct)
    {
        var sucursal = await _sucursalRepository.GetByIdAsync(sucursalId, ct)
            ?? throw new InvalidOperationException("Sucursal no encontrada.");

        var cuit = SoloDigitos(sucursal.Cuit);
        if (string.IsNullOrEmpty(cuit))
            throw new InvalidOperationException("La sucursal no tiene CUIT cargado: no se puede facturar.");

        var configuraciones = await _configuracionRepository.FindAsync(c => c.SucursalId == sucursalId, ct);
        var ambiente = configuraciones.FirstOrDefault()?.Ambiente
            ?? throw new InvalidOperationException("La sucursal no tiene configuración fiscal cargada.");

        var ticket = await _wsaaClient.ObtenerTicketAccesoAsync(sucursalId, "wsfe", ct);

        var auth = new XElement(XName.Get("Auth", EspacioNombres),
            new XElement(XName.Get("Token", EspacioNombres), ticket.Token),
            new XElement(XName.Get("Sign", EspacioNombres), ticket.Sign),
            new XElement(XName.Get("Cuit", EspacioNombres), cuit));

        return (auth, ambiente);
    }

    private static string SoloDigitos(string? valor) =>
        valor is null ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());

    private static string ConstruirSobre(XElement cuerpo)
    {
        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";

        return new XDocument(
            new XElement(soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap", soap),
                new XElement(soap + "Body", cuerpo)))
            .ToString(SaveOptions.DisableFormatting);
    }

    private async Task<string> InvocarAsync(AmbienteFiscal ambiente, string operacion, XElement cuerpo, CancellationToken ct)
    {
        var url = _settings.UrlWsfe(ambiente);
        var sobre = ConstruirSobre(cuerpo);

        using var contenido = new StringContent(sobre, Encoding.UTF8, "text/xml");
        contenido.Headers.Add("SOAPAction", $"{EspacioNombres}{operacion}");

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.PostAsync(url, contenido, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"No se pudo contactar a WSFE en {url}. Verificá la conexión a internet de la sucursal.", ex);
        }

        var cuerpoRespuesta = await respuesta.Content.ReadAsStringAsync(ct);

        var falla = BuscarValorPorNombre(cuerpoRespuesta, "faultstring");
        if (falla is not null)
            throw new InvalidOperationException($"WSFE rechazó la solicitud: {falla}");

        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"WSFE respondió {(int)respuesta.StatusCode} en {operacion}.");

        return cuerpoRespuesta;
    }

    private RespuestaCae InterpretarRespuestaCae(string requestXml, string responseXml)
    {
        var documento = XDocument.Parse(responseXml);

        var errores = LeerErrores(documento);
        var observaciones = LeerObservaciones(documento);

        // "A" = aprobado, "P" = parcial, "R" = rechazado.
        var resultado = BuscarValor(documento, "Resultado");
        var cae = BuscarValor(documento, "CAE");
        var vencimiento = LeerFechaArca(BuscarValor(documento, "CAEFchVto"));

        var autorizado = string.Equals(resultado, "A", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrWhiteSpace(cae);

        if (!autorizado)
        {
            _logger.LogWarning(
                "WSFE no autorizó el comprobante. Resultado={Resultado} Errores={Errores} Observaciones={Observaciones}",
                resultado, errores, observaciones);
        }

        return new RespuestaCae(autorizado, cae, vencimiento, observaciones, errores, requestXml, responseXml);
    }

    private static string? LeerErrores(XDocument documento) =>
        FormatearMensajes(documento.Descendants().Where(e => e.Name.LocalName == "Err"));

    private static string? LeerObservaciones(XDocument documento) =>
        FormatearMensajes(documento.Descendants().Where(e => e.Name.LocalName == "Obs"));

    private static string? FormatearMensajes(IEnumerable<XElement> elementos)
    {
        var mensajes = elementos
            .Select(e => new
            {
                Codigo = e.Elements().FirstOrDefault(x => x.Name.LocalName == "Code")?.Value,
                Mensaje = e.Elements().FirstOrDefault(x => x.Name.LocalName == "Msg")?.Value
            })
            .Where(m => m.Codigo is not null || m.Mensaje is not null)
            .Select(m => $"[{m.Codigo}] {m.Mensaje}")
            .ToList();

        return mensajes.Count == 0 ? null : string.Join(" | ", mensajes);
    }

    private static string? BuscarValor(XDocument documento, string nombreLocal) =>
        documento.Descendants().FirstOrDefault(e => e.Name.LocalName == nombreLocal)?.Value;

    private static string? BuscarValorPorNombre(string xml, string nombreLocal)
    {
        try
        {
            return BuscarValor(XDocument.Parse(xml), nombreLocal);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static DateTime? LeerFechaArca(string? valor) =>
        DateTime.TryParseExact(valor, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
            : null;
}
