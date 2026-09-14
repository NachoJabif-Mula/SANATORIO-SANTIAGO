using BaresFamilia.Core.Models.Contratos.Sincronizacion;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Entities;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Acceso a datos del punto de entrada de sincronización en la Nube.
///
/// Expone consultas por lote (en vez de por ítem) y una escritura diferida: el
/// servicio resuelve las reglas de integridad en memoria y confirma todo junto,
/// evitando el patrón N+1 que tenía la ingesta original.
/// </summary>
public interface ISincronizacionNubeRepository
{
    /// <summary>Sucursal a la que pertenece un usuario, o null si no existe.</summary>
    Task<Guid?> GetSucursalDeUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Primera sucursal activa, usada como último recurso para ubicar el lote.</summary>
    Task<Sucursal?> GetPrimeraSucursalActivaAsync(CancellationToken ct = default);

    /// <summary>Nombre de una sucursal, o null si no existe.</summary>
    Task<string?> GetNombreSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>Indica si la sucursal existe.</summary>
    Task<bool> ExisteSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>
    /// De un conjunto de ids, devuelve los que ya existen en la Nube. Una sola
    /// consulta reemplaza a un AnyAsync por ítem.
    /// </summary>
    Task<HashSet<Guid>> FiltrarIdsExistentesAsync<TEntity>(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        where TEntity : BaseEntity;

    /// <summary>Cajas ya existentes, indexadas por id, listas para actualizar.</summary>
    Task<Dictionary<Guid, Caja>> GetCajasPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Turnos ya existentes, indexados por id, listos para actualizar.</summary>
    Task<Dictionary<Guid, TurnoCaja>> GetTurnosCajaPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Comandas ya existentes con sus ítems cargados, indexadas por id.</summary>
    Task<Dictionary<Guid, Comanda>> GetComandasConItemsPorIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Cuentas corrientes de un conjunto de clientes, indexadas por ClienteId.</summary>
    Task<Dictionary<Guid, CuentaCorriente>> GetCuentasCorrientesPorClienteAsync(IReadOnlyCollection<Guid> clienteIds, CancellationToken ct = default);

    /// <summary>Caja activa de la sucursal, o null si todavía no tiene ninguna.</summary>
    Task<Caja?> GetCajaActivaDeSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>Primer usuario activo de la sucursal, o null si todavía no llegó ninguno.</summary>
    Task<Usuario?> GetPrimerUsuarioActivoDeSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>Primer rol activo del sistema, o null si no hay ninguno.</summary>
    Task<Rol?> GetPrimerRolActivoAsync(CancellationToken ct = default);

    /// <summary>Primer tipo de venta activo, usado como reemplazo cuando el recibido no existe.</summary>
    Task<TipoVenta?> GetPrimerTipoVentaActivoAsync(CancellationToken ct = default);

    /// <summary>Primer método de pago activo, usado como reemplazo cuando el recibido no existe.</summary>
    Task<MetodoPago?> GetPrimerMetodoPagoActivoAsync(CancellationToken ct = default);

    /// <summary>Marca entidades nuevas para insertarse en la próxima confirmación.</summary>
    Task AgregarAsync<TEntity>(IEnumerable<TEntity> entidades, CancellationToken ct = default) where TEntity : BaseEntity;

    /// <summary>Marca ítems de comanda para borrarse físicamente en la próxima confirmación.</summary>
    void EliminarItemsDeComanda(IEnumerable<ComandaItem> items);

    /// <summary>Confirma en la base todos los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken ct = default);

    /// <summary>Conteos consolidados de una sucursal para el panel de sincronización.</summary>
    Task<ConteosSincronizacion> GetConteosDeSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>Sucursales activas, para listar el estado de sincronización de todas.</summary>
    Task<IEnumerable<Sucursal>> GetSucursalesActivasAsync(CancellationToken ct = default);
}
