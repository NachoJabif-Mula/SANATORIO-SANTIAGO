using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Controlador para gestionar la activación local del POS y verificar su estado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DispositivoController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly LocalContext _context;

    public DispositivoController(IHttpClientFactory httpClientFactory, IConfiguration _config, LocalContext context)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = _config;
        _context = context;
    }

    /// <summary>
    /// Verifica si el dispositivo local ya se encuentra activado.
    /// </summary>
    [HttpGet("estado")]
    public async Task<IActionResult> ObtenerEstado(CancellationToken ct)
    {
        try
        {
            var dbAct = await _context.DispositivosActivacion.FirstOrDefaultAsync(d => d.Activado, ct);
            if (dbAct != null && !string.IsNullOrWhiteSpace(dbAct.TokenHash))
            {
                var data = JsonSerializer.Deserialize<ActivationData>(dbAct.TokenHash, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data != null && !string.IsNullOrWhiteSpace(data.Token))
                {
                    return Ok(new
                    {
                        activado = true,
                        sucursalId = data.SucursalId,
                        sucursalNombre = data.SucursalNombre,
                        dispositivoId = data.DispositivoId,
                        nombreDispositivo = data.NombreDispositivo,
                        expiresAt = data.ExpiresAt
                    });
                }
            }
        }
        catch {}

        return Ok(new { activado = false });
    }

    /// <summary>
    /// Canjea un código de activación en la API de Nube y guarda el token M2M localmente en la base de datos.
    /// </summary>
    [HttpPost("activar")]
    public async Task<IActionResult> Activar([FromBody] ActivarLocalRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CodigoActivacion))
            return BadRequest(new { message = "El código de activación es obligatorio." });

        var baseUrl = _configuration["NubeApi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return StatusCode(500, new { message = "La dirección de la API Nube no está configurada en appsettings.json." });

        try
        {
            var client = _httpClientFactory.CreateClient();
            var payloadJson = JsonSerializer.Serialize(new { codigoActivacion = request.CodigoActivacion.Trim().ToUpper() });
            var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            var url = $"{baseUrl.TrimEnd('/')}/api/dispositivos/activar";
            var response = await client.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                try
                {
                    var errObj = JsonSerializer.Deserialize<JsonElement>(errorBody);
                    if (errObj.TryGetProperty("message", out var msgProp))
                    {
                        return BadRequest(new { message = msgProp.GetString() });
                    }
                }
                catch {}
                return BadRequest(new { message = $"Error de la Nube: {(int)response.StatusCode} - {response.ReasonPhrase}" });
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<ActivationData>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (data == null || string.IsNullOrWhiteSpace(data.Token))
            {
                return BadRequest(new { message = "La respuesta de activación de la Nube es inválida." });
            }

            // 1. Asegurar existencia de sucursal localmente para evitar violación de FK
            var sucursalExiste = await _context.Sucursales.AnyAsync(s => s.Id == data.SucursalId, ct);
            if (!sucursalExiste)
            {
                var nuevaSucursal = new Sucursal
                {
                    Id = data.SucursalId,
                    Nombre = data.SucursalNombre,
                    Direccion = "Local",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Sucursales.Add(nuevaSucursal);
                await _context.SaveChangesAsync(ct);
            }

            // 2. Guardar en base de datos local (borrado lógico de activaciones previas
            //    para conservar el historial de qué dispositivo/sucursal estuvo activo antes)
            var prevActivations = await _context.DispositivosActivacion
                .Where(d => d.Activado)
                .ToListAsync(ct);
            foreach (var prev in prevActivations)
            {
                prev.Activado = false;
                prev.IsActive = false;
                prev.UpdatedAt = DateTime.UtcNow;
            }

            var localAct = new DispositivoActivacion
            {
                Id = data.DispositivoId,
                SucursalId = data.SucursalId,
                CodigoActivacion = request.CodigoActivacion.Trim().ToUpper(),
                NombreDispositivo = data.NombreDispositivo,
                Activado = true,
                FechaActivacion = DateTime.UtcNow,
                ExpiraCodigo = DateTime.UtcNow.AddYears(1),
                TokenHash = body
            };
            _context.DispositivosActivacion.Add(localAct);
            await _context.SaveChangesAsync(ct);

            return Ok(new { message = "Dispositivo activado con éxito." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error de red al conectar con la Nube: {ex.Message}" });
        }
    }

    /// <summary>
    /// Desactiva el dispositivo local, borrando los registros de activación de la base de datos.
    /// </summary>
    [HttpPost("desactivar")]
    public async Task<IActionResult> Desactivar(CancellationToken ct)
    {
        try
        {
            var dbActs = await _context.DispositivosActivacion
                .Where(d => d.Activado)
                .ToListAsync(ct);

            foreach (var act in dbActs)
            {
                act.Activado = false;
                act.IsActive = false;
                act.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);

            return Ok(new { desactivado = true, message = "Dispositivo desactivado correctamente." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al desactivar: {ex.Message}" });
        }
    }
}

public class ActivarLocalRequest
{
    public string CodigoActivacion { get; set; } = string.Empty;
}

public class ActivationData
{
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Guid SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
    public Guid DispositivoId { get; set; }
    public string NombreDispositivo { get; set; } = string.Empty;
}
