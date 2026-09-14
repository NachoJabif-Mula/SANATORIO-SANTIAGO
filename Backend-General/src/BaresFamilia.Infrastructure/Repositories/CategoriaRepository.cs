using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Categoria.
/// Recibe el DbContext por abstracción para poder registrarse tanto sobre
/// NubeContext como sobre LocalContext.
/// </summary>
public class CategoriaRepository : GenericRepository<Categoria>, ICategoriaRepository
{
    public CategoriaRepository(DbContext context) : base(context) { }

    public async Task<IEnumerable<Categoria>> GetPorSucursalAsync(Guid? sucursalId, bool incluirInactivas, CancellationToken ct = default)
    {
        IQueryable<Categoria> query = _dbSet.AsNoTracking();

        if (sucursalId.HasValue)
            query = query.Where(c => c.SucursalId == sucursalId.Value);

        if (!incluirInactivas)
            query = query.Where(c => c.IsActive);

        return await query.OrderBy(c => c.OrdenVisual).ToListAsync(ct);
    }
}
