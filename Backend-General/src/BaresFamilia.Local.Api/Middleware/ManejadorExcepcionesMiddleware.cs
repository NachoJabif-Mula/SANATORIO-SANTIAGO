using BaresFamilia.Core.Models.Exceptions;

namespace BaresFamilia.Local.Api.Middleware;

/// <summary>
/// Traduce las excepciones de dominio lanzadas por la capa de servicios al
/// código HTTP que corresponde, de modo que los controladores no necesiten
/// repetir validaciones ni envolver cada llamada en un try/catch.
///
/// ReglaNegocioException        -> 400 Bad Request
/// RecursoNoEncontradoException -> 404 Not Found
/// AccesoDenegadoException      -> 403 Forbidden
/// IntegracionNubeException     -> 500 Internal Server Error
/// </summary>
public class ManejadorExcepcionesMiddleware
{
    private readonly RequestDelegate _siguiente;
    private readonly ILogger<ManejadorExcepcionesMiddleware> _logger;

    public ManejadorExcepcionesMiddleware(RequestDelegate siguiente, ILogger<ManejadorExcepcionesMiddleware> logger)
    {
        _siguiente = siguiente;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _siguiente(context);
        }
        catch (ReglaNegocioException ex)
        {
            await EscribirRespuestaAsync(context, StatusCodes.Status400BadRequest, ex.Message, ConstruirCuerpo(ex));
        }
        catch (RecursoNoEncontradoException ex)
        {
            await EscribirRespuestaAsync(context, StatusCodes.Status404NotFound, ex.Message, new { message = ex.Message });
        }
        catch (AccesoDenegadoException ex)
        {
            await EscribirRespuestaAsync(context, StatusCodes.Status403Forbidden, ex.Message, new { message = ex.Message });
        }
        catch (IntegracionNubeException ex)
        {
            _logger.LogError(ex, "Fallo de integración con la API Nube.");
            await EscribirRespuestaAsync(context, StatusCodes.Status500InternalServerError, ex.Message, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Un error sin código se serializa como { message }. Si trae código, se agregan
    /// "error" y los datos adicionales, que es el contrato que espera el POS.
    /// </summary>
    private static object ConstruirCuerpo(ReglaNegocioException ex)
    {
        if (ex.Codigo is null)
            return new { message = ex.Message };

        var cuerpo = new Dictionary<string, object?>
        {
            ["error"] = ex.Codigo,
            ["message"] = ex.Message
        };

        foreach (var (clave, valor) in ex.Detalles)
            cuerpo[clave] = valor;

        return cuerpo;
    }

    private async Task EscribirRespuestaAsync(HttpContext context, int codigoEstado, string mensaje, object cuerpo)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("No se pudo escribir la respuesta de error '{Mensaje}': la respuesta ya había comenzado.", mensaje);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = codigoEstado;
        await context.Response.WriteAsJsonAsync(cuerpo);
    }
}

/// <summary>
/// Registro del middleware en el pipeline HTTP.
/// </summary>
public static class ManejadorExcepcionesMiddlewareExtensions
{
    public static IApplicationBuilder UseManejadorExcepciones(this IApplicationBuilder app)
        => app.UseMiddleware<ManejadorExcepcionesMiddleware>();
}
