namespace BaresFamilia.Printing.Cli.Drivers;

public interface IPrinterDriver
{
    string ConnectionType { get; }
    Task<PrintResult> PrintAsync(PrintJobInput input, CancellationToken ct = default);
}
