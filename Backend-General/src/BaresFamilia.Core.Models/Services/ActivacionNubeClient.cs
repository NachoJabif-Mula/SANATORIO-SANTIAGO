using System.Text.Json;
using System.Text;
using BaresFamilia.Core.Models.Contratos.Seguridad;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Cliente HTTP contra el endpoint de activación de la API Nube.
/// La BaseAddress la configura el host al registrar el cliente tipado.
/// </summary>
public class ActivacionNubeClient : IActivacionNubeClient
{
    private const string RutaActivacion = "api/dispositivos/activar";

    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;

    public ActivacionNubeClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RespuestaActivacion> CanjearCodigoAsync(string codigoActivacion, CancellationToken ct = default)
    {
        if (_httpClient.BaseAddress is null)
            throw new IntegracionNubeException("La dirección de la API Nube no está configurada en appsettings.json.");

        HttpResponseMessage respuesta;
        try
        {
            var payload = JsonSerializer.Serialize(new { codigoActivacion });
            using var contenido = new StringContent(payload, Encoding.UTF8, "application/json");

            respuesta = await _httpClient.PostAsync(RutaActivacion, contenido, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new IntegracionNubeException($"Error de red al conectar con la Nube: {ex.Message}", ex);
        }

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        if (!respuesta.IsSuccessStatusCode)
            throw new ReglaNegocioException(ExtraerMensajeDeError(cuerpo, respuesta));

        var datos = Deserializar(cuerpo);
        if (datos is null || string.IsNullOrWhiteSpace(datos.Token))
            throw new ReglaNegocioException("La respuesta de activación de la Nube es inválida.");

        return new RespuestaActivacion(datos, cuerpo);
    }

    /// <summary>
    /// La Nube devuelve sus errores de negocio como { message }. Si el cuerpo no
    /// tiene ese formato, se informa el código de estado.
    /// </summary>
    private static string ExtraerMensajeDeError(string cuerpo, HttpResponseMessage respuesta)
    {
        try
        {
            var error = JsonSerializer.Deserialize<JsonElement>(cuerpo);
            if (error.TryGetProperty("message", out var mensaje) && mensaje.GetString() is { } texto)
                return texto;
        }
        catch (JsonException)
        {
            // Cuerpo no-JSON: se cae al mensaje genérico de abajo.
        }

        return $"Error de la Nube: {(int)respuesta.StatusCode} - {respuesta.ReasonPhrase}";
    }

    private static DatosActivacion? Deserializar(string cuerpo)
    {
        try
        {
            return JsonSerializer.Deserialize<DatosActivacion>(cuerpo, OpcionesJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
