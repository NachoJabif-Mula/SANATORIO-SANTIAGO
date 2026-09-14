using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using BaresFamilia.Core.Models.Contratos.Fiscal;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.Fiscal;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.Extensions.Logging;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Implementación de <see cref="IConfiguracionFiscalService"/>.
/// </summary>
public class ConfiguracionFiscalService : IConfiguracionFiscalService
{
    private const long TamanioMaximoCertificadoBytes = 256 * 1024;

    /// <summary>
    /// OID del atributo serialNumber, donde ARCA espera el CUIT del contribuyente.
    /// </summary>
    private const string OidSerialNumber = "2.5.4.5";

    private readonly IRepository<Sucursal> _sucursalRepository;
    private readonly IRepository<ConfiguracionFiscalSucursal> _configuracionRepository;
    private readonly IProtectorFiscal _protector;
    private readonly IWsaaClient _wsaaClient;
    private readonly IWsfeClient _wsfeClient;
    private readonly ILogger<ConfiguracionFiscalService> _logger;

    public ConfiguracionFiscalService(
        IRepository<Sucursal> sucursalRepository,
        IRepository<ConfiguracionFiscalSucursal> configuracionRepository,
        IProtectorFiscal protector,
        IWsaaClient wsaaClient,
        IWsfeClient wsfeClient,
        ILogger<ConfiguracionFiscalService> logger)
    {
        _sucursalRepository = sucursalRepository;
        _configuracionRepository = configuracionRepository;
        _protector = protector;
        _wsaaClient = wsaaClient;
        _wsfeClient = wsfeClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EstadoFiscalSucursal>> ObtenerEstadosAsync(CancellationToken ct = default)
    {
        var sucursales = (await _sucursalRepository.GetAllAsync(ct)).Where(s => s.IsActive).ToList();
        var configuraciones = (await _configuracionRepository.GetAllAsync(ct)).ToDictionary(c => c.SucursalId);

        return sucursales
            .OrderBy(s => s.Nombre)
            .Select(s => ConstruirEstado(s, configuraciones.GetValueOrDefault(s.Id)))
            .ToList();
    }

    public async Task<EstadoFiscalSucursal?> ObtenerEstadoAsync(Guid sucursalId, CancellationToken ct = default)
    {
        var sucursal = await _sucursalRepository.GetByIdAsync(sucursalId, ct);
        if (sucursal is null)
            return null;

        return ConstruirEstado(sucursal, await ObtenerConfiguracionAsync(sucursalId, ct));
    }

    public async Task<SolicitudCertificado> GenerarSolicitudCertificadoAsync(Guid sucursalId, CancellationToken ct = default)
    {
        var sucursal = await ObtenerSucursalAsync(sucursalId, ct);

        // El subject lo exige ARCA con estos datos exactos; sin ellos la solicitud se rechaza.
        if (string.IsNullOrWhiteSpace(sucursal.Cuit) || string.IsNullOrWhiteSpace(sucursal.RazonSocial))
            throw new InvalidOperationException(
                "Antes de generar la solicitud, completá el CUIT y la razón social en la ficha de la sucursal.");

        var cuit = SoloDigitos(sucursal.Cuit);
        if (cuit.Length != 11)
            throw new InvalidOperationException("El CUIT de la sucursal debe tener 11 dígitos.");

        // El nombre se arma por partes en lugar de parsear una cadena, para que una razón
        // social con comas o signos no rompa el parseo.
        //
        // Los atributos se agregan al revés a propósito: el builder codifica en orden inverso
        // al de inserción, y ARCA espera la secuencia C, O, CN, serialNumber.
        var constructor = new X500DistinguishedNameBuilder();
        constructor.Add(OidSerialNumber, $"CUIT {cuit}", UniversalTagNumber.PrintableString);
        constructor.AddCommonName(sucursal.Nombre);
        constructor.AddOrganizationName(sucursal.RazonSocial);
        constructor.AddCountryOrRegion("AR");

        var nombreDistinguido = constructor.Build();
        var subject = nombreDistinguido.Name;

        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest(
            nombreDistinguido, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var csr = solicitud.CreateSigningRequestPem();

        var configuracion = await ObtenerConfiguracionAsync(sucursalId, ct);
        var esNueva = configuracion is null;
        configuracion ??= new ConfiguracionFiscalSucursal { SucursalId = sucursalId };

        configuracion.ClavePrivadaCifrada = _protector.Proteger(rsa.ExportPkcs8PrivateKey());
        configuracion.CsrGeneradoEn = DateTime.UtcNow;
        configuracion.CsrSubject = subject;
        configuracion.UpdatedAt = DateTime.UtcNow;

        await GuardarAsync(configuracion, esNueva, ct);

        _logger.LogInformation(
            "Solicitud de certificado generada para la sucursal {SucursalId} ({Subject}).", sucursalId, subject);

        var nombreArchivo = $"solicitud-{cuit}-{DateTime.UtcNow:yyyyMMdd}.csr";
        return new SolicitudCertificado(csr, nombreArchivo, subject);
    }

    public async Task<EstadoFiscalSucursal> CargarCertificadoEmitidoAsync(
        Guid sucursalId, byte[] certificado, string nombreArchivo, CancellationToken ct = default)
    {
        var sucursal = await ObtenerSucursalAsync(sucursalId, ct);

        var configuracion = await ObtenerConfiguracionAsync(sucursalId, ct);
        if (configuracion?.ClavePrivadaCifrada is null)
            throw new InvalidOperationException(
                "Primero generá la solicitud de certificado: sin la clave privada de esa solicitud el certificado no sirve.");

        if (certificado.Length == 0)
            throw new InvalidOperationException("Se requiere el archivo del certificado que emitió ARCA.");

        if (certificado.Length > TamanioMaximoCertificadoBytes)
            throw new InvalidOperationException("El archivo supera el tamaño máximo admitido para un certificado.");

        using var x509 = LeerCertificado(certificado);

        var vence = x509.NotAfter.ToUniversalTime();
        if (vence <= DateTime.UtcNow)
            throw new InvalidOperationException(
                $"El certificado venció el {vence:dd/MM/yyyy}. Generá uno nuevo en el portal de ARCA.");

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(_protector.Desproteger(configuracion.ClavePrivadaCifrada), out _);

        VerificarQueCorresponda(x509, rsa);

        // Se arma el PKCS#12 internamente: de acá en adelante todo el sistema trabaja con
        // ese formato, sin que nadie haya tenido que generarlo a mano.
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var conClave = x509.CopyWithPrivateKey(rsa);
        var pkcs12 = conClave.Export(X509ContentType.Pkcs12, password)
            ?? throw new InvalidOperationException("No se pudo combinar el certificado con la clave privada.");

        configuracion.CertificadoCifrado = _protector.Proteger(pkcs12);
        configuracion.CertificadoPasswordCifrada = _protector.ProtegerTexto(password);
        configuracion.CertificadoNombreArchivo = Path.GetFileName(nombreArchivo);
        configuracion.CertificadoSubject = x509.Subject;
        configuracion.CertificadoThumbprint = x509.Thumbprint;
        configuracion.CertificadoVence = vence;
        configuracion.CertificadoCargadoEn = DateTime.UtcNow;
        configuracion.UltimaValidacion = DateTime.UtcNow;
        configuracion.UltimaValidacionOk = true;
        configuracion.UltimaValidacionMensaje = $"Certificado válido hasta el {vence:dd/MM/yyyy}.";
        configuracion.UpdatedAt = DateTime.UtcNow;

        await _configuracionRepository.UpdateAsync(configuracion, ct);

        _logger.LogInformation(
            "Certificado emitido por ARCA cargado para la sucursal {SucursalId} (vence {Vence:yyyy-MM-dd}).",
            sucursalId, vence);

        return ConstruirEstado(sucursal, configuracion);
    }

    /// <summary>
    /// ARCA entrega el certificado en PEM, pero algunos navegadores lo guardan en binario.
    /// </summary>
    private static X509Certificate2 LeerCertificado(byte[] contenido)
    {
        try
        {
            var texto = Encoding.UTF8.GetString(contenido);
            if (texto.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal))
                return X509Certificate2.CreateFromPem(texto);

            return X509CertificateLoader.LoadCertificate(contenido);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException(
                "No se pudo leer el certificado. Subí el archivo tal como lo descargaste de ARCA (.crt o .pem).", ex);
        }
    }

    /// <summary>
    /// El certificado tiene que ser el emitido para la solicitud de esta sucursal: si no,
    /// la firma del pedido a WSAA no validaría y el error aparecería recién contra ARCA.
    /// </summary>
    private static void VerificarQueCorresponda(X509Certificate2 certificado, RSA clavePrivada)
    {
        using var publicaDelCertificado = certificado.GetRSAPublicKey();

        if (publicaDelCertificado is null ||
            !publicaDelCertificado.ExportSubjectPublicKeyInfo().SequenceEqual(clavePrivada.ExportSubjectPublicKeyInfo()))
        {
            throw new InvalidOperationException(
                "El certificado no corresponde a la última solicitud generada para esta sucursal. " +
                "Verificá que sea el que descargaste de ARCA para ese CSR.");
        }
    }

    private static string SoloDigitos(string? valor) =>
        valor is null ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());

