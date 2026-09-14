using BaresFamilia.Core.Models.Contratos.Seguridad;
using BaresFamilia.Core.Models.Contratos;
using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio para la gestión de empleados del Backoffice.
/// Flujo: EmpleadoController → IEmpleadoService → EmpleadoService → IUsuarioRepository → UsuarioRepository.
/// </summary>
public interface IEmpleadoService
{
    /// <summary>
    /// Obtiene los empleados visibles para el alcance del usuario autenticado.
    /// </summary>
    Task<IEnumerable<Usuario>> GetAsync(Guid? sucursalSolicitada, AlcanceUsuario alcance, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los empleados activos de una sucursal para la sincronización del POS.
    /// </summary>
    Task<IEnumerable<Usuario>> GetActivosDeSucursalAsync(Guid sucursalId, CancellationToken ct = default);

    /// <summary>
    /// Da de alta un empleado. Devuelve el usuario creado con su rol y sucursal cargados.
    /// </summary>
    Task<Usuario> CrearAsync(DatosEmpleado datos, AlcanceUsuario alcance, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un empleado existente.
    /// </summary>
    Task ActualizarAsync(Guid id, DatosEmpleado datos, AlcanceUsuario alcance, CancellationToken ct = default);

    /// <summary>
    /// Da de baja lógica a un empleado.
    /// </summary>
    Task DesactivarAsync(Guid id, AlcanceUsuario alcance, CancellationToken ct = default);
}
