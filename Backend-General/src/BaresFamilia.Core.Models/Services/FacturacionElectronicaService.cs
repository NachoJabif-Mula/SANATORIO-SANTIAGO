using System.Globalization;
using System.Text.Json;
using System.Text;
using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Core.Models.Contratos.Fiscal;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.Fiscal;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Fiscal.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Orquesta la emisión de comprobantes electrónicos contra WSFEv1.
/// </summary>
public class FacturacionElectronicaService : IFacturacionElectronicaService
{
    /// <summary>
    /// Documento del receptor cuando la venta es a consumidor final sin identificar.
    /// </summary>
    private const int DocumentoConsumidorFinal = 99;

    private readonly IWsfeClient _wsfeClient;
    private readonly IRepository<Sucursal> _sucursalRepository;
    private readonly IRepository<ConfiguracionFiscalSucursal> _configuracionRepository;
    private readonly IRepository<Comprobante> _comprobanteRepository;
    private readonly IRepository<ComprobanteAlicuota> _alicuotaRepository;
    private readonly AfipSettings _settings;
    private readonly ILogger<FacturacionElectronicaService> _logger;

    public FacturacionElectronicaService(
        IWsfeClient wsfeClient,
        IRepository<Sucursal> sucursalRepository,
        IRepository<ConfiguracionFiscalSucursal> configuracionRepository,
        IRepository<Comprobante> comprobanteRepository,
        IRepository<ComprobanteAlicuota> alicuotaRepository,
        IOptions<AfipSettings> settings,
        ILogger<FacturacionElectronicaService> logger)
    {
        _wsfeClient = wsfeClient;
        _sucursalRepository = sucursalRepository;
        _configuracionRepository = configuracionRepository;
        _comprobanteRepository = comprobanteRepository;
        _alicuotaRepository = alicuotaRepository;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Comprobante> EmitirDesdeComandaAsync(Comanda comanda, Guid sucursalId, CancellationToken ct = default)
    {
        var sucursal = await _sucursalRepository.GetByIdAsync(sucursalId, ct)
            ?? throw new InvalidOperationException("Sucursal no encontrada.");

        var configuraciones = await _configuracionRepository.FindAsync(c => c.SucursalId == sucursalId, ct);
        var configuracion = configuraciones.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"La sucursal '{sucursal.Nombre}' no tiene configuración fiscal cargada: no se puede facturar.");

        ValidarDatosDelEmisor(sucursal);

        // Sin CUIT del cliente no hay forma de emitir clase A: toda venta de mostrador se
        // factura a consumidor final.
        var condicionReceptor = FiscalConstants.COND_CONSUMIDOR_FINAL;
        var tipoComprobante = DeterminarTipoComprobante(sucursal.CondicionIva!.Value, condicionReceptor);

        var importes = CalcularImportes(comanda, discriminaIva: !EsClaseC(tipoComprobante));

        var comprobante = new Comprobante
        {
            SucursalId = sucursalId,
            ComandaId = comanda.Id,
            TipoComprobante = tipoComprobante,
            Ambiente = configuracion.Ambiente,
            PuntoVenta = sucursal.PuntoDeVenta,
            NumeroComprobante = 0,
            FechaEmision = DateTime.UtcNow,
            CuitEmisor = sucursal.Cuit!,
            RazonSocialEmisor = sucursal.RazonSocial!,
            CondicionIvaEmisor = sucursal.CondicionIva.Value,
            TipoDocumentoReceptor = DocumentoConsumidorFinal,
            NumeroDocumentoReceptor = null,
            RazonSocialReceptor = comanda.Cliente is null
                ? null
                : $"{comanda.Cliente.Nombre} {comanda.Cliente.Apellido}".Trim(),
            CondicionIvaReceptor = condicionReceptor,
            ImporteNeto = importes.Neto,
            ImporteIva = importes.Iva,
            ImporteExento = importes.Exento,
            ImporteNoGravado = importes.NoGravado,
            ImporteTotal = importes.Total,
            Estado = EstadoComprobante.Pendiente,
            SyncEstado = SyncEstado.Pendiente
        };

        await _comprobanteRepository.AddAsync(comprobante, ct);

        foreach (var detalle in importes.Alicuotas)
        {
            var alicuota = new ComprobanteAlicuota
            {
                ComprobanteId = comprobante.Id,
                Alicuota = detalle.Alicuota,
                BaseImponible = detalle.BaseImponible,
                Importe = detalle.Importe
            };

            await _alicuotaRepository.AddAsync(alicuota, ct);
            comprobante.Alicuotas.Add(alicuota);
        }

        return await AutorizarAsync(comprobante, importes.Alicuotas, ct);
    }

