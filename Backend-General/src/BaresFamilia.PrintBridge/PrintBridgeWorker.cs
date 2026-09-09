using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace BaresFamilia.PrintBridge;

/// <summary>
/// Worker de servicio en segundo plano para el Bridge de Impresión Local.
/// Se conecta al Hub de SignalR backend, escucha eventos de trabajos de impresión,
/// ejecuta los ejecutables CLI autónomos (Printing.Cli / Fiscal.Cli) en procesos separados
/// y reporta los resultados al backend.
/// </summary>
public class PrintBridgeWorker : BackgroundService
{
    private readonly ILogger<PrintBridgeWorker> _logger;
    private readonly IConfiguration _configuration;
    private HubConnection? _hubConnection;

    public PrintBridgeWorker(ILogger<PrintBridgeWorker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string hubUrl = _configuration.GetValue<string>("BridgeConfig:HubUrl") ?? "http://localhost:5000/hubs/print";
        Guid sucursalId = Guid.Parse(_configuration.GetValue<string>("BridgeConfig:SucursalId") ?? Guid.Empty.ToString());
        int terminalNumero = _configuration.GetValue<int>("BridgeConfig:TerminalNumero", 1);

        _logger.LogInformation("Iniciando BaresFamilia.PrintBridge para sucursal {SucursalId}, Terminal {Terminal}...",
            sucursalId, terminalNumero);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // 1. Escuchar trabajos no fiscales
        _hubConnection.On<Guid, string>("ReceivePrintJob", async (jobId, payloadJson) =>
        {
            _logger.LogInformation("Recibido trabajo de impresión no fiscal #{JobId}", jobId);
            await ProcesarTrabajoNoFiscalAsync(jobId, payloadJson, stoppingToken);
        });

        // 2. Escuchar trabajos fiscales
        _hubConnection.On<Guid, string>("ReceiveFiscalJob", async (jobId, payloadJson) =>
        {
            _logger.LogInformation("Recibido trabajo de impresión FISCAL #{JobId}", jobId);
            await ProcesarTrabajoFiscalAsync(jobId, payloadJson, stoppingToken);
        });

        // Conectar al Hub
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Conectando al Hub SignalR en {HubUrl}...", hubUrl);
                await _hubConnection.StartAsync(stoppingToken);
                _logger.LogInformation("Conectado exitosamente al Hub de impresión.");

                await _hubConnection.InvokeAsync("RegisterTerminal", sucursalId, terminalNumero, stoppingToken);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo conectar al Hub SignalR ({Message}). Reintentando en 5s...", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }

        // Mantener activo mientras no se cancele
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ProcesarTrabajoNoFiscalAsync(Guid jobId, string payloadJson, CancellationToken ct)
    {
        try
        {
            string cliPath = ObtenerRutaExecutable("BaresFamilia.Printing.Cli");
            var (success, stdout, stderr) = await EjecutarProcesoCliAsync(cliPath, payloadJson, ct);

            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("ReportPrintResult", jobId, success, stderr, stdout, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar trabajo no fiscal {JobId}", jobId);
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("ReportPrintResult", jobId, false, ex.Message, null, ct);
            }
        }
    }

    private async Task ProcesarTrabajoFiscalAsync(Guid jobId, string payloadJson, CancellationToken ct)
    {
        try
        {
            string cliPath = ObtenerRutaExecutable("BaresFamilia.Fiscal.Cli");
            var (success, stdout, stderr) = await EjecutarProcesoCliAsync(cliPath, payloadJson, ct);

            string? cae = null;
            string? nroComprobante = null;

            if (success && !string.IsNullOrWhiteSpace(stdout))
            {
                try
                {
                    using var doc = JsonDocument.Parse(stdout);
                    if (doc.RootElement.TryGetProperty("Cae", out var caeProp)) cae = caeProp.GetString();
                    if (doc.RootElement.TryGetProperty("NumeroComprobante", out var nroProp)) nroComprobante = nroProp.GetString();
                }
                catch { }
            }

            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("ReportFiscalResult", jobId, success, cae, nroComprobante, stderr, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar trabajo fiscal {JobId}", jobId);
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("ReportFiscalResult", jobId, false, null, null, ex.Message, ct);
            }
        }
    }

    private async Task<(bool success, string stdout, string stderr)> EjecutarProcesoCliAsync(string executablePath, string inputJson, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        await process.StandardInput.WriteAsync(inputJson);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        string stdout = await process.StandardOutput.ReadToEndAsync(ct);
        string stderr = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);
        bool success = process.ExitCode == 0;

        return (success, stdout, stderr);
    }

    private static string ObtenerRutaExecutable(string projectName)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string exeName = OperatingSystem.IsWindows() ? $"{projectName}.exe" : projectName;

        string localPath = Path.Combine(baseDir, exeName);
        if (File.Exists(localPath)) return localPath;

        // Buscar en directorio de desarrollo hermano
        string devPath = Path.Combine(baseDir, "..", "..", "..", "..", projectName, "bin", "Debug", "net9.0", exeName);
        if (File.Exists(devPath)) return devPath;

        return exeName; // Fallback al PATH del sistema
    }
}