    public async Task<EstadoFiscalSucursal> CargarCertificadoAsync(
        Guid sucursalId, byte[] certificado, string nombreArchivo, string? password, CancellationToken ct = default)
    {
        var sucursal = await ObtenerSucursalAsync(sucursalId, ct);

        if (certificado.Length == 0)
            throw new InvalidOperationException("Se requiere el archivo del certificado (.pfx o .p12).");

        if (certificado.Length > TamanioMaximoCertificadoBytes)
            throw new InvalidOperationException("El archivo supera el tamaño máximo admitido para un certificado.");

        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        if (extension is not (".pfx" or ".p12"))
            throw new InvalidOperationException("Formato inválido. Se espera un archivo PKCS#12 (.pfx o .p12).");

        X509Certificate2 x509;
        try
        {
            x509 = X509CertificateLoader.LoadPkcs12(certificado, password ?? string.Empty);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Certificado rechazado para la sucursal {SucursalId}.", sucursalId);
            throw new InvalidOperationException(
                "No se pudo abrir el certificado. Verificá la contraseña y que el archivo sea un PKCS#12 válido.", ex);
        }

        using (x509)
        {
            if (!x509.HasPrivateKey)
                throw new InvalidOperationException(
                    "El certificado no incluye la clave privada, necesaria para firmar el pedido de acceso a WSAA.");

            var vence = x509.NotAfter.ToUniversalTime();
            if (vence <= DateTime.UtcNow)
                throw new InvalidOperationException(
                    $"El certificado venció el {vence:dd/MM/yyyy}. Generá uno nuevo en el portal de ARCA.");

            var configuracion = await ObtenerConfiguracionAsync(sucursalId, ct);
            var esNueva = configuracion is null;
            configuracion ??= new ConfiguracionFiscalSucursal { SucursalId = sucursalId };

            configuracion.CertificadoCifrado = _protector.Proteger(certificado);
            configuracion.CertificadoPasswordCifrada = _protector.ProtegerTexto(password ?? string.Empty);
            configuracion.CertificadoNombreArchivo = Path.GetFileName(nombreArchivo);
            configuracion.CertificadoSubject = x509.Subject;
            configuracion.CertificadoThumbprint = x509.Thumbprint;
            configuracion.CertificadoVence = vence;
            configuracion.CertificadoCargadoEn = DateTime.UtcNow;
            configuracion.UltimaValidacion = DateTime.UtcNow;
            configuracion.UltimaValidacionOk = true;
            configuracion.UltimaValidacionMensaje = $"Certificado válido hasta el {vence:dd/MM/yyyy}.";
            configuracion.UpdatedAt = DateTime.UtcNow;

            await GuardarAsync(configuracion, esNueva, ct);

            _logger.LogInformation(
                "Certificado fiscal actualizado para la sucursal {SucursalId} (vence {Vence:yyyy-MM-dd}).",
                sucursalId, vence);

            return ConstruirEstado(sucursal, configuracion);
        }
    }

