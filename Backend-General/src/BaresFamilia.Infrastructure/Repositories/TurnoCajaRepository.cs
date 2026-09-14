using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para TurnoCaja.
/// </summary>
public class TurnoCajaRepository : GenericRepository<TurnoCaja>, ITurnoCajaRepository
{
    public TurnoCajaRepository(DbContext context) : base(context) { }

    public async Task<TurnoCaja?> GetAbiertoPorCajaAsync(Guid cajaId, CancellationToken ct = default)
        => await _dbSet
            .Where(t => t.CajaId == cajaId && t.FechaCierre == null && t.IsActive)
            .OrderByDescending(t => t.FechaApertura)
            .FirstOrDefaultAsync(ct);

    public async Task<TurnoCaja?> GetUltimoCerradoPorCajaAsync(Guid cajaId, CancellationToken ct = default)
        => await _dbSet
            .Where(t => t.CajaId == cajaId && t.FechaCierre != null && t.IsActive)
            .OrderByDescending(t => t.FechaApertura)
            .FirstOrDefaultAsync(ct);

    public async Task<TurnoCaja?> GetAbiertoConDetallesAsync(CancellationToken ct = default)
        => await _dbSet
            .Include(t => t.Caja)
            .Include(t => t.Usuario)
            .Where(t => t.FechaCierre == null && t.IsActive)
            .OrderByDescending(t => t.FechaApertura)
            .FirstOrDefaultAsync(ct);

    public async Task<TurnoCaja?> GetConDetallesAsync(Guid turnoId, CancellationToken ct = default)
        => await _dbSet
            .Include(t => t.Caja)
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.Id == turnoId && t.IsActive, ct);

    public async Task<TurnoCaja?> GetConDetallesIncluyendoInactivosAsync(Guid turnoId, CancellationToken ct = default)
        => await _dbSet
            .Include(t => t.Caja)
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.Id == turnoId, ct);

    public async Task<IEnumerable<TurnoCaja>> GetDeFechaContableAsync(Guid cajaId, DateTime fechaContable, CancellationToken ct = default)
        => await _dbSet
            .Include(t => t.Usuario)
            .Where(t => t.CajaId == cajaId && t.FechaContable.Date == fechaContable.Date && t.IsActive)
            .ToListAsync(ct);

    public async Task<bool> ExisteAbiertoEnCajaAsync(Guid cajaId, CancellationToken ct = default)
        => await _dbSet.AnyAsync(t => t.CajaId == cajaId && t.FechaCierre == null && t.IsActive, ct);

    public async Task<TurnoCaja?> GetAbiertoPorIdAsync(Guid turnoId, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(t => t.Id == turnoId && t.FechaCierre == null, ct);
}
