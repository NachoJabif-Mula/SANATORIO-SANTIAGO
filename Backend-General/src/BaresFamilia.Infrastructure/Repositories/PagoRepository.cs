using BaresFamilia.Core.Models.Contratos.Cajas;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Repositories;

/// <summary>
/// Repositorio específico para Pago.
/// </summary>
public class PagoRepository : GenericRepository<Pago>, IPagoRepository
{
    public PagoRepository(DbContext context) : base(context) { }

    public async Task<List<DesglosePorMetodo>> GetDesglosePorMetodoDeTurnoAsync(Guid turnoCajaId, CancellationToken ct = default)
        => await AgruparPorMetodo(_dbSet.Where(p => p.TurnoCajaId == turnoCajaId && p.IsActive)).ToListAsync(ct);

    public async Task<List<DesglosePorMetodo>> GetDesglosePorMetodoDeTurnosAsync(IReadOnlyCollection<Guid> turnoCajaIds, CancellationToken ct = default)
    {
        if (turnoCajaIds.Count == 0)
            return [];

        return await AgruparPorMetodo(_dbSet.Where(p => turnoCajaIds.Contains(p.TurnoCajaId) && p.IsActive)).ToListAsync(ct);
    }

    public async Task<decimal> GetTotalDeTurnosAsync(IReadOnlyCollection<Guid> turnoCajaIds, CancellationToken ct = default)
    {
        if (turnoCajaIds.Count == 0)
            return 0m;

        return await _dbSet
            .Where(p => turnoCajaIds.Contains(p.TurnoCajaId) && p.IsActive)
            .SumAsync(p => p.Monto, ct);
    }

    private static IQueryable<DesglosePorMetodo> AgruparPorMetodo(IQueryable<Pago> pagos)
        => pagos
            .GroupBy(p => new { p.MetodoPagoId, p.MetodoPago.Nombre })
            .Select(g => new DesglosePorMetodo
            {
                MetodoPagoId = g.Key.MetodoPagoId,
                MetodoPagoNombre = g.Key.Nombre,
                CantidadOperaciones = g.Count(),
                Total = g.Sum(p => p.Monto)
            });
}
