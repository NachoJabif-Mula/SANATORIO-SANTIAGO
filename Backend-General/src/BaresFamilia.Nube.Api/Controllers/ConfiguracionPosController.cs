using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Nube.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Endpoint para guardar y obtener la configuración visual del POS
/// por sucursal (coordenadas de mesas, colores de botones, zonas del salón, etc.).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfiguracionPosController : ControllerBase
{
    private readonly IService<ConfiguracionPos> _configService;
    private readonly IService<Mesa> _mesaService;

    public ConfiguracionPosController(IService<ConfiguracionPos> configService, IService<Mesa> mesaService)
    {
        _configService = configService;
        _mesaService = mesaService;
    }

    /// <summary>
    /// Obtiene todas las configuraciones POS activas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ConfiguracionPos>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var configs = await _configService.GetAllAsync(ct);
        return Ok(configs.Where(c => c.IsActive));
    }

    /// <summary>
    /// Obtiene las configuraciones POS de una sucursal.
    /// </summary>
    [HttpGet("sucursal/{sucursalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<ConfiguracionPos>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySucursal(Guid sucursalId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        SyncManagerStore.RecordPull(sucursalId, "config");
        var configs = includeInactive
            ? await _configService.FindAsync(c => c.SucursalId == sucursalId, ct)
            : await _configService.FindAsync(c => c.SucursalId == sucursalId && c.IsActive, ct);
        return Ok(configs);
    }

    /// <summary>
    /// Obtiene una configuración POS por su ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConfiguracionPos), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var config = await _configService.GetByIdAsync(id, ct);
        if (config is null || !config.IsActive)
            return NotFound(new { message = $"Configuración POS con ID '{id}' no encontrada o inactiva." });

        return Ok(config);
    }

    /// <summary>
    /// Guarda una nueva configuración visual del POS.
    /// El campo ConfiguracionJson acepta cualquier estructura JSON válida.
    /// Ejemplo de payload:
    /// {
    ///   "mesas": [{"id": "...", "posX": 120, "posY": 80, "forma": "Cuadrada", "color": "#FF6B35"}],
    ///   "botones": {"colorPrimario": "#1E88E5", "colorSecundario": "#43A047"},
    ///   "zonas": [{"nombre": "Patio", "color": "#E3F2FD"}]
    /// }
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConfiguracionPos), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaveConfiguracionPosRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre de la configuración es obligatorio." });

        if (string.IsNullOrWhiteSpace(request.ConfiguracionJson))
            return BadRequest(new { message = "El JSON de configuración es obligatorio." });

        if (!User.IsGlobal() && User.GetSucursalId() != request.SucursalId)
            return Forbid();

        var config = new ConfiguracionPos
        {
            SucursalId = request.SucursalId,
            Nombre = request.Nombre.Trim(),
            ConfiguracionJson = request.ConfiguracionJson
        };

        var created = await _configService.CreateAsync(config, ct);
        await SynchronizeMesasAsync(config.SucursalId, config.ConfiguracionJson, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Actualiza una configuración visual del POS existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveConfiguracionPosRequest request, CancellationToken ct)
    {
        var config = await _configService.GetByIdAsync(id, ct);
        if (config is null || !config.IsActive)
            return NotFound(new { message = $"Configuración POS con ID '{id}' no encontrada." });

        if (string.IsNullOrWhiteSpace(request.ConfiguracionJson))
            return BadRequest(new { message = "El JSON de configuración es obligatorio." });

        if (!User.IsGlobal() && (User.GetSucursalId() != config.SucursalId || User.GetSucursalId() != request.SucursalId))
            return Forbid();

        config.SucursalId = request.SucursalId;
        config.Nombre = request.Nombre?.Trim() ?? config.Nombre;
        config.ConfiguracionJson = request.ConfiguracionJson;

        await _configService.UpdateAsync(config, ct);
        await SynchronizeMesasAsync(config.SucursalId, config.ConfiguracionJson, ct);
        return NoContent();
    }

    /// <summary>
    /// Elimina una configuración POS (borrado lógico).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var config = await _configService.GetByIdAsync(id, ct);
        if (config is null || !config.IsActive)
            return NotFound(new { message = $"Configuración POS con ID '{id}' no encontrada." });

        config.IsActive = false;
        config.UpdatedAt = DateTime.UtcNow;
        await _configService.UpdateAsync(config, ct);

        // Volver a sincronizar las mesas restantes de la sucursal (para desactivar las que pertenecían solo a este plano)
        await SynchronizeMesasAsync(config.SucursalId, "{\"tables\":[]}", ct);

        return NoContent();
    }

    private static Guid ConvertToGuid(string id)
    {
        if (Guid.TryParse(id, out var guid))
            return guid;

        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id));
            return new Guid(hash);
        }
    }

    private async Task SynchronizeMesasAsync(Guid sucursalId, string json, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("tables", out var tablesElement) || tablesElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                return;

            var existingMesas = await _mesaService.FindAsync(m => m.SucursalId == sucursalId, ct);
            var existingMap = existingMesas.ToDictionary(m => m.Id);

            var tablesInLayout = new List<Guid>();

            foreach (var item in tablesElement.EnumerateArray())
            {
                if (item.TryGetProperty("isDecoration", out var isDecProp) && isDecProp.ValueKind == System.Text.Json.JsonValueKind.True)
                    continue;

                if (item.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "decoration")
                    continue;

                if (!item.TryGetProperty("id", out var idProp) || idProp.GetString() is not string stringId)
                    continue;

                var mesaId = ConvertToGuid(stringId);
                tablesInLayout.Add(mesaId);

                var label = item.TryGetProperty("label", out var labelProp) ? labelProp.GetString() ?? "Mesa" : "Mesa";
                var seats = item.TryGetProperty("seats", out var seatsProp) && seatsProp.TryGetInt32(out var sVal) ? sVal : 4;
                var x = item.TryGetProperty("x", out var xProp) && xProp.TryGetDouble(out var xVal) ? xVal : 0;
                var y = item.TryGetProperty("y", out var yProp) && yProp.TryGetDouble(out var yVal) ? yVal : 0;
                
                var typeStr = item.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "";
                var forma = typeStr.ToLower() switch
                {
                    "circle" => BaresFamilia.Core.Models.Enums.FormaMesa.Redonda,
                    "rectangle" => BaresFamilia.Core.Models.Enums.FormaMesa.Rectangular,
                    _ => BaresFamilia.Core.Models.Enums.FormaMesa.Cuadrada
                };

                if (existingMap.TryGetValue(mesaId, out var existingMesa))
                {
                    existingMesa.Etiqueta = label;
                    existingMesa.Capacidad = seats;
                    existingMesa.PosX = x;
                    existingMesa.PosY = y;
                    existingMesa.Forma = forma;
                    existingMesa.IsActive = true;
                    existingMesa.UpdatedAt = DateTime.UtcNow;

                    await _mesaService.UpdateAsync(existingMesa, ct);
                }
                else
                {
                    var newMesa = new Mesa
                    {
                        Id = mesaId,
                        SucursalId = sucursalId,
                        Etiqueta = label,
                        Capacidad = seats,
                        PosX = x,
                        PosY = y,
                        Forma = forma,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _mesaService.CreateAsync(newMesa, ct);
                }
            }

            var allConfigs = await _configService.FindAsync(c => c.SucursalId == sucursalId && c.IsActive, ct);
            var activeMesaIds = new HashSet<Guid>();

            foreach (var id in tablesInLayout)
            {
                activeMesaIds.Add(id);
            }

            foreach (var cfg in allConfigs)
            {
                try
                {
                    using var otherDoc = System.Text.Json.JsonDocument.Parse(cfg.ConfiguracionJson);
                    if (otherDoc.RootElement.TryGetProperty("tables", out var otherTables) && otherTables.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in otherTables.EnumerateArray())
                        {
                            if (item.TryGetProperty("isDecoration", out var isDecProp) && isDecProp.ValueKind == System.Text.Json.JsonValueKind.True)
                                continue;
                            if (item.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "decoration")
                                continue;
                            if (item.TryGetProperty("id", out var idProp) && idProp.GetString() is string stringId)
                            {
                                activeMesaIds.Add(ConvertToGuid(stringId));
                            }
                        }
                    }
                }
                catch {}
            }

            foreach (var mesa in existingMesas)
            {
                if (!activeMesaIds.Contains(mesa.Id) && mesa.IsActive)
                {
                    mesa.IsActive = false;
                    mesa.UpdatedAt = DateTime.UtcNow;
                    await _mesaService.UpdateAsync(mesa, ct);
                }
            }
        }
        catch {}
    }
}

// ═══════════════════════════════════
// DTOs de Request
// ═══════════════════════════════════

/// <summary>
/// DTO para crear/actualizar configuración visual del POS.
/// ConfiguracionJson es un string JSON con estructura libre.
/// </summary>
public record SaveConfiguracionPosRequest(
    Guid SucursalId,
    string? Nombre,
    string ConfiguracionJson
);
