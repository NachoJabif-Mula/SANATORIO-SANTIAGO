using System.Security.Claims;
using BaresFamilia.Core.Models.Contratos;

namespace BaresFamilia.Nube.Api.Extensions;

/// <summary>
/// Helpers para leer el alcance de sucursal del usuario autenticado a partir
/// de los claims emitidos por AuthController.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public const string SucursalIdClaim = "sucursal_id";
    public const string EsGlobalClaim = "es_global";

    public static Guid? GetSucursalId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(SucursalIdClaim);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static bool IsGlobal(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(EsGlobalClaim);
        return bool.TryParse(value, out var esGlobal) && esGlobal;
    }

    /// <summary>
    /// Empaqueta el alcance del usuario para pasárselo a la capa de servicios, que
    /// aplica las reglas de visibilidad sin depender de los claims ni de HTTP.
    /// </summary>
    public static AlcanceUsuario GetAlcance(this ClaimsPrincipal user)
        => new(user.IsGlobal(), user.GetSucursalId());
}
