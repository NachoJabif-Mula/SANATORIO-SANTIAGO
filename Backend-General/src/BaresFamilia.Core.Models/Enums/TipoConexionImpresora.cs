namespace BaresFamilia.Core.Models.Enums;

/// <summary>
/// Tipo de conexión física de una impresora térmica.
/// </summary>
public enum TipoConexionImpresora
{
    /// <summary>
    /// Conexión por red TCP/IP (puerto 9100 por defecto).
    /// </summary>
    Red,

    /// <summary>
    /// Conexión por USB usando el spooler de Windows.
    /// </summary>
    USB
}
