using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para CierreDiario.
/// </summary>
public class CierreDiarioRepository : GenericRepository<CierreDiario>, ICierreDiarioRepository
{
    public CierreDiarioRepository(DbContext context) : base(context) { }

    public async Task<bool> ExisteParaCajaYFechaAsync(Guid cajaId, DateTime fecha, CancellationToken ct = default)
        => await _dbSet.AnyAsync(c => c.CajaId == cajaId && c.Fecha.Date == fecha.Date, ct);

    public async Task<IEnumerable<CierreDiario>> GetConDetallesAsync(Guid? sucursalId, DateTime? desde, DateTime? hasta, CancellationToken ct = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(c => c.Caja)
            .Include(c => c.UsuarioCierre)
            .Where(c => c.IsActive);

        if (sucursalId.HasValue)
            query = query.Where(c => c.Caja.SucursalId == sucursalId.Value);

        if (desde.HasValue)
            query = query.Where(c => c.Fecha >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(c => c.Fecha <= hasta.Value);

        return await query.OrderByDescending(c => c.Fecha).ToListAsync(ct);
    }

    public async Task<CierreDiario?> GetPorIdConDetallesAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Include(c => c.Caja)
            .Include(c => c.UsuarioCierre)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);
}
