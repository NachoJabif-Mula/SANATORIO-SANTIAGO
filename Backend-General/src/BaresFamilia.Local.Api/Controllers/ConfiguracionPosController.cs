using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Controlador local para la consulta de planos de salón (configuraciones visuales).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ConfiguracionPosController : ControllerBase
{
    private readonly IService<ConfiguracionPos> _configService;

    public ConfiguracionPosController(IService<ConfiguracionPos> configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Obtiene todos los planos de salón activos cargados localmente en la sucursal.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ConfiguracionPos>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var configs = await _configService.GetAllAsync(ct);
        return Ok(configs.Where(c => c.IsActive));
    }
}
