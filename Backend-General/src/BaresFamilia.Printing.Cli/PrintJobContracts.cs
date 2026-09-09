namespace BaresFamilia.Printing.Cli;

/// <summary>
/// Modelo DTO para un trabajo de impresión no fiscal (comandera/ticket).
/// </summary>
public class PrintJobInput
{
    public string PrinterName { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = "Tcp"; // "Tcp", "Usb", "Emulator"
    public string IpAddress { get; set; } = "192.168.1.100";
    public int Port { get; set; } = 9100;
    public string Content { get; set; } = string.Empty;
    public bool CutPaper { get; set; } = true;
}

/// <summary>
/// Resultado del proceso de impresión no fiscal.
/// </summary>
public class PrintResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }

    public static PrintResult Ok(long elapsedMs = 0) => new() { Success = true, ElapsedMs = elapsedMs };
    public static PrintResult Error(string message, long elapsedMs = 0) => new() { Success = false, ErrorMessage = message, ElapsedMs = elapsedMs };
}
