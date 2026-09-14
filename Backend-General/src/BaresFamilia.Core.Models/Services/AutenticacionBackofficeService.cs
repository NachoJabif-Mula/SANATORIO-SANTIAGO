using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de autenticación del Backoffice.
/// </summary>
public class AutenticacionBackofficeService : IAutenticacionBackofficeService
{
    /// <summary>El User-Agent se guarda recortado: es informativo y la columna es acotada.</summary>
    private const int LargoMaximoUserAgent = 300;

    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ISesionUsuarioRepository _sesionRepository;
    private readonly IRepository<DispositivoActivacion> _dispositivoRepository;

    public AutenticacionBackofficeService(
        IUsuarioRepository usuarioRepository,
        ISesionUsuarioRepository sesionRepository,
        IRepository<DispositivoActivacion> dispositivoRepository)
    {
        _usuarioRepository = usuarioRepository;
        _sesionRepository = sesionRepository;
        _dispositivoRepository = dispositivoRepository;
    }

    public async Task<Usuario?> ValidarCredencialesAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return null;

        var usuario = await _usuarioRepository.GetActivoPorEmailAsync(email.Trim().ToLower(), ct);
        if (usuario is null)
            return null;

        return BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash) ? usuario : null;
    }

    public async Task<Usuario?> GetPerfilAsync(Guid usuarioId, CancellationToken ct = default)
        => await _usuarioRepository.GetActivoConRolYSucursalAsync(usuarioId, ct);

    public async Task RegistrarSesionAsync(Guid usuarioId, string token, DateTime expiraEn, string? userAgent, CancellationToken ct = default)
    {
        await _sesionRepository.AddAsync(new SesionUsuario
        {
            UsuarioId = usuarioId,
            TokenHash = TokenHasher.Compute(token),
            ExpiraEn = expiraEn,
            UserAgent = string.IsNullOrEmpty(userAgent)
                ? null
                : userAgent[..Math.Min(userAgent.Length, LargoMaximoUserAgent)]
        }, ct);
    }

    public async Task RevocarSesionAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        var sesion = await _sesionRepository.GetPorTokenHashAsync(TokenHasher.Compute(token), ct);
        if (sesion is null || sesion.Revocada)
            return;

        sesion.Revocada = true;
        await _sesionRepository.UpdateAsync(sesion, ct);
    }

    public async Task<bool> ValidarSesionVigenteAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return true;

        var sesion = await _sesionRepository.GetPorTokenHashAsync(TokenHasher.Compute(token), ct);

        // Sin fila no hay nada que revocar: es una sesión corta, que solo expira sola.
        if (sesion is null)
            return true;

        if (sesion.Revocada || sesion.ExpiraEn < DateTime.UtcNow)
            return false;

        sesion.UltimoUsoEn = DateTime.UtcNow;
        await _sesionRepository.UpdateAsync(sesion, ct);
        return true;
    }

    public async Task<bool> EsDispositivoActivoAsync(Guid dispositivoId, CancellationToken ct = default)
        => await _dispositivoRepository.ExistsAsync(dispositivoId, ct);
}
