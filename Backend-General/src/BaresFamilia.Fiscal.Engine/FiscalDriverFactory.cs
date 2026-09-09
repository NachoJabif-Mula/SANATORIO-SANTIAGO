using BaresFamilia.Fiscal.Engine.Drivers;

namespace BaresFamilia.Fiscal.Engine;

/// <summary>
/// Factoría para resolver la instancia del driver fiscal según la marca de la impresora.
/// </summary>
public static class FiscalDriverFactory
{
    private static readonly EpsonDriver _epsonDriver = new();
    private static readonly HasarDriver _hasarDriver = new();

    /// <summary>
    /// Devuelve la implementación de <see className="IFiscalDriver"/> correspondiente a la marca especificada.
    /// </summary>
    public static IFiscalDriver GetDriver(string driverName)
    {
        return driverName.ToLowerInvariant().Trim() switch
        {
            "epson" or "fiscalepson" => _epsonDriver,
            "hasar" or "fiscalhasar" => _hasarDriver,
            _ => throw new NotSupportedException($"Driver fiscal '{driverName}' no soportado. Drivers válidos: 'Epson', 'Hasar'.")
        };
    }
}
