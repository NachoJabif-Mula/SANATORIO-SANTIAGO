namespace BaresFamilia.Core.Models.Contratos.Seguridad;

/// <summary>
/// Estado de vinculación del POS local con la Nube.
/// </summary>
public record EstadoActivacionPos(
    Guid SucursalId,
    string SucursalNombre,
    Guid DispositivoId,
    string NombreDispositivo,
    DateTime ExpiresAt);

/// <summary>
/// Respuesta de activación que devuelve la API Nube al canjear un código.
/// </summary>
public class DatosActivacion
{
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Guid SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
    public Guid DispositivoId { get; set; }
    public string NombreDispositivo { get; set; } = string.Empty;
}

/// <summary>
/// Datos de activación junto con el cuerpo crudo de la respuesta, que la sucursal
/// guarda tal cual para poder releer el token M2M en cada sincronización.
/// </summary>
public record RespuestaActivacion(DatosActivacion Datos, string CuerpoCrudo);
