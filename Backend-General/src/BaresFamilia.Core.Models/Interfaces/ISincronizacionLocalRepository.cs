using BaresFamilia.Core.Models.Entities;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Acceso a datos del motor de sincronización de la sucursal.
///
/// Cubre las dos mitades del ciclo: leer lo que está pendiente de subir (push) y
/// volcar el catálogo que baja de la Nube (pull). Mantiene EF Core fuera del worker,
/// que queda como pura orquestación.
/// </summary>
public interface ISincronizacionLocalRepository
{
    // ── Activación ──────────────────────────────────────────

    /// <summary>Activación vigente del POS, o null si no está vinculado.</summary>
    Task<DispositivoActivacion?> GetActivacionVigenteAsync(CancellationToken ct = default);

    /// <summary>
    /// Borra físicamente las activaciones locales. Se usa cuando el Backoffice revocó
    /// el dispositivo: el POS debe quedar desvinculado por completo.
    /// </summary>
    Task EliminarTodasLasActivacionesAsync(CancellationToken ct = default);

    // ── Sucursal propia ─────────────────────────────────────

    Task<bool> ExisteSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>Sucursal sin seguimiento de cambios, solo para leer su nombre.</summary>
    Task<Sucursal?> GetSucursalSinSeguimientoAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>Sucursal con seguimiento, para actualizar sus datos desde la Nube.</summary>
    Task<Sucursal?> GetSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    // ── Push: registros pendientes de subir ─────────────────

    /// <summary>
    /// Comandas a subir: las cerradas (cobradas o anuladas) más las que siguen
    /// abiertas pero tienen algún ítem anulado, porque esa anulación vive en el ítem
    /// y de otro modo nunca llegaría a la Nube.
    /// </summary>
    Task<List<Comanda>> GetComandasPendientesAsync(CancellationToken ct = default);

    Task<List<Pago>> GetPagosPendientesAsync(CancellationToken ct = default);

    Task<List<MovimientoCaja>> GetMovimientosCajaPendientesAsync(CancellationToken ct = default);

    Task<List<CierreDiario>> GetCierresDiariosPendientesAsync(CancellationToken ct = default);

    Task<List<Cliente>> GetClientesPendientesAsync(CancellationToken ct = default);

    Task<List<MovimientoCuentaCorriente>> GetMovimientosCuentaCorrientePendientesAsync(CancellationToken ct = default);

    Task<List<TurnoCaja>> GetTurnosCajaPendientesAsync(CancellationToken ct = default);

    Task<List<Caja>> GetCajasPendientesAsync(CancellationToken ct = default);

    // ── Pull: volcado del catálogo que baja de la Nube ───────

    /// <summary>Todas las filas de una tabla, con seguimiento, para comparar contra lo que baja.</summary>
    Task<List<TEntity>> GetTodosAsync<TEntity>(CancellationToken ct = default) where TEntity : BaseEntity;

    /// <summary>Indica si existe la entidad, sin importar su estado de baja lógica.</summary>
    Task<bool> ExisteAsync<TEntity>(Guid id, CancellationToken ct = default) where TEntity : BaseEntity;

    /// <summary>Indica si la tabla tiene al menos una fila.</summary>
    Task<bool> HayAlgunoAsync<TEntity>(CancellationToken ct = default) where TEntity : BaseEntity;

    /// <summary>Indica si un cliente ya tiene cuenta corriente abierta.</summary>
    Task<bool> ExisteCuentaCorrienteDeClienteAsync(Guid clienteId, CancellationToken ct = default);

    /// <summary>Marca una entidad nueva para insertarse en la próxima confirmación.</summary>
    void Agregar<TEntity>(TEntity entidad) where TEntity : BaseEntity;

    /// <summary>Confirma en la base todos los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
