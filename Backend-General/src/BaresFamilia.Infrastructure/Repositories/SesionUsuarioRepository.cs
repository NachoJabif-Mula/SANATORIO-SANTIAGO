using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio de sesiones persistidas del Backoffice.
/// </summary>
public class SesionUsuarioRepository : GenericRepository<SesionUsuario>, ISesionUsuarioRepository
{
    public SesionUsuarioRepository(DbContext context) : base(context) { }

    public async Task<SesionUsuario?> GetPorTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(s => s.TokenHash == tokenHash, ct);
}
