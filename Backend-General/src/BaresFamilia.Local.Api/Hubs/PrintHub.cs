using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Local.Api.Hubs;

/// <summary>
/// Hub de SignalR para la comunicación bidireccional en tiempo real entre la API backend local
/// y los servicios PrintBridge locales en cada terminal de cobro.
/// </summary>
public class PrintHub : Hub
{
    private readonly LocalContext _context;
    private readonly ILogger<PrintHub> _logger;

    public PrintHub(LocalContext context, ILogger<PrintHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Registra la terminal/bridge local en el grupo de sucursal correspondiente.
    /// </summary>
    public async Task RegisterTerminal(Guid sucursalId, int terminalNumero)
    {
        string groupName = $"Sucursal_{sucursalId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("PrintBridge registrado: ConnId={ConnId}, SucursalId={SucursalId}, Terminal={Terminal}",
            Context.ConnectionId, sucursalId, terminalNumero);
    }

    /// <summary>
    /// Reporta el resultado de un trabajo de impresión no fiscal ejecutado por el CLI.
    /// </summary>
    public async Task ReportPrintResult(Guid jobId, bool success, string? errorMessage, string? resultJson)
    {
        var job = await _context.PrintJobs.FindAsync(jobId);
        if (job != null)
        {
            job.Estado = success ? "Impreso" : "Fallo";
            job.ResultadoJson = resultJson ?? errorMessage;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("PrintJob {JobId} finalizado: Estado={Estado}", jobId, job.Estado);
        }
    }

    /// <summary>
    /// Reporta el resultado de una operación fiscal ejecutada por el CLI fiscal.
    /// </summary>
    public async Task ReportFiscalResult(Guid jobId, bool success, string? cae, string? nroComprobante, string? errorMessage)
    {
        var job = await _context.PrintJobs.FindAsync(jobId);
        if (job != null)
        {
            job.Estado = success ? "Impreso" : "Fallo";
            job.ResultadoJson = success
                ? $"{{\"cae\":\"{cae}\",\"comprobante\":\"{nroComprobante}\"}}"
                : $"{{\"error\":\"{errorMessage}\"}}";
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("FiscalJob {JobId} finalizado: Estado={Estado}, CAE={Cae}", jobId, job.Estado, cae);
        }
    }
}
