using System.Collections.Concurrent;
using System.Text.Json;
using BaresFamilia.Core.Models.Contratos.Sincronizacion;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.Extensions.Logging;

namespace BaresFamilia.Infrastructure.Sincronizacion;

/// <summary>
/// Monitor en memoria de la actividad de sincronización de las sucursales.
///
/// Antes era una clase estática declarada dentro de SyncController; extraerla
/// permite inyectarla, sustituirla en tests y mantener el estado operativo fuera
/// de la capa de presentación.
/// </summary>
public class MonitorSincronizacion : IMonitorSincronizacion
{
    private const int IntervaloPorDefectoSegundos = 30;
    private const int MaximoRegistrosRecientes = 100;

    /// <summary>
    /// Margen extra sobre dos intervalos: un POS se considera conectado si se lo
    /// vio dentro de esa ventana, tolerando una demora puntual sin marcarlo caído.
    /// </summary>
    private const int MargenDeGraciaSegundos = 10;

    private readonly ConcurrentDictionary<Guid, EstadoSincronizacionSucursal> _estados = new();
    private readonly ConcurrentDictionary<Guid, bool> _sincronizacionesForzadas = new();
    private readonly ConcurrentDictionary<Guid, int> _intervalos = new();
    private readonly ConcurrentQueue<RegistroSincronizacion> _registrosRecientes = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, DateTime>> _dispositivosConectados = new();

    private readonly string _rutaArchivoIntervalos;
    private readonly ILogger<MonitorSincronizacion> _logger;

    public MonitorSincronizacion(ILogger<MonitorSincronizacion> logger)
    {
        _logger = logger;
        _rutaArchivoIntervalos = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_intervals.json");

        CargarIntervalos();
    }

    public void RegistrarPush(Guid sucursalId, string sucursalNombre, TipoPush tipo)
    {
        var estado = GetEstado(sucursalId, sucursalNombre);
        estado.SucursalNombre = sucursalNombre;

        var ahora = DateTime.UtcNow;
        switch (tipo)
        {
            case TipoPush.Comandas: estado.LastPushComandas = ahora; break;
            case TipoPush.Pagos: estado.LastPushPagos = ahora; break;
            case TipoPush.Movimientos: estado.LastPushMovimientos = ahora; break;
            case TipoPush.CierresDiarios: estado.LastPushCierresDiarios = ahora; break;
        }
    }

    public void RegistrarPull(Guid sucursalId, TipoPull tipo)
    {
        var estado = GetEstado(sucursalId, "Sucursal");

        var ahora = DateTime.UtcNow;
        switch (tipo)
        {
            case TipoPull.Config: estado.LastPullConfig = ahora; break;
            case TipoPull.Mesas: estado.LastPullMesas = ahora; break;
            case TipoPull.Roles: estado.LastPullRoles = ahora; break;
            case TipoPull.Usuarios: estado.LastPullUsuarios = ahora; break;
        }
    }

    public EstadoSincronizacionSucursal GetEstado(Guid sucursalId, string sucursalNombre)
        => _estados.GetOrAdd(sucursalId, _ => new EstadoSincronizacionSucursal
        {
            SucursalId = sucursalId,
            SucursalNombre = sucursalNombre
        });

    public void AgregarRegistro(Guid sucursalId, string sucursalNombre, string tipo, string mensaje, bool exitoso)
    {
        _registrosRecientes.Enqueue(new RegistroSincronizacion
        {
            Id = Guid.NewGuid(),
            SucursalId = sucursalId,
            SucursalNombre = sucursalNombre,
            Timestamp = DateTime.UtcNow,
            Tipo = tipo,
            Mensaje = mensaje,
            Exitoso = exitoso
        });

        while (_registrosRecientes.Count > MaximoRegistrosRecientes)
            _registrosRecientes.TryDequeue(out _);
    }

    public IReadOnlyList<RegistroSincronizacion> GetRegistrosRecientes(int cantidad)
        => _registrosRecientes
            .OrderByDescending(r => r.Timestamp)
            .Take(cantidad)
            .ToList();

    public void SolicitarSincronizacionForzada(Guid sucursalId)
    {
        _sincronizacionesForzadas[sucursalId] = true;

        // Asegura que la sucursal figure en el listado de estado del Backoffice
        // aunque todavía no haya sincronizado nunca.
        GetEstado(sucursalId, "Sucursal");
    }

    public bool TieneSincronizacionForzadaPendiente(Guid sucursalId)
        => _sincronizacionesForzadas.TryGetValue(sucursalId, out var pendiente) && pendiente;

    public bool ConsumirSincronizacionForzada(Guid sucursalId)
        => _sincronizacionesForzadas.TryRemove(sucursalId, out var pendiente) && pendiente;

    public int GetIntervaloSegundos(Guid sucursalId)
        => _intervalos.TryGetValue(sucursalId, out var segundos) ? segundos : IntervaloPorDefectoSegundos;

    public void SetIntervaloSegundos(Guid sucursalId, int segundos)
    {
        _intervalos[sucursalId] = segundos;
        GuardarIntervalos();
    }

    public void RegistrarLatidoDeDispositivo(Guid sucursalId, Guid dispositivoId)
    {
        var dispositivos = _dispositivosConectados.GetOrAdd(sucursalId, _ => new ConcurrentDictionary<Guid, DateTime>());
        dispositivos[dispositivoId] = DateTime.UtcNow;
    }

    public int ContarDispositivosConectados(Guid sucursalId)
    {
        if (!_dispositivosConectados.TryGetValue(sucursalId, out var dispositivos))
            return 0;

        var ventana = GetIntervaloSegundos(sucursalId) * 2 + MargenDeGraciaSegundos;
        var limite = DateTime.UtcNow.AddSeconds(-ventana);

        return dispositivos.Values.Count(ultimaSenal => ultimaSenal > limite);
    }

    /// <summary>
    /// Los intervalos son configuración operativa y deben sobrevivir un reinicio de
    /// la Nube, por eso son lo único que se persiste a disco.
    /// </summary>
    private void CargarIntervalos()
    {
        if (!File.Exists(_rutaArchivoIntervalos))
            return;

        try
        {
            var intervalos = JsonSerializer.Deserialize<Dictionary<Guid, int>>(File.ReadAllText(_rutaArchivoIntervalos));
            if (intervalos is null)
                return;

            foreach (var (sucursalId, segundos) in intervalos)
                _intervalos[sucursalId] = segundos;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Un archivo corrupto o ilegible no debe impedir el arranque: se siguen
            // usando los intervalos por defecto.
            _logger.LogWarning(ex, "No se pudieron cargar los intervalos de sincronización desde {Ruta}.", _rutaArchivoIntervalos);
        }
    }

    private void GuardarIntervalos()
    {
        try
        {
            var intervalos = _intervalos.ToDictionary(par => par.Key, par => par.Value);
            File.WriteAllText(_rutaArchivoIntervalos, JsonSerializer.Serialize(intervalos));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "No se pudieron persistir los intervalos de sincronización en {Ruta}.", _rutaArchivoIntervalos);
        }
    }
}
