using System.Diagnostics;

namespace BaresFamilia.Printing.Cli.Drivers;

/// <summary>
/// Emulador de impresión para pruebas y desarrollo. Escribe el contenido del ticket en un archivo de texto plano local.
/// </summary>
public class EmulatorPrinterDriver : IPrinterDriver
{
    public string ConnectionType => "Emulator";

    public async Task<PrintResult> PrintAsync(PrintJobInput input, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emulator_output");
            Directory.CreateDirectory(outputDir);

            string fileName = $"ticket_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt";
            string filePath = Path.Combine(outputDir, fileName);

            string fileHeader = $"========================================\n" +
                                $"EMULADOR DE IMPRESIÓN — BARES FAMILIA\n" +
                                $"Impresora: {input.PrinterName} ({input.ConnectionType})\n" +
                                $"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                $"========================================\n\n";

            await File.WriteAllTextAsync(filePath, fileHeader + input.Content, ct);
            sw.Stop();
            return PrintResult.Ok(sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return PrintResult.Error($"Error en Emulador de Impresión: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
