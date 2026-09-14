using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BaresFamilia.Local.Api.Hubs;

/// <summary>
/// Hub de SignalR para la comunicación bidireccional en tiempo real entre la API backend local
/// y los servicios PrintBridge locales en cada terminal de cobro.
/// Flujo: PrintHub → IPrintJobService → PrintJobService → IPrintJobRepository → PrintJobRepository.
/// </summary>
public class PrintHub : Hub
{
    private readonly IPrintJobService _printJobService;
    private readonly ILogger<PrintHub> _logger;

    public PrintHub(IPrintJobService printJobService, ILogger<PrintHub> logger)
    {
        _printJobService = printJobService;
        _logger = logger;
    }

    /// <summary>
    /// Registra la terminal/bridge local en el grupo de sucursal correspondiente.
    /// </summary>
    public async Task RegisterTerminal(Guid sucursalId, int terminalNumero)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Sucursal_{sucursalId}");

        _logger.LogInformation("PrintBridge registrado: ConnId={ConnId}, SucursalId={SucursalId}, Terminal={Terminal}",
            Context.ConnectionId, sucursalId, terminalNumero);
    }

    /// <summary>
    /// Reporta el resultado de un trabajo de impresión no fiscal ejecutado por el CLI.
    /// </summary>
    public async Task ReportPrintResult(Guid jobId, bool success, string? errorMessage, string? resultJson)
    {
        if (await _printJobService.RegistrarResultadoImpresionAsync(jobId, success, errorMessage, resultJson))
            _logger.LogInformation("PrintJob {JobId} finalizado. Exitoso={Exitoso}", jobId, success);
    }

    /// <summary>
    /// Reporta el resultado de una operación fiscal ejecutada por el CLI fiscal.
    /// </summary>
    public async Task ReportFiscalResult(Guid jobId, bool success, string? cae, string? nroComprobante, string? errorMessage)
    {
        if (await _printJobService.RegistrarResultadoFiscalAsync(jobId, success, cae, nroComprobante, errorMessage))
            _logger.LogInformation("FiscalJob {JobId} finalizado. Exitoso={Exitoso}, CAE={Cae}", jobId, success, cae);
    }
}
