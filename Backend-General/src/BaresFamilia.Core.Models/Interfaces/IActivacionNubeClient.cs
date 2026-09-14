using BaresFamilia.Core.Models.Contratos.Seguridad;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Cliente HTTP contra el endpoint de activación de la API Nube.
/// </summary>
public interface IActivacionNubeClient
{
    /// <summary>
    /// Canjea un código de activación contra la Nube y devuelve el token M2M emitido.
    /// </summary>
    Task<RespuestaActivacion> CanjearCodigoAsync(string codigoActivacion, CancellationToken ct = default);
}
