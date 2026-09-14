using BaresFamilia.Core.Models.Entities.Seguridad;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio de sesiones persistidas del Backoffice.
/// Solo se persisten las sesiones abiertas con "mantener sesión iniciada": son las
/// únicas que se pueden revocar antes de que expire el JWT.
/// </summary>
public interface ISesionUsuarioRepository : IRepository<SesionUsuario>
{
    /// <summary>
    /// Obtiene la sesión asociada al hash de un token, o null si no fue persistida.
    /// </summary>
    Task<SesionUsuario?> GetPorTokenHashAsync(string tokenHash, CancellationToken ct = default);
}
