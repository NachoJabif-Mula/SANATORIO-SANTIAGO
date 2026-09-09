using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace BaresFamilia.Fiscal.Engine.Drivers;

/// <summary>
/// Driver nativo propio para controladores fiscales Hasar de 2da Generación (SMH/PT-250AF 2G).
/// Se comunica mediante solicitudes HTTP POST con payloads XML enviadas al servidor web interno de la impresora.
/// Escrito 100% desde cero para el proyecto Bares Familia.
/// </summary>
public class HasarDriver : IFiscalDriver
{
    public string NombreDriver => "Hasar";

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<FiscalResult> ConsultarEstadoAsync(FiscalDeviceConfig config, CancellationToken ct = default)
    {
        try
        {
            var xml = new XElement("hasar",
                new XElement("command",
                    new XAttribute("name", "ConsultarEstado")
                )
            );

            var responseXml = await EnviarXmlAsync(config, xml, ct);
            var statusElement = responseXml.Element("status");
            if (statusElement != null)
            {
                return FiscalResult.Ok("OK", cae: statusElement.Attribute("code")?.Value ?? "0");
            }
            return FiscalResult.Ok("OK_HASAR");
        }
        catch (Exception ex)
        {
            return FiscalResult.Error($"Error al conectar con impresora Hasar en {config.Ip}:{config.PuertoTcp} — {ex.Message}");
        }
    }

    public Task<FiscalResult> ImprimirFacturaAsync(FiscalDeviceConfig config, FiscalFacturaRequest request, CancellationToken ct = default)
    {
        return ImprimirDocumentoAsync(config, request, esNotaCredito: false, ct);
    }

    public Task<FiscalResult> ImprimirNotaCreditoAsync(FiscalDeviceConfig config, FiscalFacturaRequest request, CancellationToken ct = default)
    {
        return ImprimirDocumentoAsync(config, request, esNotaCredito: true, ct);
    }

