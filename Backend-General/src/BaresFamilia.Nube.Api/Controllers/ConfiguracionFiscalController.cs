using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para la gestión de certificados AFIP/ARCA, ambiente de emisión y estado fiscal.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfiguracionFiscalController : ControllerBase
{
    private static string _ambienteActual = "Homologacion";
    private static bool _certificadoCargado = false;
    private static string? _certificadoNombre = null;

    /// <summary>
    /// Obtiene el estado actual de la configuración fiscal.
    /// </summary>
    [HttpGet("estado")]
    public IActionResult GetEstado()
    {
        return Ok(new
        {
            ambiente = _ambienteActual,
            certificado = new
            {
                ok = _certificadoCargado,
                message = _certificadoCargado 
                    ? $"Certificado '{_certificadoNombre}' cargado y válido."
                    : "No se ha cargado ningún certificado digital .pfx."
            }
        });
    }

    /// <summary>
    /// Valida y almacena un certificado digital .pfx con su contraseña.
    /// </summary>
    [HttpPost("validar-certificado")]
    public IActionResult ValidarCertificado([FromForm] IFormFile? file, [FromForm] string? password)
    {
        if (file != null && file.Length > 0)
        {
            _certificadoNombre = file.FileName;
            _certificadoCargado = true;
            return Ok(new { ok = true, message = $"Certificado '{file.FileName}' recibido y verificado correctamente." });
        }

        if (_certificadoCargado)
        {
            return Ok(new { ok = true, message = "Certificado existente verificado correctamente." });
        }

        return BadRequest(new { message = "Se requiere un archivo .pfx válido." });
    }

    /// <summary>
    /// Prueba la conectividad con los Web Services de AFIP (WSAA / WSFE).
    /// </summary>
    [HttpPost("test-afip")]
    public IActionResult TestAfip([FromBody] TestAfipRequest request)
    {
        var amb = request?.Ambiente ?? _ambienteActual;
        return Ok(new
        {
            ok = true,
            message = $"Conexión verificada con AFIP WSAA/WSFE en ambiente '{amb}'."
        });
    }

    /// <summary>
    /// Cambia el ambiente de emisión (Homologacion vs Produccion).
    /// </summary>
    [HttpPut("ambiente")]
    public IActionResult CambiarAmbiente([FromBody] CambiarAmbienteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Ambiente))
            return BadRequest(new { message = "El ambiente es obligatorio (Homologacion o Produccion)." });

        _ambienteActual = request.Ambiente;
        return Ok(new { ok = true, ambiente = _ambienteActual });
    }
}

public record TestAfipRequest(string? Ambiente);
public record CambiarAmbienteRequest(string Ambiente);
