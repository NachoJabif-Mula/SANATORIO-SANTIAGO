using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para Usuario.
/// </summary>
public interface IUsuarioRepository : IRepository<Usuario>
{
    /// <summary>
    /// Obtiene un usuario activo por su PIN de acceso, con el rol cargado.
    /// </summary>
    Task<Usuario?> GetActivoPorPinAsync(string pin, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un usuario activo por ID con el rol cargado.
    /// </summary>
    Task<Usuario?> GetActivoConRolAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Cuenta los usuarios activos disponibles en la base.
    /// </summary>
    Task<int> ContarActivosAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene los usuarios activos con rol y sucursal cargados. Si sucursalId es
    /// null devuelve los de todas las sucursales (vista consolidada del backoffice).
    /// </summary>
    Task<IEnumerable<Usuario>> GetActivosConRolYSucursalAsync(Guid? sucursalId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un usuario activo por ID con rol y sucursal cargados.
    /// </summary>
    Task<Usuario?> GetActivoConRolYSucursalAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un usuario activo por email, con rol y sucursal cargados.
    /// </summary>
    Task<Usuario?> GetActivoPorEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los usuarios activos de una sucursal, para la sincronización del POS.
    /// </summary>
    Task<IEnumerable<Usuario>> GetActivosDeSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>
    /// Indica si el PIN ya está asignado a otro empleado activo de la misma sucursal.
    /// El PIN identifica al empleado dentro de su sucursal, por eso debe ser único ahí.
    /// </summary>
    Task<bool> ExistePinEnSucursalAsync(Guid sucursalId, string pin, Guid? idExcluido, CancellationToken ct = default);
}
