using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de consulta de cierres diarios consolidados.
/// </summary>
public class CierreDiarioService : GenericService<CierreDiario>, ICierreDiarioService
{
    private readonly ICierreDiarioRepository _cierreDiarioRepository;

    public CierreDiarioService(ICierreDiarioRepository cierreDiarioRepository) : base(cierreDiarioRepository)
    {
        _cierreDiarioRepository = cierreDiarioRepository;
    }

    public async Task<IEnumerable<CierreDiario>> GetConDetallesAsync(Guid? sucursalId, DateTime? desde, DateTime? hasta, CancellationToken ct = default)
        => await _cierreDiarioRepository.GetConDetallesAsync(sucursalId, desde, hasta, ct);

    public async Task<CierreDiario?> GetPorIdConDetallesAsync(Guid id, CancellationToken ct = default)
        => await _cierreDiarioRepository.GetPorIdConDetallesAsync(id, ct);
}
