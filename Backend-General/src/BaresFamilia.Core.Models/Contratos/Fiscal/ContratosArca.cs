using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Contratos.Fiscal;

/// <summary>
/// Datos de un comprobante a autorizar. Los importes ya vienen calculados y cuadrados:
/// ARCA rechaza el pedido si el total no coincide exactamente con la suma de sus partes.
/// </summary>
public record SolicitudCae(
    int PuntoVenta,
    TipoComprobanteAfip TipoComprobante,
    long NumeroComprobante,
    DateTime FechaComprobante,
    int TipoDocumentoReceptor,
    long NumeroDocumentoReceptor,
    int CondicionIvaReceptor,
    decimal ImporteTotal,
    decimal ImporteNeto,
    decimal ImporteIva,
    decimal ImporteExento,
    decimal ImporteNoGravado,
    IReadOnlyList<DetalleAlicuota> Alicuotas);

public record DetalleAlicuota(AlicuotaIva Alicuota, decimal BaseImponible, decimal Importe);

/// <summary>
/// Respuesta de ARCA a una solicitud de CAE. Conserva el XML de ida y vuelta para poder
/// reconciliar después contra lo que ARCA efectivamente registró.
/// </summary>
public record RespuestaCae(
    bool Autorizado,
    string? Cae,
    DateTime? CaeVencimiento,
    string? Observaciones,
    string? Errores,
    string RequestXml,
    string ResponseXml);

public record EstadoServiciosArca(string AppServer, string DbServer, string AuthServer)
{
    public bool TodoOperativo =>
        AppServer.Equals("OK", StringComparison.OrdinalIgnoreCase)
        && DbServer.Equals("OK", StringComparison.OrdinalIgnoreCase)
        && AuthServer.Equals("OK", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Estado fiscal de una sucursal tal como lo muestra el Backoffice.
/// </summary>
public record EstadoFiscalSucursal(
    Guid SucursalId,
    string Nombre,
    string? Cuit,
    string? RazonSocial,
    int PuntoDeVenta,
    int? CondicionIva,
    string Ambiente,
    bool CertificadoCargado,
    string? CertificadoNombreArchivo,
    string? CertificadoSubject,
    DateTime? CertificadoVence,
    bool CertificadoVencido,
    DateTime? CertificadoCargadoEn,
    DateTime? UltimaValidacion,
    bool UltimaValidacionOk,
    string? UltimaValidacionMensaje,
    bool DatosFiscalesCompletos,
    string[] Faltantes,
    bool SolicitudGenerada,
    DateTime? CsrGeneradoEn);

/// <summary>
/// Solicitud de certificado lista para subir al portal de ARCA.
/// </summary>
public record SolicitudCertificado(string ContenidoPem, string NombreArchivo, string Subject);
