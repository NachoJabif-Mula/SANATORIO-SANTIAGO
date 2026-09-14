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

    /// <summary>
    /// Cuenta las comandas abiertas de un turno concreto. Las cuentas corrientes
    /// abiertas (sin fecha contable, exentas de turno) quedan excluidas.
    /// </summary>
    Task<int> ContarAbiertasDeTurnoAsync(DateTime fechaContable, string turno, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las comandas abiertas de un turno concreto.
    /// </summary>
    Task<IEnumerable<Comanda>> GetAbiertasDeTurnoAsync(DateTime fechaContable, string turno, CancellationToken ct = default);

    /// <summary>
    /// Cuenta las comandas abiertas de una fecha contable, sin distinguir turno.
    /// </summary>
    Task<int> ContarAbiertasDeFechaAsync(DateTime fechaContable, CancellationToken ct = default);

    /// <summary>
    /// Persiste los cambios de un conjunto de comandas ya modificadas en memoria.
    /// Lo usa la transferencia de mesas abiertas al turno siguiente.
    /// </summary>
    Task GuardarCambiosAsync(IEnumerable<Comanda> comandas, CancellationToken ct = default);
}
