using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BaresFamilia.Printing.Cli.Drivers;

/// <summary>
/// Driver de impresión para impresoras locales/USB usando la API Win32 Spooler (winspool.drv).
/// Permite enviar contenido raw (ESC/POS) a impresoras USB en Windows sin bloqueos de archivo.
/// </summary>
public class WindowsPrinterDriver : IPrinterDriver
{
    public string ConnectionType => "Usb";

    #region Win32 Spooler API P/Invoke

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName = "BaresFamilia_Ticket";
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile = null;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType = "RAW";
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, Int32 level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, Int32 dwCount, out Int32 dwWritten);

    #endregion

    public Task<PrintResult> PrintAsync(PrintJobInput input, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return PrintResult.Error("Impresión vía Spooler Win32 solo es compatible con Windows.", sw.ElapsedMilliseconds);
            }

            IntPtr hPrinter = IntPtr.Zero;
            try
            {
                if (!OpenPrinter(input.PrinterName.Normalize(), out hPrinter, IntPtr.Zero))
                {
                    int err = Marshal.GetLastWin32Error();
                    return PrintResult.Error($"No se pudo abrir la impresora '{input.PrinterName}' en Spooler de Windows (Win32 Error: {err}).", sw.ElapsedMilliseconds);
                }

                var di = new DOCINFOA();
                if (!StartDocPrinter(hPrinter, 1, di))
                {
                    ClosePrinter(hPrinter);
                    return PrintResult.Error($"Error al iniciar documento en Spooler para '{input.PrinterName}'.", sw.ElapsedMilliseconds);
                }

                if (!StartPagePrinter(hPrinter))
                {
                    EndDocPrinter(hPrinter);
                    ClosePrinter(hPrinter);
                    return PrintResult.Error($"Error al iniciar página en Spooler.", sw.ElapsedMilliseconds);
                }

                // Codificación IBM850
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

                // Agregar corte de papel si corresponde
                if (input.CutPaper)
                {
                    byte[] cutBytes = new byte[] { 0x1D, 0x56, 0x00 };
                    var merged = new byte[bodyBytes.Length + cutBytes.Length];
                    Buffer.BlockCopy(bodyBytes, 0, merged, 0, bodyBytes.Length);
                    Buffer.BlockCopy(cutBytes, 0, merged, bodyBytes.Length, cutBytes.Length);
                    bodyBytes = merged;
                }

                IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(bodyBytes.Length);
                Marshal.Copy(bodyBytes, 0, pUnmanagedBytes, bodyBytes.Length);

                bool success = WritePrinter(hPrinter, pUnmanagedBytes, bodyBytes.Length, out int written);
                Marshal.FreeCoTaskMem(pUnmanagedBytes);

                EndPagePrinter(hPrinter);
                EndDocPrinter(hPrinter);
                ClosePrinter(hPrinter);

                sw.Stop();
                if (success && written > 0)
                {
                    return PrintResult.Ok(sw.ElapsedMilliseconds);
                }

                return PrintResult.Error($"WritePrinter devolvió falso o 0 bytes escritos.", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                if (hPrinter != IntPtr.Zero) try { ClosePrinter(hPrinter); } catch { }
                sw.Stop();
                return PrintResult.Error($"Excepción en Spooler Win32: {ex.Message}", sw.ElapsedMilliseconds);
            }
        }, ct);
    }
}