    public async Task<EstadoFiscalSucursal> CambiarAmbienteAsync(
        Guid sucursalId, AmbienteFiscal ambiente, CancellationToken ct = default)
    {
        var sucursal = await ObtenerSucursalAsync(sucursalId, ct);

        var configuracion = await ObtenerConfiguracionAsync(sucursalId, ct);
        var esNueva = configuracion is null;
        configuracion ??= new ConfiguracionFiscalSucursal { SucursalId = sucursalId };

        configuracion.Ambiente = ambiente;
        configuracion.UpdatedAt = DateTime.UtcNow;

        await GuardarAsync(configuracion, esNueva, ct);

        _logger.LogInformation(
            "Ambiente fiscal de la sucursal {SucursalId} cambiado a {Ambiente}.", sucursalId, ambiente);

        return ConstruirEstado(sucursal, configuracion);
    }

    public async Task<EstadoFiscalSucursal> VerificarAsync(Guid sucursalId, CancellationToken ct = default)
    {
        var sucursal = await ObtenerSucursalAsync(sucursalId, ct);

        var configuracion = await ObtenerConfiguracionAsync(sucursalId, ct);
        if (configuracion?.CertificadoCifrado is null)
            throw new InvalidOperationException("La sucursal todavía no tiene certificado cargado.");

        var (ok, mensaje) = VerificarCertificado(configuracion);

        var faltantes = DatosFiscalesFaltantes(sucursal);
        if (ok && faltantes.Length > 0)
        {
            ok = false;
            mensaje = $"{mensaje} Faltan datos del emisor: {string.Join(", ", faltantes)}.";
        }

        if (ok)
            (ok, mensaje) = await ProbarConexionArcaAsync(sucursalId, configuracion.Ambiente, ct);

        configuracion.UltimaValidacion = DateTime.UtcNow;
        configuracion.UltimaValidacionOk = ok;
        configuracion.UltimaValidacionMensaje = mensaje;
        configuracion.UpdatedAt = DateTime.UtcNow;
        await _configuracionRepository.UpdateAsync(configuracion, ct);

        return ConstruirEstado(sucursal, configuracion);
    }

