namespace BaresFamilia.Nube.Api.Extensions;

/// <summary>
/// Helpers para leer la cabecera Authorization.
/// </summary>
public static class HeaderDictionaryExtensions
{
    private const string PrefijoBearer = "Bearer ";

    /// <summary>
    /// Extrae el token crudo de la cabecera Authorization, sin el prefijo "Bearer ".
    /// Devuelve cadena vacía si la cabecera no vino.
    /// </summary>
    public static string LeerTokenBearer(this IHeaderDictionary headers)
    {
        var authHeader = headers.Authorization.ToString();

        return authHeader.StartsWith(PrefijoBearer, StringComparison.OrdinalIgnoreCase)
            ? authHeader[PrefijoBearer.Length..]
            : authHeader;
    }
}
