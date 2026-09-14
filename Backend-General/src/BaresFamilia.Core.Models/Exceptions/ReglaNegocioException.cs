namespace BaresFamilia.Core.Models.Exceptions;

/// <summary>
/// Se lanza cuando una operación viola una regla de negocio del dominio
/// (por ejemplo: un nombre obligatorio vacío o un duplicado no permitido).
///
/// Permite que la capa de servicios exprese el error sin conocer HTTP:
/// la capa de API la traduce a un 400 Bad Request mediante
/// <c>ManejadorExcepcionesMiddleware</c>.
/// </summary>
public class ReglaNegocioException : Exception
{
    /// <summary>
    /// Código estable que el cliente puede interpretar para reaccionar de forma
    /// distinta según el caso (por ejemplo "MesasAbiertas", que en el POS abre el
    /// diálogo de transferencia de mesas). Null cuando alcanza con el mensaje.
    /// </summary>
    public string? Codigo { get; }

    /// <summary>
    /// Datos adicionales que acompañan al error en la respuesta, como la cantidad
    /// de comandas que impiden cerrar el turno.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Detalles { get; }

    public ReglaNegocioException(string mensaje) : this(mensaje, null, null) { }

    public ReglaNegocioException(string mensaje, string? codigo, IReadOnlyDictionary<string, object?>? detalles = null)
        : base(mensaje)
    {
        Codigo = codigo;
        Detalles = detalles ?? new Dictionary<string, object?>();
    }
}
