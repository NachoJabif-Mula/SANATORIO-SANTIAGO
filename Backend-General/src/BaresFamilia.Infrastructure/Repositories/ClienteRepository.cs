using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Cliente con queries de cuentas corrientes.
/// Hereda GenericRepository para CRUD estándar.
/// </summary>
public class ClienteRepository : GenericRepository<Cliente>, IClienteRepository
{
    public ClienteRepository(DbContext context) : base(context) { }

    public async Task<Cliente?> GetWithCuentaCorrienteAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .Where(c => c.Id == id && c.IsActive)
            .Include(c => c.CuentaCorriente)
            .FirstOrDefaultAsync(ct);

    public async Task<IEnumerable<Cliente>> BuscarPorNombreAsync(string nombre, CancellationToken ct = default)
        => await _dbSet
            .Where(c => c.IsActive && EF.Functions.ILike(c.Nombre, $"%{nombre}%"))
            .Include(c => c.CuentaCorriente)
            .OrderBy(c => c.Nombre)
            .ToListAsync(ct);
}
