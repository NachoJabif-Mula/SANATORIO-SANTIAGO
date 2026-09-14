namespace BaresFamilia.Core.Models.Exceptions;

/// <summary>
/// Se lanza cuando el usuario autenticado no tiene alcance suficiente para la
/// operación pedida (por ejemplo, un usuario de sucursal intentando tocar datos
/// de otra sucursal o asignar un rol global).
///
/// La capa de API la traduce a un 403 Forbidden mediante
/// <c>ManejadorExcepcionesMiddleware</c>.
/// </summary>
public class AccesoDenegadoException : Exception
{
    public AccesoDenegadoException(string mensaje = "No tiene permisos para realizar esta operación.")
        : base(mensaje) { }
}
