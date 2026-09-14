using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Entities.Seguridad;

/// <summary>
/// Sesión persistida de un Usuario del Backoffice que inició sesión con
/// "mantener sesión iniciada". Permite validar, auditar y revocar el JWT
/// de larga duración (30 días) contra la base en cada request, en vez de
/// confiar únicamente en la expiración firmada del token.
/// </summary>
public class SesionUsuario : BaseEntity
{
    public Guid UsuarioId { get; set; }

    /// <summary>
    /// Hash SHA-256 (Base64) del JWT emitido. Nunca se guarda el token en claro.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Expiración del JWT asociado (debe coincidir con el claim "exp").
    /// </summary>
    public DateTime ExpiraEn { get; set; }

    /// <summary>
    /// Última vez que este token fue usado para autenticar un request.
    /// </summary>
    public DateTime? UltimoUsoEn { get; set; }

    /// <summary>
    /// Marcada true al hacer logout o al revocar la sesión manualmente.
    /// </summary>
    public bool Revocada { get; set; }

    public string? UserAgent { get; set; }

    // Navegación
    public Usuario Usuario { get; set; } = null!;
}
