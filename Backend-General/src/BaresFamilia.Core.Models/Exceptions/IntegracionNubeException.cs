namespace BaresFamilia.Core.Models.Exceptions;

/// <summary>
/// Se lanza cuando la sucursal no logra completar una operación contra la API Nube
/// por un problema de infraestructura: falta de configuración, red caída o una
/// respuesta que no se puede interpretar.
///
/// Se distingue de <see cref="ReglaNegocioException"/> porque el pedido era válido:
/// lo que falló fue la comunicación. La capa de API la traduce a un 500.
/// </summary>
public class IntegracionNubeException : Exception
{
    public IntegracionNubeException(string mensaje) : base(mensaje) { }

    public IntegracionNubeException(string mensaje, Exception innerException) : base(mensaje, innerException) { }
}
