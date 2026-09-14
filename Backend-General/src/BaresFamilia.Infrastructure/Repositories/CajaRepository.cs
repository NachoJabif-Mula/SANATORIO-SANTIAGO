using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Caja.
/// </summary>
public class CajaRepository : GenericRepository<Caja>, ICajaRepository
{
    public CajaRepository(DbContext context) : base(context) { }

    public async Task<Caja?> GetActivaAsync(CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(c => c.IsActive, ct);
}
