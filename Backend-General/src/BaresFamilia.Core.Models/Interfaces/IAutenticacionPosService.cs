using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de autenticación por PIN del punto de venta de la sucursal.
/// Flujo: UsuarioController → IAutenticacionPosService → AutenticacionPosService → IUsuarioRepository → UsuarioRepository.
/// </summary>
public interface IAutenticacionPosService
{
    /// <summary>
    /// Busca el usuario activo dueño de ese PIN, con su rol cargado.
    /// Devuelve null si el PIN no corresponde a ningún usuario activo.
    /// </summary>
    Task<Usuario?> AutenticarPorPinAsync(string pin, CancellationToken ct = default);

    /// <summary>
    /// Indica si el PIN corresponde a un usuario habilitado a autorizar operaciones
    /// restringidas (rol gerente o permiso explícito de override).
    /// </summary>
    Task<bool> EsGerenteAsync(string pin, CancellationToken ct = default);

    /// <summary>
    /// Cantidad de usuarios activos ya sincronizados en la base local. El POS la
    /// consulta tras activarse para saber cuándo puede mostrar la pantalla de PIN.
    /// </summary>
    Task<int> ContarUsuariosDisponiblesAsync(CancellationToken ct = default);
}
