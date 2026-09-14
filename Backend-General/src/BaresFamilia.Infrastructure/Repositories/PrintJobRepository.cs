using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para PrintJob.
/// </summary>
public class PrintJobRepository : GenericRepository<PrintJob>, IPrintJobRepository
{
    public PrintJobRepository(DbContext context) : base(context) { }

    public async Task<PrintJob?> GetPorIdIncluyendoInactivosAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(j => j.Id == id, ct);
}
