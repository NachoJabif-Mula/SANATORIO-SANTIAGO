using System.Collections.Concurrent;
using BaresFamilia.Core.Models.Contratos.Sincronizacion;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Local.Api.Workers;

/// <summary>
/// Almacén en memoria de los logs de sincronización locales para ser expuestos al frontend.
/// </summary>
public static class LocalSyncLogStore
{
    private const int MaximoLogsRetenidos = 50;

    private static readonly ConcurrentQueue<RegistroSincronizacionLocal> _logs = new();

    public static void AddLog(string tipo, string mensaje, bool exitoso)
    {
        _logs.Enqueue(new RegistroSincronizacionLocal
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Tipo = tipo,
            Mensaje = mensaje,
            Exitoso = exitoso
        });

        // Se retienen solo los últimos para evitar consumo excesivo de memoria.
        while (_logs.Count > MaximoLogsRetenidos)
        {
            _logs.TryDequeue(out _);
        }
    }

    public static List<RegistroSincronizacionLocal> GetLogs()
        => _logs.OrderByDescending(l => l.Timestamp).ToList();
}