    /// <summary>
    /// Primero consulta el estado de los servidores de ARCA (no requiere certificado), para
    /// que una caída del servicio no se confunda con un problema del certificado, y recién
    /// después intenta autenticarse.
    /// </summary>
    private async Task<(bool Ok, string Mensaje)> ProbarConexionArcaAsync(
        Guid sucursalId, AmbienteFiscal ambiente, CancellationToken ct)
    {
        try
        {
            var estado = await _wsfeClient.ConsultarEstadoServiciosAsync(ambiente, ct);
            if (!estado.TodoOperativo)
            {
                return (false,
                    $"ARCA informa servicios caídos en {ambiente} (App: {estado.AppServer}, Base: {estado.DbServer}, Auth: {estado.AuthServer}). " +
                    "Reintentá más tarde.");
            }

            var ticket = await _wsaaClient.ObtenerTicketAccesoAsync(sucursalId, "wsfe", ct);
            return (true,
                $"Conexión con ARCA ({ambiente}) correcta. Ticket de Acceso vigente hasta el {ticket.ExpiraEn.ToLocalTime():dd/MM/yyyy HH:mm}.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Falló la prueba de conexión con ARCA para la sucursal {SucursalId}.", sucursalId);
            return (false, ex.Message);
        }
    }

    private (bool Ok, string Mensaje) VerificarCertificado(ConfiguracionFiscalSucursal configuracion)
    {
        try
        {
            var contenido = _protector.Desproteger(configuracion.CertificadoCifrado!);
            var password = configuracion.CertificadoPasswordCifrada is null
                ? string.Empty
                : _protector.DesprotegerTexto(configuracion.CertificadoPasswordCifrada);

            using var x509 = X509CertificateLoader.LoadPkcs12(contenido, password);

            if (!x509.HasPrivateKey)
                return (false, "El certificado almacenado no tiene clave privada.");

            var vence = x509.NotAfter.ToUniversalTime();
            if (vence <= DateTime.UtcNow)
                return (false, $"El certificado venció el {vence:dd/MM/yyyy}.");

            return (true, $"Certificado válido hasta el {vence:dd/MM/yyyy}.");
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "No se pudo abrir el certificado almacenado de la sucursal {SucursalId}.", configuracion.SucursalId);
            return (false, "No se pudo abrir el certificado almacenado. Volvé a cargarlo.");
        }
    }

    private async Task GuardarAsync(ConfiguracionFiscalSucursal configuracion, bool esNueva, CancellationToken ct)
    {
        if (esNueva)
            await _configuracionRepository.AddAsync(configuracion, ct);
        else
            await _configuracionRepository.UpdateAsync(configuracion, ct);
    }

    private async Task<Sucursal> ObtenerSucursalAsync(Guid sucursalId, CancellationToken ct) =>
        await _sucursalRepository.GetByIdAsync(sucursalId, ct)
        ?? throw new KeyNotFoundException("Sucursal no encontrada.");

    private async Task<ConfiguracionFiscalSucursal?> ObtenerConfiguracionAsync(Guid sucursalId, CancellationToken ct)
    {
        var configuraciones = await _configuracionRepository.FindAsync(c => c.SucursalId == sucursalId, ct);
        return configuraciones.FirstOrDefault();
    }

    private static string[] DatosFiscalesFaltantes(Sucursal sucursal)
    {
        var faltantes = new List<string>();

        if (string.IsNullOrWhiteSpace(sucursal.Cuit)) faltantes.Add("CUIT");
        if (string.IsNullOrWhiteSpace(sucursal.RazonSocial)) faltantes.Add("razón social");
        if (sucursal.CondicionIva is null) faltantes.Add("condición IVA");
        if (sucursal.PuntoDeVenta <= 0) faltantes.Add("punto de venta");

        return [.. faltantes];
    }

    private static EstadoFiscalSucursal ConstruirEstado(Sucursal sucursal, ConfiguracionFiscalSucursal? configuracion)
    {
        var faltantes = DatosFiscalesFaltantes(sucursal);

        return new EstadoFiscalSucursal(
            SucursalId: sucursal.Id,
            Nombre: sucursal.Nombre,
            Cuit: sucursal.Cuit,
            RazonSocial: sucursal.RazonSocial,
            PuntoDeVenta: sucursal.PuntoDeVenta,
            CondicionIva: sucursal.CondicionIva,
            Ambiente: (configuracion?.Ambiente ?? AmbienteFiscal.Homologacion).ToString(),
            CertificadoCargado: configuracion?.CertificadoCifrado is not null,
            CertificadoNombreArchivo: configuracion?.CertificadoNombreArchivo,
            CertificadoSubject: configuracion?.CertificadoSubject,
            CertificadoVence: configuracion?.CertificadoVence,
            CertificadoVencido: configuracion?.CertificadoVence is { } vence && vence <= DateTime.UtcNow,
            CertificadoCargadoEn: configuracion?.CertificadoCargadoEn,
            UltimaValidacion: configuracion?.UltimaValidacion,
            UltimaValidacionOk: configuracion?.UltimaValidacionOk ?? false,
            UltimaValidacionMensaje: configuracion?.UltimaValidacionMensaje,
            DatosFiscalesCompletos: faltantes.Length == 0,
            Faltantes: faltantes,
            SolicitudGenerada: configuracion?.ClavePrivadaCifrada is not null,
            CsrGeneradoEn: configuracion?.CsrGeneradoEn);
    }
}
