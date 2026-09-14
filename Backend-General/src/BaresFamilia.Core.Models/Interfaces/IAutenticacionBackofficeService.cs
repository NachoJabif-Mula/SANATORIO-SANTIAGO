using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de autenticación del Backoffice: validación de credenciales y ciclo de
/// vida de las sesiones persistidas.
///
/// La emisión y firma del JWT queda en la capa de API, que es la dueña de la
/// configuración del esquema de autenticación del host.
/// Flujo: AuthController → IAutenticacionBackofficeService → AutenticacionBackofficeService → I*Repository → *Repository.
/// </summary>
public interface IAutenticacionBackofficeService
{
    /// <summary>
    /// Valida email y contraseña. Devuelve el usuario con rol y sucursal cargados,
    /// o null si las credenciales no son correctas.
    /// </summary>
    Task<Usuario?> ValidarCredencialesAsync(string email, string password, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el perfil del usuario autenticado con rol y sucursal cargados.
    /// </summary>
    Task<Usuario?> GetPerfilAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Persiste la sesión asociada a un token para poder revocarla antes de que expire.
    /// </summary>
    Task RegistrarSesionAsync(Guid usuarioId, string token, DateTime expiraEn, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// Revoca la sesión asociada a un token, si estaba persistida.
    /// </summary>
    Task RevocarSesionAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Verifica que la sesión asociada al token siga vigente y registra el uso.
    /// Un token sin sesión persistida se considera válido: las sesiones cortas
    /// (sin "mantener sesión iniciada") nunca se guardan.
    /// </summary>
    Task<bool> ValidarSesionVigenteAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Indica si el dispositivo POS dueño de un token M2M sigue habilitado.
    /// Revocarlo desde el Backoffice solo lo marca inactivo en la base, así que
    /// hay que revalidarlo en cada request.
    /// </summary>
    Task<bool> EsDispositivoActivoAsync(Guid dispositivoId, CancellationToken ct = default);
}