    private async Task<FiscalResult> ImprimirDocumentoAsync(FiscalDeviceConfig config, FiscalFacturaRequest request, bool esNotaCredito, CancellationToken ct)
    {
        try
        {
            string tipoDocHasar = MapearTipoDocumentoHasar(request.TipoComprobante, esNotaCredito);
            var cliente = request.Cliente;

            // Construir payload XML Hasar 2G
            var xmlDoc = new XElement("hasar",
                // 1. Cargar Datos Cliente
                new XElement("command",
                    new XAttribute("name", "CargarDatosCliente"),
                    new XElement("parameter", new XAttribute("name", "RazonSocial"), cliente.Nombre),
                    new XElement("parameter", new XAttribute("name", "NroDocumento"), cliente.Cuit.Replace("-", "")),
                    new XElement("parameter", new XAttribute("name", "TipoDocumento"), string.IsNullOrWhiteSpace(cliente.Cuit) ? "T_SIN_DOCUMENTO" : "T_CUIT"),
                    new XElement("parameter", new XAttribute("name", "ResponsabilidadIVA"), MapearCondicionIvaHasar(cliente.CondicionIva)),
                    new XElement("parameter", new XAttribute("name", "Domicilio"), cliente.Domicilio)
                ),
                // 2. Abrir Documento
                new XElement("command",
                    new XAttribute("name", "AbrirDocumento"),
                    new XElement("parameter", new XAttribute("name", "TipoDocumento"), tipoDocHasar)
                )
            );

            // 3. Agregar Items
            foreach (var item in request.Items)
            {
                xmlDoc.Add(new XElement("command",
                    new XAttribute("name", "ImprimirItem"),
                    new XElement("parameter", new XAttribute("name", "Texto"), item.Descripcion),
                    new XElement("parameter", new XAttribute("name", "Cantidad"), item.Cantidad.ToString("F3", CultureInfo.InvariantCulture)),
                    new XElement("parameter", new XAttribute("name", "PrecioUnitario"), item.PrecioUnitarioConIva.ToString("F4", CultureInfo.InvariantCulture)),
                    new XElement("parameter", new XAttribute("name", "AlicuotaIVA"), MapearAlicuotaIvaHasar(item.AlicuotaIvaCode))
                ));
            }

            // 4. Descuento general si aplica
            if (request.DescuentoGeneralMonto > 0)
            {
                xmlDoc.Add(new XElement("command",
                    new XAttribute("name", "ImprimirAjuste"),
                    new XElement("parameter", new XAttribute("name", "Texto"), "Descuento General"),
                    new XElement("parameter", new XAttribute("name", "Monto"), request.DescuentoGeneralMonto.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement("parameter", new XAttribute("name", "Tipo"), "T_DESCUENTO")
                ));
            }

            // 5. Cargar Pagos
            foreach (var pago in request.Pagos)
            {
                xmlDoc.Add(new XElement("command",
                    new XAttribute("name", "ImprimirPago"),
                    new XElement("parameter", new XAttribute("name", "Texto"), pago.Descripcion),
                    new XElement("parameter", new XAttribute("name", "Monto"), pago.Monto.ToString("F2", CultureInfo.InvariantCulture))
                ));
            }

            // 6. Cerrar Documento
            xmlDoc.Add(new XElement("command",
                new XAttribute("name", "CerrarDocumento")
            ));

            var responseXml = await EnviarXmlAsync(config, xmlDoc, ct);

            // Extraer respuesta del comprobante
            var cerrarResp = responseXml.Elements("response").FirstOrDefault(e => e.Attribute("name")?.Value == "CerrarDocumento");
            string nroComprobante = cerrarResp?.Element("parameter")?.Attribute("value")?.Value ?? "1";
            string cae = responseXml.Descendants("parameter").FirstOrDefault(p => p.Attribute("name")?.Value == "CAE")?.Attribute("value")?.Value ?? "";

            return FiscalResult.Ok(nroComprobante, cae);
        }
        catch (Exception ex)
        {
            // Intentar cancelar automáticamente en caso de fallo intermedio
            try { await CancelarComprobanteAsync(config, ct); } catch { }
            return FiscalResult.Error($"Error durante facturación en Hasar: {ex.Message}");
        }
    }

    public async Task<FiscalResult> EjecutarCierreZAsync(FiscalDeviceConfig config, CancellationToken ct = default)
    {
        try
        {
            var xml = new XElement("hasar",
                new XElement("command",
                    new XAttribute("name", "CierreZ")
                )
            );
            var res = await EnviarXmlAsync(config, xml, ct);
            return FiscalResult.Ok("CierreZ_Hasar_OK");
        }
        catch (Exception ex)
        {
            return FiscalResult.Error($"Error al ejecutar Cierre Z en Hasar: {ex.Message}");
        }
    }

    public async Task<FiscalResult> EjecutarCierreXAsync(FiscalDeviceConfig config, CancellationToken ct = default)
    {
        try
        {
            var xml = new XElement("hasar",
                new XElement("command",
                    new XAttribute("name", "CierreX")
                )
            );
            await EnviarXmlAsync(config, xml, ct);
            return FiscalResult.Ok("CierreX_Hasar_OK");
        }
        catch (Exception ex)
        {
            return FiscalResult.Error($"Error al ejecutar Cierre X en Hasar: {ex.Message}");
        }
    }

    public async Task<FiscalResult> CancelarComprobanteAsync(FiscalDeviceConfig config, CancellationToken ct = default)
    {
        try
        {
            var xml = new XElement("hasar",
                new XElement("command",
                    new XAttribute("name", "Cancelar")
                )
            );
            await EnviarXmlAsync(config, xml, ct);
            return FiscalResult.Ok("Cancelado");
        }
        catch (Exception ex)
        {
            return FiscalResult.Error($"Error al cancelar comprobante Hasar: {ex.Message}");
        }
    }

    private async Task<XElement> EnviarXmlAsync(FiscalDeviceConfig config, XElement xmlContent, CancellationToken ct)
    {
        string url = $"http://{config.Ip}:{config.PuertoTcp}/fiscal.xml";
        var content = new StringContent(xmlContent.ToString(), Encoding.UTF8, "application/xml");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        using var response = await _httpClient.SendAsync(requestMessage, ct);

        response.EnsureSuccessStatusCode();
        string responseString = await response.Content.ReadAsStringAsync(ct);
        return XElement.Parse(responseString);
    }

    private static string MapearTipoDocumentoHasar(int tipoComprobante, bool esNc)
    {
        return (tipoComprobante, esNc) switch
        {
            (FiscalConstants.COMP_FACTURA_A, false) => "T_FAC_A",
            (FiscalConstants.COMP_FACTURA_B, false) => "T_FAC_B",
            (FiscalConstants.COMP_FACTURA_C, false) => "T_FAC_C",
            (FiscalConstants.COMP_NOTA_CREDITO_A, true) or (FiscalConstants.COMP_FACTURA_A, true) => "T_NC_A",
            (FiscalConstants.COMP_NOTA_CREDITO_B, true) or (FiscalConstants.COMP_FACTURA_B, true) => "T_NC_B",
            (FiscalConstants.COMP_NOTA_CREDITO_C, true) or (FiscalConstants.COMP_FACTURA_C, true) => "T_NC_C",
            _ => "T_FAC_B"
        };
    }

    private static string MapearCondicionIvaHasar(int condicionIva)
    {
        return condicionIva switch
        {
            FiscalConstants.COND_RESPONSABLE_INSCRIPTO => "T_RESPONSABLE_INSCRIPTO",
            FiscalConstants.COND_EXENTO => "T_EXENTO",
            FiscalConstants.COND_MONOTRIBUTO => "T_MONOTRIBUTO",
            _ => "T_CONSUMIDOR_FINAL"
        };
    }

    private static string MapearAlicuotaIvaHasar(int alicuotaCode)
    {
        return alicuotaCode switch
        {
            FiscalConstants.IVA_10_5 => "10.50",
            FiscalConstants.IVA_27 => "27.00",
            FiscalConstants.IVA_EXENTO => "0.00",
            _ => "21.00"
        };
    }
}
