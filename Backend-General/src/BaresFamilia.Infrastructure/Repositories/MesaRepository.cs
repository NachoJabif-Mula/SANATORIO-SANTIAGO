using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Mesa.
/// </summary>
public class MesaRepository : GenericRepository<Mesa>, IMesaRepository
{
    public MesaRepository(DbContext context) : base(context) { }

    public async Task<IEnumerable<Mesa>> GetActivasOrdenadasAsync(CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.Etiqueta)
            .ToListAsync(ct);
}
