using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para MovimientoCaja.
/// </summary>
public class MovimientoCajaRepository : GenericRepository<MovimientoCaja>, IMovimientoCajaRepository
{
    public MovimientoCajaRepository(DbContext context) : base(context) { }

    public async Task<IEnumerable<MovimientoCaja>> GetPorTurnoAsync(Guid turnoCajaId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(m => m.TurnoCajaId == turnoCajaId && m.IsActive)
            .ToListAsync(ct);

    public async Task<IEnumerable<MovimientoCaja>> GetPorTurnosAsync(IReadOnlyCollection<Guid> turnoCajaIds, CancellationToken ct = default)
    {
        if (turnoCajaIds.Count == 0)
            return [];

        return await _dbSet
            .AsNoTracking()
            .Where(m => turnoCajaIds.Contains(m.TurnoCajaId) && m.IsActive)
            .ToListAsync(ct);
    }
}
