using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para CuentaCorriente.
/// </summary>
public class CuentaCorrienteRepository : GenericRepository<CuentaCorriente>, ICuentaCorrienteRepository
{
    public CuentaCorrienteRepository(DbContext context) : base(context) { }

    public async Task<CuentaCorriente?> GetPorClienteAsync(Guid clienteId, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(c => c.ClienteId == clienteId, ct);

    public async Task<CuentaCorriente?> GetPorClienteConClienteAsync(Guid clienteId, CancellationToken ct = default)
        => await _dbSet
            .Include(c => c.Cliente)
            .FirstOrDefaultAsync(c => c.ClienteId == clienteId, ct);

    public async Task<IEnumerable<MovimientoCuentaCorriente>> GetMovimientosAsync(Guid cuentaCorrienteId, CancellationToken ct = default)
        => await _context.Set<MovimientoCuentaCorriente>()
            .AsNoTracking()
            .Where(m => m.CuentaCorrienteId == cuentaCorrienteId && m.IsActive)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
}
