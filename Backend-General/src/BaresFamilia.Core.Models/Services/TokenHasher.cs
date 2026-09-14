using System.Security.Cryptography;
using System.Text;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Hashea tokens (JWT M2M, JWT de sesión de usuario) antes de persistirlos,
/// para poder validarlos/revocarlos contra la base sin guardar el token en claro.
/// </summary>
public static class TokenHasher
{
    public static string Compute(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }
}
