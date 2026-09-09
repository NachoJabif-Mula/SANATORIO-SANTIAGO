using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace BaresFamilia.Printing.Cli.Drivers;

/// <summary>
/// Driver de impresión para impresoras térmicas conectadas por red TCP/IP (Raw Socket puerto 9100).
/// </summary>
public class TcpPrinterDriver : IPrinterDriver
{
    public string ConnectionType => "Tcp";

    public async Task<PrintResult> PrintAsync(PrintJobInput input, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(input.IpAddress, input.Port);
            if (await Task.WhenAny(connectTask, Task.Delay(5000, ct)) != connectTask)
            {
                return PrintResult.Error($"Timeout de conexión TCP al puerto {input.Port} en {input.IpAddress}", sw.ElapsedMilliseconds);
            }

            using var stream = client.GetStream();
            stream.WriteTimeout = 6000;

            // Codificación IBM850 para impresoras térmicas en español
            Encoding encoding;
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                encoding = Encoding.GetEncoding(850);
            }
            catch
            {
                encoding = Encoding.UTF8;
            }

            byte[] bodyBytes = encoding.GetBytes(input.Content);
            await stream.WriteAsync(bodyBytes, ct);

            // Corte de papel ESC/POS (GS V 0)
            if (input.CutPaper)
            {
                byte[] cutBytes = new byte[] { 0x1D, 0x56, 0x00 };
                await stream.WriteAsync(cutBytes, ct);
            }

            await stream.FlushAsync(ct);
            sw.Stop();
            return PrintResult.Ok(sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return PrintResult.Error($"Error de impresión TCP: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
