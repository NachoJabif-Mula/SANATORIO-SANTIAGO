using BaresFamilia.Core.Models.Contratos.Fiscal;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Gestión de las credenciales fiscales de cada sucursal: certificado digital, contraseña
/// y ambiente de emisión ante ARCA.
///
/// Vive acá y no en un controlador porque se usa desde las dos APIs: el Backoffice carga el
/// certificado en la Nube, y cada sucursal necesita el suyo en su propia base para poder
/// facturar cuando cobra.
/// </summary>
public interface IConfiguracionFiscalService
{
    Task<IReadOnlyList<EstadoFiscalSucursal>> ObtenerEstadosAsync(CancellationToken ct = default);

    Task<EstadoFiscalSucursal?> ObtenerEstadoAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>
    /// Genera la clave privada de la sucursal y devuelve la solicitud de certificado (CSR)
    /// para subir al portal de ARCA. La clave privada queda guardada cifrada y nunca se expone.
    /// </summary>
    Task<SolicitudCertificado> GenerarSolicitudCertificadoAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>
    /// Carga el certificado (.crt) que devuelve ARCA y lo combina con la clave privada
    /// generada al crear la solicitud. Es el camino normal: evita que alguien tenga que
    /// armar un PKCS#12 a mano.
    /// </summary>
    Task<EstadoFiscalSucursal> CargarCertificadoEmitidoAsync(
        Guid sucursalId, byte[] certificado, string nombreArchivo, CancellationToken ct = default);

    /// <summary>
    /// Valida un PKCS#12 ya armado (que abra con la contraseña, tenga clave privada y esté
    /// vigente) y lo guarda cifrado. Alternativa para certificados emitidos por fuera del
    /// sistema, por ejemplo los que provee un contador.
    /// </summary>
    Task<EstadoFiscalSucursal> CargarCertificadoAsync(
        Guid sucursalId, byte[] certificado, string nombreArchivo, string? password, CancellationToken ct = default);

    Task<EstadoFiscalSucursal> CambiarAmbienteAsync(
        Guid sucursalId, AmbienteFiscal ambiente, CancellationToken ct = default);

    /// <summary>
    /// Comprueba que la sucursal esté en condiciones de facturar: certificado, datos del
    /// emisor y conexión efectiva con ARCA.
    /// </summary>
    Task<EstadoFiscalSucursal> VerificarAsync(Guid sucursalId, CancellationToken ct = default);
}
