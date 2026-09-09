namespace BaresFamilia.Core.Models.Configuration;

/// <summary>
/// Configuración JWT para emisión y validación de tokens.
/// Se mapea desde appsettings.json sección "Jwt".
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Clave secreta para firmar los tokens (mínimo 32 caracteres).
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Emisor del token (ej: "BaresFamilia.Nube.Api").
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Audiencia del token (ej: "BaresFamilia.Local.Api").
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Duración del token M2M en días (por defecto 365 para larga duración).
    /// </summary>
    public int M2MTokenDurationDays { get; set; } = 365;
}
