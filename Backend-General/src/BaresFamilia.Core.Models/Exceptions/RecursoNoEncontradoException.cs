namespace BaresFamilia.Core.Models.Exceptions;

/// <summary>
/// Se lanza cuando la operación solicitada apunta a una entidad que no existe
/// o fue dada de baja lógicamente.
///
/// La capa de API la traduce a un 404 Not Found mediante
/// <c>ManejadorExcepcionesMiddleware</c>.
/// </summary>
public class RecursoNoEncontradoException : Exception
{
    public RecursoNoEncontradoException(string mensaje) : base(mensaje) { }

    /// <summary>
    /// Construye el mensaje estándar "&lt;recurso&gt; con ID '&lt;id&gt;' no encontrado."
    /// </summary>
    public static RecursoNoEncontradoException ParaId(string recurso, Guid id)
        => new($"{recurso} con ID '{id}' no encontrado.");
}
