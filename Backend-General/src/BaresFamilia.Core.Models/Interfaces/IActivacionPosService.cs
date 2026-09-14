using BaresFamilia.Core.Models.Contratos.Seguridad;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de activación del POS de la sucursal contra la Nube.
/// Flujo: DispositivoController → IActivacionPosService → ActivacionPosService → I*Repository → *Repository.
/// </summary>
public interface IActivacionPosService
{
    /// <summary>
    /// Devuelve los datos de la activación vigente, o null si el POS no está vinculado.
    /// </summary>
    Task<EstadoActivacionPos?> GetEstadoAsync(CancellationToken ct = default);

    /// <summary>
    /// Canjea el código contra la Nube y guarda localmente el token M2M resultante,
    /// dando de baja cualquier activación anterior.
    /// </summary>
    Task ActivarAsync(string codigoActivacion, CancellationToken ct = default);

    /// <summary>
    /// Desvincula el POS dando de baja las activaciones locales.
    /// </summary>
    Task DesactivarAsync(CancellationToken ct = default);
}
