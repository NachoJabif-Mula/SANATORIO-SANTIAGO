using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para Comanda.
/// Extiende IRepository con operaciones propias del punto de venta.
/// </summary>
public interface IComandaRepository : IRepository<Comanda>
{
    /// <summary>
    /// Obtiene una comanda con todos sus ítems, pagos y navegaciones cargadas.
    /// </summary>
    Task<Comanda?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las comandas abiertas de una mesa específica.
    /// </summary>
    Task<IEnumerable<Comanda>> GetAbierdasPorMesaAsync(Guid mesaId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las cuentas corrientes abiertas (comandas exentas de turno) de un cliente específico.
    /// </summary>
    Task<IEnumerable<Comanda>> GetAbiertasPorClienteAsync(Guid clienteId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las comandas pendientes de sincronización.
    /// </summary>
    Task<IEnumerable<Comanda>> GetPendientesSyncAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las comandas con sus ítems, pagos y navegaciones cargadas.
    /// </summary>
    Task<IEnumerable<Comanda>> GetAllWithDetailsAsync(CancellationToken ct = default);

    /// <summary>
    /// Actualiza una comanda existente reemplazando físicamente todos sus ítems asociados.
    /// </summary>
    Task UpdateComandaWithItemsAsync(Comanda existing, List<ComandaItem> newItems, CancellationToken ct);
}
