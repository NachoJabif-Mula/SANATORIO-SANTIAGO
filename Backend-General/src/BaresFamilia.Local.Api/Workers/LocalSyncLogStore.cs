using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BaresFamilia.Local.Api.Workers;

/// <summary>
/// Almacén en memoria de los logs de sincronización locales para ser expuestos al frontend.
/// </summary>
public static class LocalSyncLogStore
{
    private static readonly ConcurrentQueue<LocalSyncLog> _logs = new();

    public static void AddLog(string tipo, string mensaje, bool exitoso)
    {
        _logs.Enqueue(new LocalSyncLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Tipo = tipo,
            Mensaje = mensaje,
            Exitoso = exitoso
        });

        // Limitar a los últimos 50 logs para evitar consumo excesivo de memoria
        while (_logs.Count > 50)
        {
            _logs.TryDequeue(out _);
        }
    }

    public static List<LocalSyncLog> GetLogs()
    {
        return _logs.OrderByDescending(l => l.Timestamp).ToList();
    }
}

public class LocalSyncLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Tipo { get; set; } = string.Empty; // "PUSH", "PULL", "CONFIG", "ERROR", "HEARTBEAT"
    public string Mensaje { get; set; } = string.Empty;
    public bool Exitoso { get; set; }
}
