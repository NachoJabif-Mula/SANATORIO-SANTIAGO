using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Fiscal;

/// <summary>
/// Credenciales y ambiente de emisión electrónica de una sucursal. Cada sucursal factura
/// con su propio CUIT y su propio certificado ante ARCA, por lo que esta configuración
/// es 1:1 con <see cref="Sucursal"/> y nunca se comparte entre sucursales.
///
/// Los datos del emisor (CUIT, razón social, punto de venta, condición IVA) viven en
/// <see cref="Sucursal"/>; acá vive únicamente lo relativo al acceso a los Web Services.
/// </summary>
public class ConfiguracionFiscalSucursal : BaseEntity
{
    public Guid SucursalId { get; set; }

    public AmbienteFiscal Ambiente { get; set; } = AmbienteFiscal.Homologacion;

    /// <summary>
    /// Certificado PKCS#12 (.pfx) cifrado con Data Protection. Nunca se persiste en claro.
    /// </summary>
    public byte[]? CertificadoCifrado { get; set; }

    /// <summary>
    /// Clave privada (PKCS#8) generada al crear la solicitud de certificado, cifrada.
    ///
    /// Se conserva por separado porque entre que se genera el CSR y ARCA devuelve el
    /// certificado pueden pasar días, y porque al renovar el certificado se reutiliza
    /// la misma clave. Nunca sale del sistema.
    /// </summary>
    public byte[]? ClavePrivadaCifrada { get; set; }

    public DateTime? CsrGeneradoEn { get; set; }

    /// <summary>
    /// Subject con el que se emitió la solicitud, para poder mostrar contra qué CUIT y
    /// razón social se pidió el certificado.
    /// </summary>
    public string? CsrSubject { get; set; }

    /// <summary>
    /// Contraseña del .pfx cifrada con Data Protection.
    /// </summary>
    public string? CertificadoPasswordCifrada { get; set; }

    public string? CertificadoNombreArchivo { get; set; }

    /// <summary>
    /// Subject del certificado emitido por ARCA, para mostrar en pantalla a qué CUIT pertenece.
    /// </summary>
    public string? CertificadoSubject { get; set; }

    public string? CertificadoThumbprint { get; set; }

    /// <summary>
    /// Fecha de expiración del certificado. Permite avisar antes de que caduque y corte la facturación.
    /// </summary>
    public DateTime? CertificadoVence { get; set; }

    public DateTime? CertificadoCargadoEn { get; set; }

    // ── Resultado de la última verificación manual desde el Backoffice ──
    public DateTime? UltimaValidacion { get; set; }
    public bool UltimaValidacionOk { get; set; }
    public string? UltimaValidacionMensaje { get; set; }

    // Navegación
    public Sucursal? Sucursal { get; set; }
}
