namespace BaresFamilia.Nube.Api.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    /// Los parámetros [FromQuery] DateTime? llegan con Kind=Unspecified (el binder de
    /// ASP.NET Core no lo setea). Las columnas "timestamp with time zone" de Postgres
    /// exigen Kind=Utc explícito o Npgsql tira ArgumentException al armar la query.
    /// Como todas las fechas del sistema se manejan en UTC, es seguro forzarlo acá.
    /// </summary>
    public static DateTime AsUtc(this DateTime dt)
        => DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    public static DateTime? AsUtc(this DateTime? dt)
        => dt.HasValue ? DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc) : null;
}
