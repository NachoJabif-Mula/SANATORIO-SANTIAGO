using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Usuario.
/// </summary>
public class UsuarioRepository : GenericRepository<Usuario>, IUsuarioRepository
{
    public UsuarioRepository(DbContext context) : base(context) { }

    public async Task<Usuario?> GetActivoPorPinAsync(string pin, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.PinAcceso == pin && u.IsActive, ct);

    public async Task<Usuario?> GetActivoConRolAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);

    public async Task<int> ContarActivosAsync(CancellationToken ct = default)
        => await _dbSet.CountAsync(u => u.IsActive, ct);

    public async Task<IEnumerable<Usuario>> GetActivosConRolYSucursalAsync(Guid? sucursalId, CancellationToken ct = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(u => u.Rol)
            .Include(u => u.Sucursal)
            .Where(u => u.IsActive);

        if (sucursalId.HasValue)
            query = query.Where(u => u.SucursalId == sucursalId.Value);

        return await query.ToListAsync(ct);
    }

    public async Task<Usuario?> GetActivoConRolYSucursalAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .Include(u => u.Rol)
            .Include(u => u.Sucursal)
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);

    public async Task<Usuario?> GetActivoPorEmailAsync(string email, CancellationToken ct = default)
        => await _dbSet
            .Include(u => u.Rol)
            .Include(u => u.Sucursal)
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

    public async Task<IEnumerable<Usuario>> GetActivosDeSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(u => u.SucursalId == sucursalId && u.IsActive)
            .ToListAsync(ct);

    public async Task<bool> ExistePinEnSucursalAsync(Guid sucursalId, string pin, Guid? idExcluido, CancellationToken ct = default)
        => await _dbSet.AnyAsync(
            u => u.SucursalId == sucursalId
                && u.PinAcceso == pin
                && u.IsActive
                && (idExcluido == null || u.Id != idExcluido.Value),
            ct);
}
