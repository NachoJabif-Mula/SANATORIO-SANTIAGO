using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para DispositivoActivacion.
/// </summary>
public class DispositivoActivacionRepository : GenericRepository<DispositivoActivacion>, IDispositivoActivacionRepository
{
    public DispositivoActivacionRepository(DbContext context) : base(context) { }

    public async Task<DispositivoActivacion?> GetPorCodigoAsync(string codigoActivacion, CancellationToken ct = default)
        => await _dbSet
            .Include(d => d.Sucursal)
            .FirstOrDefaultAsync(d => d.CodigoActivacion == codigoActivacion && d.IsActive, ct);

    public async Task<IEnumerable<DispositivoActivacion>> GetVigentesConSucursalAsync(CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Include(d => d.Sucursal)
            .Where(d => d.IsActive)
            .ToListAsync(ct);

    public async Task<DispositivoActivacion?> GetPorIdIncluyendoInactivosAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IEnumerable<DispositivoActivacion>> GetActivadasAsync(CancellationToken ct = default)
        => await _dbSet.Where(d => d.Activado).ToListAsync(ct);

    public async Task GuardarCambiosAsync(IEnumerable<DispositivoActivacion> dispositivos, CancellationToken ct = default)
    {
        _dbSet.UpdateRange(dispositivos);
        await _context.SaveChangesAsync(ct);
    }
}