    public async Task<Comprobante> ReintentarAsync(Guid comprobanteId, CancellationToken ct = default)
    {
        var comprobante = await _comprobanteRepository.GetByIdAsync(comprobanteId, ct)
            ?? throw new KeyNotFoundException($"Comprobante '{comprobanteId}' no encontrado.");

        if (comprobante.Estado == EstadoComprobante.Emitido)
            return comprobante;

        var alicuotas = await _alicuotaRepository.FindAsync(a => a.ComprobanteId == comprobanteId, ct);
        var detalles = alicuotas
            .Select(a => new DetalleAlicuota(a.Alicuota, a.BaseImponible, a.Importe))
            .ToList();

        return await AutorizarAsync(comprobante, detalles, ct);
    }

    /// <summary>
    /// Pide el número correlativo a ARCA y solicita el CAE.
    ///
    /// Distingue dos fracasos que se tratan distinto: un rechazo de ARCA es un problema de
    /// datos que no se arregla reintentando, mientras que un fallo de conexión deja el
    /// comprobante pendiente para el próximo intento.
    /// </summary>
    private async Task<Comprobante> AutorizarAsync(
        Comprobante comprobante, IReadOnlyList<DetalleAlicuota> alicuotas, CancellationToken ct)
    {
        comprobante.IntentosEmision++;
        comprobante.FechaUltimoIntento = DateTime.UtcNow;

        try
        {
            var ultimo = await _wsfeClient.ConsultarUltimoAutorizadoAsync(
                comprobante.SucursalId, comprobante.PuntoVenta, comprobante.TipoComprobante, ct);

            var numero = ultimo + 1;

            var solicitud = new SolicitudCae(
                PuntoVenta: comprobante.PuntoVenta,
                TipoComprobante: comprobante.TipoComprobante,
                NumeroComprobante: numero,
                FechaComprobante: DateTime.UtcNow,
                TipoDocumentoReceptor: comprobante.TipoDocumentoReceptor,
                NumeroDocumentoReceptor: long.TryParse(comprobante.NumeroDocumentoReceptor, out var doc) ? doc : 0,
                CondicionIvaReceptor: comprobante.CondicionIvaReceptor,
                ImporteTotal: comprobante.ImporteTotal,
                ImporteNeto: comprobante.ImporteNeto,
                ImporteIva: comprobante.ImporteIva,
                ImporteExento: comprobante.ImporteExento,
                ImporteNoGravado: comprobante.ImporteNoGravado,
                Alicuotas: alicuotas);

            var respuesta = await _wsfeClient.SolicitarCaeAsync(comprobante.SucursalId, solicitud, ct);

            comprobante.RequestXml = respuesta.RequestXml;
            comprobante.ResponseXml = respuesta.ResponseXml;
            comprobante.ObservacionesArca = respuesta.Observaciones;

            if (respuesta.Autorizado)
            {
                comprobante.NumeroComprobante = numero;
                comprobante.Cae = respuesta.Cae;
                comprobante.CaeVencimiento = respuesta.CaeVencimiento;
                comprobante.FechaEmision = solicitud.FechaComprobante;
                comprobante.Estado = EstadoComprobante.Emitido;
                comprobante.UltimoError = null;
                comprobante.QrPayload = ConstruirQr(comprobante);

                _logger.LogInformation(
                    "Comprobante {Tipo} {PuntoVenta:D4}-{Numero:D8} autorizado. CAE {Cae} vence {Vencimiento:yyyy-MM-dd}.",
                    comprobante.TipoComprobante, comprobante.PuntoVenta, numero, comprobante.Cae, comprobante.CaeVencimiento);
            }
            else
            {
                comprobante.Estado = EstadoComprobante.Rechazado;
                comprobante.UltimoError = Recortar(respuesta.Errores ?? respuesta.Observaciones ?? "ARCA rechazó el comprobante sin detallar el motivo.");

                _logger.LogError(
                    "ARCA rechazó el comprobante de la comanda {ComandaId}: {Error}",
                    comprobante.ComandaId, comprobante.UltimoError);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            comprobante.Estado = EstadoComprobante.Pendiente;
            comprobante.UltimoError = Recortar(ex.Message);

            _logger.LogError(ex,
                "No se pudo autorizar el comprobante de la comanda {ComandaId}. Queda pendiente de reintento.",
                comprobante.ComandaId);
        }

        comprobante.SyncEstado = SyncEstado.Pendiente;
        comprobante.UpdatedAt = DateTime.UtcNow;
        await _comprobanteRepository.UpdateAsync(comprobante, ct);

        return comprobante;
    }

    private static void ValidarDatosDelEmisor(Sucursal sucursal)
    {
        var faltantes = new List<string>();

        if (string.IsNullOrWhiteSpace(sucursal.Cuit)) faltantes.Add("CUIT");
        if (string.IsNullOrWhiteSpace(sucursal.RazonSocial)) faltantes.Add("razón social");
        if (sucursal.CondicionIva is null) faltantes.Add("condición IVA");
        if (sucursal.PuntoDeVenta <= 0) faltantes.Add("punto de venta");

        if (faltantes.Count > 0)
            throw new InvalidOperationException(
                $"La sucursal '{sucursal.Nombre}' no puede facturar: faltan datos del emisor ({string.Join(", ", faltantes)}).");
    }

    /// <summary>
    /// Un monotributista o un exento emiten siempre clase C. Un responsable inscripto emite
    /// A cuando el receptor también lo es, y B en el resto de los casos.
    /// </summary>
    private static TipoComprobanteAfip DeterminarTipoComprobante(int condicionEmisor, int condicionReceptor)
    {
        if (condicionEmisor is FiscalConstants.COND_MONOTRIBUTO or FiscalConstants.COND_EXENTO)
            return TipoComprobanteAfip.FacturaC;

        return condicionReceptor == FiscalConstants.COND_RESPONSABLE_INSCRIPTO
            ? TipoComprobanteAfip.FacturaA
            : TipoComprobanteAfip.FacturaB;
    }

    private static bool EsClaseC(TipoComprobanteAfip tipo) =>
        tipo is TipoComprobanteAfip.FacturaC or TipoComprobanteAfip.NotaDebitoC or TipoComprobanteAfip.NotaCreditoC;

    private static decimal TasaIva(AlicuotaIva alicuota) => alicuota switch
    {
        AlicuotaIva.Iva105 => 0.105m,
        AlicuotaIva.Iva21 => 0.21m,
        AlicuotaIva.Iva27 => 0.27m,
        _ => 0m
    };

    /// <summary>
    /// Reparte el total efectivamente cobrado entre las alícuotas de los productos vendidos.
    ///
    /// Los precios del POS son finales (con IVA incluido), así que el neto se despeja hacia
    /// atrás. El reparto se cierra contra el total de la comanda, porque ARCA rechaza el
    /// comprobante si el total no coincide exactamente con la suma de sus partes.
    /// </summary>
    private static ImportesComprobante CalcularImportes(Comanda comanda, bool discriminaIva)
    {
        var total = Math.Round(comanda.Total, 2, MidpointRounding.AwayFromZero);

        if (!discriminaIva)
        {
            // Clase C: no se discrimina IVA, el total es todo neto.
            return new ImportesComprobante(total, 0m, 0m, 0m, total, []);
        }

        var grupos = (comanda.Items ?? [])
            .Where(i => !i.Cancelado)
            .GroupBy(i => i.Producto?.AlicuotaIva ?? AlicuotaIva.Iva21)
            .Select(g => new { Alicuota = g.Key, Bruto = g.Sum(i => i.Cantidad * i.PrecioUnitario) })
            .Where(g => g.Bruto > 0)
            .ToList();

        if (grupos.Count == 0)
            return new ImportesComprobante(total, 0m, 0m, 0m, total, []);

        // El descuento de la comanda se prorratea entre las alícuotas según su peso.
        var bruto = grupos.Sum(g => g.Bruto);
        var repartidos = grupos
            .Select(g => (g.Alicuota, Total: Math.Round(g.Bruto * total / bruto, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        // El redondeo del prorrateo puede dejar una diferencia de centavos: se absorbe en el
        // grupo de mayor importe para que la suma cierre exacta contra el total cobrado.
        var diferencia = total - repartidos.Sum(r => r.Total);
        if (diferencia != 0)
        {
            var indiceMayor = repartidos.IndexOf(repartidos.MaxBy(r => r.Total));
            repartidos[indiceMayor] = (repartidos[indiceMayor].Alicuota, repartidos[indiceMayor].Total + diferencia);
        }

        decimal neto = 0m, iva = 0m, exento = 0m, noGravado = 0m;
        var alicuotas = new List<DetalleAlicuota>();

        foreach (var grupo in repartidos)
        {
            switch (grupo.Alicuota)
            {
                case AlicuotaIva.NoGravado:
                    noGravado += grupo.Total;
                    break;

                case AlicuotaIva.Exento:
                    exento += grupo.Total;
                    break;

                default:
                    var baseImponible = Math.Round(grupo.Total / (1 + TasaIva(grupo.Alicuota)), 2, MidpointRounding.AwayFromZero);
                    var importeIva = grupo.Total - baseImponible;

                    neto += baseImponible;
                    iva += importeIva;
                    alicuotas.Add(new DetalleAlicuota(grupo.Alicuota, baseImponible, importeIva));
                    break;
            }
        }

        return new ImportesComprobante(neto, iva, exento, noGravado, total, alicuotas);
    }

    /// <summary>
    /// Arma el contenido del QR obligatorio (RG 4291): el JSON especificado por ARCA,
    /// codificado en base64 y colgado de la URL pública de verificación.
    /// </summary>
    private string ConstruirQr(Comprobante comprobante)
    {
        var datos = new Dictionary<string, object?>
        {
            ["ver"] = 1,
            ["fecha"] = comprobante.FechaEmision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["cuit"] = long.TryParse(new string(comprobante.CuitEmisor.Where(char.IsDigit).ToArray()), out var cuit) ? cuit : 0,
            ["ptoVta"] = comprobante.PuntoVenta,
            ["tipoCmp"] = (int)comprobante.TipoComprobante,
            ["nroCmp"] = comprobante.NumeroComprobante,
            ["importe"] = comprobante.ImporteTotal,
            ["moneda"] = "PES",
            ["ctz"] = 1,
            ["tipoDocRec"] = comprobante.TipoDocumentoReceptor,
            ["nroDocRec"] = long.TryParse(comprobante.NumeroDocumentoReceptor, out var doc) ? doc : 0,
            ["tipoCodAut"] = "E",
            ["codAut"] = long.TryParse(comprobante.Cae, out var cae) ? cae : 0
        };

        var json = JsonSerializer.Serialize(datos);
        return _settings.QrBaseUrl + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static string Recortar(string mensaje) =>
        mensaje.Length <= 1000 ? mensaje : mensaje[..1000];

    private record ImportesComprobante(
        decimal Neto,
        decimal Iva,
        decimal Exento,
        decimal NoGravado,
        decimal Total,
        IReadOnlyList<DetalleAlicuota> Alicuotas);
}
