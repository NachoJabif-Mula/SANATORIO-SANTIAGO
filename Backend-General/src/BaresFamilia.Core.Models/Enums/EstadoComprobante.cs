namespace BaresFamilia.Core.Models.Enums;

/// <summary>
/// Estado del ciclo de vida de un comprobante electrónico frente a ARCA.
/// </summary>
public enum EstadoComprobante
{
    /// <summary>
    /// Registrado localmente pero todavía sin CAE: la sucursal no pudo contactar a ARCA
    /// al momento del cobro. Un worker lo reintenta cuando vuelve la conectividad.
    /// </summary>
    Pendiente = 0,

    /// <summary>
    /// Autorizado por ARCA: tiene CAE y vencimiento asignados.
    /// </summary>
    Emitido = 1,

    /// <summary>
    /// ARCA respondió rechazando la solicitud (errores de negocio en Errors/Observaciones).
    /// Requiere corrección manual: no se reintenta automáticamente.
    /// </summary>
    Rechazado = 2,

    /// <summary>
    /// Anulado posteriormente mediante nota de crédito.
    /// </summary>
    Anulado = 3
}
