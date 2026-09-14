using BaresFamilia.Core.Models.Entities.Seguridad;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para DispositivoActivacion.
/// La Nube lo usa para emitir y revocar activaciones; la sucursal, para guardar
/// la suya y saber si sigue vinculada.
/// </summary>
public interface IDispositivoActivacionRepository : IRepository<DispositivoActivacion>
{
    /// <summary>
    /// Obtiene el dispositivo dueño de un código de activación, con su sucursal cargada.
    /// </summary>
    Task<DispositivoActivacion?> GetPorCodigoAsync(string codigoActivacion, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los dispositivos vigentes con su sucursal cargada.
    /// </summary>
    Task<IEnumerable<DispositivoActivacion>> GetVigentesConSucursalAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene un dispositivo por ID sin filtrar por IsActive: el POS consulta su
    /// propio estado justamente para enterarse de que fue revocado.
    /// </summary>
    Task<DispositivoActivacion?> GetPorIdIncluyendoInactivosAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las activaciones marcadas como activadas en esta base local.
    /// </summary>
    Task<IEnumerable<DispositivoActivacion>> GetActivadasAsync(CancellationToken ct = default);

    /// <summary>
    /// Persiste los cambios de un conjunto de activaciones ya modificadas en memoria.
    /// </summary>
    Task GuardarCambiosAsync(IEnumerable<DispositivoActivacion> dispositivos, CancellationToken ct = default);
}
