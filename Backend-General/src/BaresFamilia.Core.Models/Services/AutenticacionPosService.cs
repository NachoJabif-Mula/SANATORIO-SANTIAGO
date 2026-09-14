using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de autenticación por PIN del punto de venta.
/// </summary>
public class AutenticacionPosService : IAutenticacionPosService
{
    private const string RolGerente = "gerente";
    private const string PermisoOverrideGerente = "gerente.override";

    private readonly IUsuarioRepository _usuarioRepository;

    public AutenticacionPosService(IUsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    public async Task<Usuario?> AutenticarPorPinAsync(string pin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pin))
            throw new ReglaNegocioException("El PIN es obligatorio.");

        return await _usuarioRepository.GetActivoPorPinAsync(pin.Trim(), ct);
    }

    public async Task<bool> EsGerenteAsync(string pin, CancellationToken ct = default)
    {
        var usuario = await AutenticarPorPinAsync(pin, ct);
        if (usuario is null)
            return false;

        return usuario.Rol.Permisos.Contains(PermisoOverrideGerente)
            || usuario.Rol.Nombre.Equals(RolGerente, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<int> ContarUsuariosDisponiblesAsync(CancellationToken ct = default)
        => await _usuarioRepository.ContarActivosAsync(ct);
}
