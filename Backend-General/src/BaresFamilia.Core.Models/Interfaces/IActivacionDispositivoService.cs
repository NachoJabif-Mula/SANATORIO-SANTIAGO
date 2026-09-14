using BaresFamilia.Core.Models.Entities.Seguridad;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de activación de dispositivos POS en la Nube: emisión de códigos,
/// canje, listado y revocación.
///
/// La firma del JWT M2M queda en la capa de API, dueña de la configuración del
/// esquema de autenticación.
/// Flujo: DispositivosController → IActivacionDispositivoService → ActivacionDispositivoService → IDispositivoActivacionRepository → DispositivoActivacionRepository.
/// </summary>
public interface IActivacionDispositivoService
{
    /// <summary>
    /// Genera un código de activación de un solo uso para una sucursal, válido por 24 horas.
    /// </summary>
    Task<DispositivoActivacion> GenerarCodigoAsync(Guid sucursalId, string? nombreDispositivo, CancellationToken ct = default);

    /// <summary>
    /// Valida que un código exista, no haya sido usado y no esté vencido, y devuelve
    /// el dispositivo listo para activarse.
    /// </summary>
    Task<DispositivoActivacion> ValidarCodigoCanjeableAsync(string codigoActivacion, CancellationToken ct = default);

    /// <summary>
    /// Marca el dispositivo como activado y guarda el hash del token emitido.
    /// </summary>
    Task ConfirmarActivacionAsync(DispositivoActivacion dispositivo, string token, CancellationToken ct = default);

    /// <summary>
    /// Lista los dispositivos vigentes con su sucursal.
    /// </summary>
    Task<IEnumerable<DispositivoActivacion>> ListarVigentesAsync(CancellationToken ct = default);

    /// <summary>
    /// Revoca un dispositivo: deja de aceptarse su token M2M en la próxima request.
    /// </summary>
    Task RevocarAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Indica si un dispositivo sigue vinculado. Devuelve null si no existe.
    /// </summary>
    Task<bool?> EstaActivoAsync(Guid id, CancellationToken ct = default);
}
