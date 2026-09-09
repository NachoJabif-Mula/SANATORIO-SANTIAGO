using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BaresFamilia.Local.Api.Workers;

/// <summary>
/// Motor Local-First de sincronización.
/// BackgroundService con intervalo configurable, consulta registros
/// con SyncEstado == Pendiente y los envía a la API Nube vía HTTP POST
/// con el JWT M2M en los headers.
/// </summary>
public class SincronizacionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SincronizacionWorker> _logger;

    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
    private bool _isSyncing = false;
    public bool IsSyncing => _isSyncing;

    // Intervalo configurable — se lee desde appsettings o se puede actualizar en runtime desde la nube
    private TimeSpan _intervalo;
    private static readonly string IntervalFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_interval.json");

    public SincronizacionWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SincronizacionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;

        // Leer intervalo: primero de archivo local (configurado desde nube), luego de appsettings, default 30s
        _intervalo = TimeSpan.FromSeconds(LeerIntervaloConfigurado());
    }

    private int LeerIntervaloConfigurado()
    {
        // 1. Intentar leer de archivo local (prioridad: fue configurado remotamente)
        if (System.IO.File.Exists(IntervalFilePath))
        {
            try
            {
                var json = System.IO.File.ReadAllText(IntervalFilePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("intervalSeconds", out var prop))
                {
                    var val = prop.GetInt32();
                    if (val >= 5 && val <= 300) return val;
                }
            }
            catch { }
        }

        // 2. Leer de appsettings.json
        var fromConfig = _configuration.GetValue<int>("NubeApi:SyncIntervalSeconds", 30);
        return fromConfig >= 5 ? fromConfig : 30;
    }

    /// <summary>
    /// Actualiza el intervalo de sincronización y lo persiste en disco.
    /// </summary>
    public void ActualizarIntervalo(int segundos)
    {
        if (segundos < 5) segundos = 5;
        if (segundos > 300) segundos = 300;
        _intervalo = TimeSpan.FromSeconds(segundos);

        try
        {
            var json = JsonSerializer.Serialize(new { intervalSeconds = segundos });
            System.IO.File.WriteAllText(IntervalFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("No se pudo persistir el intervalo de sync: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Retorna el intervalo actual en segundos.
    /// </summary>
    public int ObtenerIntervaloActual() => (int)_intervalo.TotalSeconds;

    public Task<bool> TriggerManualSyncAsync(CancellationToken ct)
    {
        if (_isSyncing)
        {
            return Task.FromResult(false);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await TriggerSyncInternalAsync(forcePull: true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ejecución de sync manual.");
            }
        });

        return Task.FromResult(true);
    }

    private async Task TriggerSyncInternalAsync(bool forcePull, CancellationToken ct)
    {
        if (!await _syncSemaphore.WaitAsync(0, ct))
        {
            _logger.LogDebug("Sincronización ya ejecutándose. Se ignora esta solicitud.");
            return;
        }

        _isSyncing = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LocalContext>();
            await ProcesarSincronizacionConOpcionesAsync(context, forcePull, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico durante la sincronización.");
        }
        finally
        {
            _isSyncing = false;
            _syncSemaphore.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "══════════════════════════════════════════════════════\n" +
            "  🚀 SincronizacionWorker INICIADO\n" +
            "  ⏱️  Intervalo: {Intervalo}s\n" +
            "  📁 Config: {ConfigPath}\n" +
            "══════════════════════════════════════════════════════",
            _intervalo.TotalSeconds, IntervalFilePath);

        var lastSyncTime = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            bool timeToSync = (now - lastSyncTime) >= _intervalo;
            bool forceRequested = false;

            if (!timeToSync)
            {
                forceRequested = await CheckForceSyncRequestedAsync(stoppingToken);
            }

            if (timeToSync || forceRequested)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await TriggerSyncInternalAsync(forcePull: forceRequested, stoppingToken);
                    sw.Stop();
                    lastSyncTime = DateTime.UtcNow;
                    _logger.LogDebug("⏱️ Ciclo de sync completado en {Ms}ms. Próximo en {Intervalo}s.", sw.ElapsedMilliseconds, _intervalo.TotalSeconds);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogError(ex, "❌ ERROR CRÍTICO en ciclo de sincronización ({Ms}ms). Reintentará en {Intervalo}s.", sw.ElapsedMilliseconds, _intervalo.TotalSeconds);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("🛑 SincronizacionWorker DETENIDO.");
    }

    private async Task<bool> CheckForceSyncRequestedAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LocalContext>();
        
        string? jwtToken = null;
        Guid? sucursalId = null;
        Guid? dispositivoId = null;

        try
        {
            var dbAct = await context.DispositivosActivacion.FirstOrDefaultAsync(d => d.Activado, ct);
            if (dbAct != null && !string.IsNullOrWhiteSpace(dbAct.TokenHash))
            {
                var data = JsonSerializer.Deserialize<SyncActivationData>(dbAct.TokenHash, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data != null && !string.IsNullOrWhiteSpace(data.Token))
                {
                    jwtToken = data.Token;
                    sucursalId = data.SucursalId;
                    dispositivoId = data.DispositivoId;
                }
            }
        }
        catch { }

        if (string.IsNullOrEmpty(jwtToken) || sucursalId == null)
        {
            return false;
        }

        var baseUrl = _configuration["NubeApi:BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return false;

        try
        {
            var client = _httpClientFactory.CreateClient("NubeApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            var checkUrl = $"{baseUrl.TrimEnd('/')}/api/sync/check-force/{sucursalId}?dispositivoId={dispositivoId}";
            var checkResponse = await client.GetAsync(checkUrl, ct);
            if (checkResponse.IsSuccessStatusCode)
            {
                var checkJson = await checkResponse.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(checkJson);
                
                if (doc.RootElement.TryGetProperty("syncIntervalSeconds", out var intervalProp))
                {
                    var remoteInterval = intervalProp.GetInt32();
                    if (remoteInterval != ObtenerIntervaloActual() && remoteInterval >= 5)
                    {
                        _logger.LogInformation("⚙️ Intervalo de sync actualizado remotamente: {Old}s → {New}s", ObtenerIntervaloActual(), remoteInterval);
                        ActualizarIntervalo(remoteInterval);
                    }
                }

                if (doc.RootElement.TryGetProperty("forceSync", out var forceProp) && forceProp.GetBoolean())
                {
                    _logger.LogInformation("🔄 SYNC FORZADA detectada en chequeo rápido.");
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    private async Task ProcesarSincronizacionConOpcionesAsync(LocalContext context, bool forcePull, CancellationToken ct)
    {
        // ── Obtener datos de activación desde la base de datos local ──
        string? jwtToken = null;
        Guid? sucursalId = null;
        Guid? dispositivoId = null;
        string sucursalNombre = "Sucursal Central";

        try
        {
            var dbAct = await context.DispositivosActivacion.FirstOrDefaultAsync(d => d.Activado, ct);
            if (dbAct != null && !string.IsNullOrWhiteSpace(dbAct.TokenHash))
            {
                var data = JsonSerializer.Deserialize<SyncActivationData>(dbAct.TokenHash, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data != null && !string.IsNullOrWhiteSpace(data.Token))
                {
                    jwtToken = data.Token;
                    sucursalId = data.SucursalId;
                    dispositivoId = data.DispositivoId;
                    sucursalNombre = data.SucursalNombre;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("No se pudo leer la activación desde la base de datos local: {Message}", ex.Message);
        }

        if (string.IsNullOrEmpty(jwtToken) || sucursalId == null)
        {
            _logger.LogDebug("💤 Sync omitida: dispositivo no activado.");
            return;
        }

        var baseUrl = _configuration["NubeApi:BaseUrl"];
        bool forceFromNube = false;

        try
        {
            // ── Check: Sincronización forzada desde la Nube ──
            if (!string.IsNullOrEmpty(baseUrl))
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("NubeApi");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
                    var checkUrl = $"{baseUrl.TrimEnd('/')}/api/sync/check-force/{sucursalId}?dispositivoId={dispositivoId}";
                    var checkResponse = await client.GetAsync(checkUrl, ct);
                    if (checkResponse.IsSuccessStatusCode)
                    {
                        var checkJson = await checkResponse.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(checkJson);
                        if (doc.RootElement.TryGetProperty("forceSync", out var forceProp) && forceProp.GetBoolean())
                        {
                            forceFromNube = true;
                            _logger.LogInformation("🔄 SYNC FORZADA solicitada desde Backoffice. Ejecutando inmediatamente...");
                            LocalSyncLogStore.AddLog("CONFIG", "Sincronización forzada solicitada desde Backoffice.", true);
                        }

                        // Leer intervalo remoto si viene en la respuesta
                        if (doc.RootElement.TryGetProperty("syncIntervalSeconds", out var intervalProp))
                        {
                            var remoteInterval = intervalProp.GetInt32();
                            if (remoteInterval != ObtenerIntervaloActual() && remoteInterval >= 5)
                            {
                                _logger.LogInformation("⚙️ Intervalo de sync actualizado remotamente: {Old}s → {New}s", ObtenerIntervaloActual(), remoteInterval);
                                ActualizarIntervalo(remoteInterval);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("No se pudo verificar sync forzada: {Message}", ex.Message);
                }
            }

            // ── Check: Dispositivo revocado remotamente ──
            if (!string.IsNullOrEmpty(baseUrl) && dispositivoId.HasValue)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("NubeApi");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
                    var statusUrl = $"{baseUrl.TrimEnd('/')}/api/dispositivos/{dispositivoId.Value}/estado-activacion";
                    var statusResponse = await client.GetAsync(statusUrl, ct);
                    if (statusResponse.IsSuccessStatusCode)
                    {
                        var statusJson = await statusResponse.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(statusJson);
                        if (doc.RootElement.TryGetProperty("activo", out var activoProp) && !activoProp.GetBoolean())
                        {
                            _logger.LogWarning("🚫 DISPOSITIVO REVOCADO remotamente desde Backoffice. Eliminando activación local...");
                            LocalSyncLogStore.AddLog("ERROR", "🚫 DISPOSITIVO REVOCADO remotamente desde Backoffice.", false);
                            
                            // Limpiar base de datos local
                            try
                            {
                                var dbActs = await context.DispositivosActivacion.ToListAsync(ct);
                                context.DispositivosActivacion.RemoveRange(dbActs);
                                await context.SaveChangesAsync(ct);
                            }
                            catch (Exception dbEx)
                            {
                                _logger.LogWarning("No se pudo limpiar la tabla de activación en la base de datos local: {Message}", dbEx.Message);
                            }

                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("No se pudo verificar estado de activación: {Message}", ex.Message);
                }
            }

            // ── Asegurar sucursal local ──
            var sucursalExiste = await context.Sucursales.AnyAsync(s => s.Id == sucursalId.Value, ct);
            if (!sucursalExiste)
            {
                var nuevaSucursal = new Sucursal
                {
                    Id = sucursalId.Value,
                    Nombre = sucursalNombre,
                    Direccion = "Local",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Sucursales.Add(nuevaSucursal);
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("✅ Sucursal '{Nombre}' creada localmente (integridad referencial).", sucursalNombre);
            }
            else
            {
                var sObj = await context.Sucursales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sucursalId.Value, ct);
                if (sObj != null)
                {
                    sucursalNombre = sObj.Nombre;
                }
            }

            // ══════════════════════════════════
            // 1. PUSH — Enviar pendientes a Nube
            // ══════════════════════════════════
            // Comandas a subir: cobradas o anuladas (las que la Nube necesita como
            // "cerradas"), MÁS cualquier comanda que aún esté Abierta pero tenga algún
            // ítem anulado individualmente — si no la incluimos aquí, esa anulación
            // (que vive como estado en el propio ComandaItem) nunca llega a la Nube.
            var comandasPendientes = await context.Comandas
                .Where(c => c.IsActive && c.SyncEstado == SyncEstado.Pendiente &&
                    (c.Estado == ComandaEstado.Cobrada || c.Estado == ComandaEstado.Anulada
                     || c.Items.Any(i => i.Cancelado)))
                .Include(c => c.Items)
                .ToListAsync(ct);

            var pagosPendientes = await context.Pagos
                .Include(p => p.Comanda)
                .Where(p => p.SyncEstado == SyncEstado.Pendiente && p.Comanda.Estado == ComandaEstado.Cobrada && p.IsActive)
                .ToListAsync(ct);

            var movimientosPendientes = await context.MovimientosCaja
                .Where(m => m.SyncEstado == SyncEstado.Pendiente && m.IsActive)
                .ToListAsync(ct);

            var cierresPendientes = await context.CierresDiarios
                .Where(c => c.SyncEstado == SyncEstado.Pendiente && c.IsActive)
                .ToListAsync(ct);

            var clientesPendientes = await context.Clientes
                .Where(c => c.SyncEstado == SyncEstado.Pendiente && c.IsActive)
                .ToListAsync(ct);

            var movimientosCtaCtePendientes = await context.Set<MovimientoCuentaCorriente>()
                .Include(m => m.CuentaCorriente)
                .Where(m => m.SyncEstado == SyncEstado.Pendiente && m.IsActive)
                .ToListAsync(ct);

            var turnosCajaPendientes = await context.TurnosCaja
                .Where(t => t.SyncEstado == SyncEstado.Pendiente && t.IsActive)
                .ToListAsync(ct);

            // La Caja real de la sucursal nunca se había sincronizado: la Nube fabricaba una
            // "Caja Sincronizada Principal" propia con otro Id, por lo que el TurnoCaja real
            // nunca podía enlazar por FK. Sincronizarla primero resuelve eso de raíz.
            var cajasPendientes = await context.Cajas
                .Where(c => c.SyncEstado == SyncEstado.Pendiente && c.IsActive)
                .ToListAsync(ct);

            var totalPendientes = comandasPendientes.Count + pagosPendientes.Count + movimientosPendientes.Count + cierresPendientes.Count + clientesPendientes.Count + movimientosCtaCtePendientes.Count + turnosCajaPendientes.Count + cajasPendientes.Count;

            if (totalPendientes > 0)
            {
                _logger.LogInformation(
                    "📤 [PUSH] Detectados registros locales pendientes de sincronizar:\n" +
                    "  ▪️ Comandas: {Comandas}\n" +
                    "  ▪️ Pagos: {Pagos}\n" +
                    "  ▪️ Movimientos de Caja: {Movimientos}\n" +
                    "  ▪️ Cierres Diarios: {CierresDiarios}\n" +
                    "  ▪️ Clientes: {Clientes}\n" +
                    "  ▪️ Movimientos Cta. Cte.: {MovimientosCtaCte}\n" +
                    "  ➡️ Total a enviar: {Total}",
                    comandasPendientes.Count, pagosPendientes.Count, movimientosPendientes.Count, cierresPendientes.Count, clientesPendientes.Count, movimientosCtaCtePendientes.Count, totalPendientes);

                LocalSyncLogStore.AddLog("PUSH", $"📤 PUSH: Subiendo {totalPendientes} registros locales pendientes (Comandas: {comandasPendientes.Count}, Pagos: {pagosPendientes.Count}, Egresos: {movimientosPendientes.Count}, Cierres: {cierresPendientes.Count}, Clientes: {clientesPendientes.Count}, Mov. Cta. Cte.: {movimientosCtaCtePendientes.Count}).", true);

                var payload = new SyncPayload
                {
                    Timestamp = DateTime.UtcNow,
                    Comandas = comandasPendientes.Select(c => new SyncComanda
                    {
                        Id = c.Id,
                        TipoVentaId = c.TipoVentaId,
                        MesaId = c.MesaId,
                        UsuarioId = c.UsuarioId,
                        Estado = c.Estado.ToString(),
                        Subtotal = c.Subtotal,
                        Descuento = c.Descuento,
                        Total = c.Total,
                        // Solo se pushean comandas ya Cobradas (ver query de comandasPendientes), que
                        // siempre tienen FechaContable/Turno asignados al cerrarse; el fallback es defensivo.
                        FechaContable = c.FechaContable ?? DateTime.UtcNow,
                        Turno = c.Turno ?? "AM",
                        CreatedAt = c.CreatedAt,
                        Items = c.Items.Select(i => new SyncComandaItem
                        {
                            Id = i.Id,
                            ProductoId = i.ProductoId,
                            Cantidad = i.Cantidad,
                            PrecioUnitario = i.PrecioUnitario,
                            Notas = i.Notas,
                            Cancelado = i.Cancelado,
                            MotivoAnulacion = i.MotivoAnulacion,
                            AnuladoPorUsuarioId = i.AnuladoPorUsuarioId,
                            FechaAnulacion = i.FechaAnulacion
                        }).ToList()
                    }).ToList(),
                    Pagos = pagosPendientes.Select(p => new SyncPago
                    {
                        Id = p.Id,
                        ComandaId = p.ComandaId,
                        TurnoCajaId = p.TurnoCajaId,
                        MetodoPagoId = p.MetodoPagoId,
                        Monto = p.Monto,
                        CreatedAt = p.CreatedAt
                    }).ToList(),
                    Movimientos = movimientosPendientes.Select(m => new SyncMovimiento
                    {
                        Id = m.Id,
                        TurnoCajaId = m.TurnoCajaId,
                        Tipo = m.Tipo.ToString(),
                        Monto = m.Monto,
                        Concepto = m.Concepto,
                        CreatedAt = m.CreatedAt
                    }).ToList(),
                    CierresDiarios = cierresPendientes.Select(c => new SyncCierreDiario
                    {
                        Id = c.Id,
                        CajaId = c.CajaId,
                        Fecha = c.Fecha,
                        UsuarioCierreId = c.UsuarioCierreId,
                        TotalVentas = c.TotalVentas,
                        TotalEgresos = c.TotalEgresos,
                        TotalNeto = c.TotalNeto,
                        ResumenJson = c.ResumenJson,
                        Observaciones = c.Observaciones,
                        CreatedAt = c.CreatedAt
                    }).ToList(),
                    // Los clientes altados en el POS son locales: a la Nube solo le llega el nombre
                    // (necesario para el reporte de Cuentas Corrientes). Teléfono/Email/Límite de
                    // Crédito quedan exclusivamente en la sucursal que los cargó.
                    Clientes = clientesPendientes.Select(c => new SyncCliente
                    {
                        Id = c.Id,
                        Nombre = c.Nombre,
                        Apellido = c.Apellido,
                        CreatedAt = c.CreatedAt
                    }).ToList(),
                    MovimientosCuentaCorriente = movimientosCtaCtePendientes.Select(m => new SyncMovimientoCuentaCorriente
                    {
                        Id = m.Id,
                        ClienteId = m.CuentaCorriente.ClienteId,
                        ComandaId = m.ComandaId,
                        Tipo = m.Tipo.ToString(),
                        Monto = m.Monto,
                        Detalle = m.Detalle,
                        CreatedAt = m.CreatedAt
                    }).ToList(),
                    TurnosCaja = turnosCajaPendientes.Select(t => new SyncTurnoCaja
                    {
                        Id = t.Id,
                        CajaId = t.CajaId,
                        UsuarioId = t.UsuarioId,
                        FechaApertura = t.FechaApertura,
                        FechaCierre = t.FechaCierre,
                        FechaContable = t.FechaContable,
                        Turno = t.Turno,
                        FondoInicial = t.FondoInicial,
                        DiferenciaArqueo = t.DiferenciaArqueo
                    }).ToList(),
                    Cajas = cajasPendientes.Select(c => new SyncCaja
                    {
                        Id = c.Id,
                        Nombre = c.Nombre,
                        TipoCaja = c.TipoCaja.ToString()
                    }).ToList()
                };

                var (exitoso, pushErrorDetail) = await EnviarANubeAsync(payload, jwtToken, ct);

                if (exitoso)
                {
                    foreach (var c in comandasPendientes) { c.SyncEstado = SyncEstado.Sincronizado; c.UpdatedAt = DateTime.UtcNow; }
                    foreach (var p in pagosPendientes) { p.SyncEstado = SyncEstado.Sincronizado; p.UpdatedAt = DateTime.UtcNow; }
                    foreach (var m in movimientosPendientes) { m.SyncEstado = SyncEstado.Sincronizado; m.UpdatedAt = DateTime.UtcNow; }
                    foreach (var c in cierresPendientes) { c.SyncEstado = SyncEstado.Sincronizado; c.UpdatedAt = DateTime.UtcNow; }
                    foreach (var c in clientesPendientes) { c.SyncEstado = SyncEstado.Sincronizado; c.UpdatedAt = DateTime.UtcNow; }
                    foreach (var m in movimientosCtaCtePendientes) { m.SyncEstado = SyncEstado.Sincronizado; m.UpdatedAt = DateTime.UtcNow; }
                    foreach (var t in turnosCajaPendientes) { t.SyncEstado = SyncEstado.Sincronizado; t.UpdatedAt = DateTime.UtcNow; }
                    foreach (var c in cajasPendientes) { c.SyncEstado = SyncEstado.Sincronizado; c.UpdatedAt = DateTime.UtcNow; }

                    await context.SaveChangesAsync(ct);
                    _logger.LogInformation("📤 [PUSH] ✅ Sincronización exitosa. Los {Total} registros locales ahora están en la Nube.", totalPendientes);
                    LocalSyncLogStore.AddLog("PUSH", $"📤 PUSH: ✅ Sincronizados {totalPendientes} registros con éxito.", true);
                    await EnviarLogANubeAsync(sucursalId.Value, sucursalNombre, "PUSH", $"📤 PUSH ✅ Sincronizados {totalPendientes} registros locales con éxito (Comandas: {comandasPendientes.Count}, Pagos: {pagosPendientes.Count}, Movs: {movimientosPendientes.Count}, Cierres: {cierresPendientes.Count}).", true, jwtToken, ct);
                }
                else
                {
                    _logger.LogWarning("📤 [PUSH] ❌ Falló el envío de {Total} registros. Se reintentará en {Intervalo}s.", totalPendientes, _intervalo.TotalSeconds);
                    LocalSyncLogStore.AddLog("PUSH", $"📤 PUSH: ❌ Falló el envío de {totalPendientes} registros locales. Detalle: {pushErrorDetail}", false);
                    await EnviarLogANubeAsync(sucursalId.Value, sucursalNombre, "PUSH", $"📤 PUSH ❌ Falló la sincronización de {totalPendientes} registros locales. Detalle: {pushErrorDetail}", false, jwtToken, ct);
                }
            }
            else
            {
                _logger.LogDebug("📤 PUSH — Sin pendientes.");
                if (forcePull)
                {
                    LocalSyncLogStore.AddLog("PUSH", "📤 PUSH: Sin transacciones pendientes para subir.", true);
                }
            }

            // ── Asegurar que exista un reporte de latido (polling vacío) cada tanto en Backoffice si no hay PUSH ──
            if (totalPendientes == 0)
            {
                await EnviarLogANubeAsync(sucursalId.Value, sucursalNombre, "HEARTBEAT", $"💓 Sincronización activa (Intervalo: {ObtenerIntervaloActual()}s). Sin datos nuevos para enviar.", true, jwtToken, ct);
            }

            // ── Clientes: se sincroniza en cada ciclo (no solo en pull forzado), para que las altas
            // hechas desde el Backoffice aparezcan en el POS sin depender de una sync manual ──
            await SincronizarClientesAsync(context, jwtToken, ct);

            // ══════════════════════════════════
            // 2. PULL — Descargar catálogos de Nube
            // ══════════════════════════════════
            bool noHayUsuarios = !await context.Usuarios.AnyAsync(ct);
            if (forcePull || forceFromNube || noHayUsuarios)
            {
                LocalSyncLogStore.AddLog("PULL", "📥 PULL: Iniciando descarga de catálogos desde la Nube...", true);
                await EjecutarPullSincronizacionAsync(context, jwtToken, sucursalId.Value, sucursalNombre, ct);
            }
            else
            {
                _logger.LogDebug("📥 PULL omitido (solo ejecución manual autorizada).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ERROR general en ProcesarSincronizacionConOpcionesAsync: {Message}", ex.Message);
            LocalSyncLogStore.AddLog("ERROR", $"❌ Error general de sincronización: {ex.Message}", false);
            await EnviarLogANubeAsync(sucursalId.Value, sucursalNombre, "ERROR", $"❌ Error crítico en ciclo de sync: {ex.Message}", false, jwtToken, ct);
            throw;
        }
    }

    private async Task EjecutarPullSincronizacionAsync(LocalContext context, string jwtToken, Guid sucursalId, string sucursalNombre, CancellationToken ct)
    {
        var baseUrl = _configuration["NubeApi:BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return;

        LocalSyncLogStore.AddLog("PULL", "📥 PULL: Descargando catálogos de productos, precios, planos y mesas...", true);
        var sw = Stopwatch.StartNew();
        int configCount = 0, mesaCount = 0, rolCount = 0, usuarioCount = 0;
        int configNew = 0, mesaNew = 0, rolNew = 0, usuarioNew = 0;
        int tipoVentaCount = 0, categoriaCount = 0, productoCount = 0, precioCount = 0;
        int tipoVentaNew = 0, categoriaNew = 0, productoNew = 0, precioNew = 0;
        int metodoPagoCount = 0, metodoPagoNew = 0;
        var errores = new List<string>();

        try
        {
            var client = _httpClientFactory.CreateClient("NubeApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

            // A. PULL Configuraciones POS
            try
            {
                var configUrl = $"{baseUrl.TrimEnd('/')}/api/ConfiguracionPos/sucursal/{sucursalId}?includeInactive=true";
                var configResponse = await client.GetAsync(configUrl, ct);
                if (configResponse.IsSuccessStatusCode)
                {
                    var json = await configResponse.Content.ReadAsStringAsync(ct);
                    var pulledConfigs = JsonSerializer.Deserialize<List<SyncConfigPosDto>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    if (pulledConfigs != null)
                    {
                        configCount = pulledConfigs.Count;
                        var existingConfigs = await context.ConfiguracionesPos.ToListAsync(ct);
                        var existingMap = existingConfigs.ToDictionary(c => c.Id);

                        foreach (var pulled in pulledConfigs)
                        {
                            if (existingMap.TryGetValue(pulled.Id, out var existing))
                            {
                                existing.Nombre = pulled.Nombre;
                                existing.ConfiguracionJson = pulled.ConfiguracionJson;
                                existing.IsActive = pulled.IsActive;
                                existing.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                configNew++;
                                var newConfig = new ConfiguracionPos
                                {
                                    Id = pulled.Id,
                                    SucursalId = sucursalId,
                                    Nombre = pulled.Nombre,
                                    ConfiguracionJson = pulled.ConfiguracionJson,
                                    IsActive = pulled.IsActive,
                                    CreatedAt = pulled.CreatedAt,
                                    UpdatedAt = pulled.UpdatedAt
                                };
                                context.ConfiguracionesPos.Add(newConfig);
                            }
                        }
                    }
                }
                else
                {
                    errores.Add($"ConfigPOS: HTTP {(int)configResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                errores.Add($"ConfigPOS: {ex.Message}");
            }

            // B. PULL Mesas
            try
            {
                var mesaUrl = $"{baseUrl.TrimEnd('/')}/api/mesa/sucursal/{sucursalId}?includeInactive=true";
                var mesaResponse = await client.GetAsync(mesaUrl, ct);
                if (mesaResponse.IsSuccessStatusCode)
                {
                    var json = await mesaResponse.Content.ReadAsStringAsync(ct);
                    var pulledMesas = JsonSerializer.Deserialize<List<SyncMesaDto>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    if (pulledMesas != null)
                    {
                        mesaCount = pulledMesas.Count;
                        var existingMesas = await context.Mesas.ToListAsync(ct);
                        var existingMap = existingMesas.ToDictionary(m => m.Id);

                        foreach (var pulled in pulledMesas)
                        {
                            if (existingMap.TryGetValue(pulled.Id, out var existing))
                            {
                                existing.Etiqueta = pulled.Etiqueta;
                                existing.Capacidad = pulled.Capacidad;
                                existing.PosX = pulled.PosX;
                                existing.PosY = pulled.PosY;
                                existing.Forma = pulled.Forma;
                                existing.IsActive = pulled.IsActive;
                                existing.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                mesaNew++;
                                var newMesa = new Mesa
                                {
                                    Id = pulled.Id,
                                    SucursalId = sucursalId,
                                    Etiqueta = pulled.Etiqueta,
                                    Capacidad = pulled.Capacidad,
                                    PosX = pulled.PosX,
                                    PosY = pulled.PosY,
                                    Forma = pulled.Forma,
                                    IsActive = pulled.IsActive,
                                    CreatedAt = pulled.CreatedAt,
                                    UpdatedAt = pulled.UpdatedAt
                                };
                                context.Mesas.Add(newMesa);
                            }
                        }
                    }
                }
                else
                {
                    errores.Add($"Mesas: HTTP {(int)mesaResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                errores.Add($"Mesas: {ex.Message}");
            }

            // C. PULL Roles
            try
            {
                var rolUrl = $"{baseUrl.TrimEnd('/')}/api/rol";
                var rolResponse = await client.GetAsync(rolUrl, ct);
                if (rolResponse.IsSuccessStatusCode)
                {
                    var json = await rolResponse.Content.ReadAsStringAsync(ct);
                    var pulledRoles = JsonSerializer.Deserialize<List<SyncRolDto>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    if (pulledRoles != null)
                    {
                        rolCount = pulledRoles.Count;
                        var existingRoles = await context.Roles.ToListAsync(ct);
                        var existingMap = existingRoles.ToDictionary(r => r.Id);

                        foreach (var pulled in pulledRoles)
                        {
                            if (existingMap.TryGetValue(pulled.Id, out var existing))
                            {
                                existing.Nombre = pulled.Nombre;
                                existing.Permisos = pulled.Permisos ?? [];
                                existing.IsActive = pulled.IsActive;
                                existing.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                rolNew++;
                                var newRol = new Rol
                                {
                                    Id = pulled.Id,
                                    Nombre = pulled.Nombre,
                                    Permisos = pulled.Permisos ?? [],
                                    IsActive = pulled.IsActive,
                                    CreatedAt = pulled.CreatedAt,
                                    UpdatedAt = pulled.UpdatedAt
                                };
                                context.Roles.Add(newRol);
                            }
                        }
                    }
                }
                else
                {
                    errores.Add($"Roles: HTTP {(int)rolResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                errores.Add($"Roles: {ex.Message}");
            }

            // D. PULL Usuarios (Empleados)
            try
            {
                var usuarioUrl = $"{baseUrl.TrimEnd('/')}/api/empleado/por-sucursal/{sucursalId}";
                var usuarioResponse = await client.GetAsync(usuarioUrl, ct);
                if (usuarioResponse.IsSuccessStatusCode)
                {
                    var json = await usuarioResponse.Content.ReadAsStringAsync(ct);
                    var pulledUsuarios = JsonSerializer.Deserialize<List<SyncUsuarioDto>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    if (pulledUsuarios != null)
                    {
                        usuarioCount = pulledUsuarios.Count;
                        var existingUsuarios = await context.Usuarios.ToListAsync(ct);
                        var existingMap = existingUsuarios.ToDictionary(u => u.Id);

                        foreach (var pulled in pulledUsuarios)
                        {
                            if (existingMap.TryGetValue(pulled.Id, out var existing))
                            {
                                existing.RolId = pulled.RolId;
                                existing.SucursalId = pulled.SucursalId;
                                existing.Nombre = pulled.Nombre;
                                existing.Email = pulled.Email;
                                existing.PinAcceso = pulled.PinAcceso;
                                existing.IsActive = pulled.IsActive;
                                existing.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                usuarioNew++;
                                var newUsuario = new Usuario
                                {
                                    Id = pulled.Id,
                                    RolId = pulled.RolId,
                                    SucursalId = pulled.SucursalId,
                                    Nombre = pulled.Nombre,
                                    Email = pulled.Email,
                                    PinAcceso = pulled.PinAcceso,
                                    PasswordHash = "", 
                                    IsActive = pulled.IsActive,
                                    CreatedAt = pulled.CreatedAt,
                                    UpdatedAt = pulled.UpdatedAt
                                };
                                context.Usuarios.Add(newUsuario);
                            }
                        }
                    }
                }
                else
                {
                    errores.Add($"Empleados: HTTP {(int)usuarioResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                errores.Add($"Empleados: {ex.Message}");
            }

            // E. PULL TiposVenta
            try
            {
                var tvUrl = $"{baseUrl.TrimEnd('/')}/api/tipoventa?includeInactive=true";
                var tvResponse = await client.GetAsync(tvUrl, ct);
                if (tvResponse.IsSuccessStatusCode)
                {
                    var json = await tvResponse.Content.ReadAsStringAsync(ct);
                    var pulledTvs = JsonSerializer.Deserialize<List<SyncTipoVentaDto>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    if (pulledTvs != null)
                    {
                        tipoVentaCount = pulledTvs.Count;
                        var existingTvs = await context.TiposVenta.ToListAsync(ct);
                        var existingMap = existingTvs.ToDictionary(t => t.Id);

                        foreach (var pulled in pulledTvs)
                        {
                            if (existingMap.TryGetValue(pulled.Id, out var existing))
                            {
                                existing.Nombre = pulled.Nombre;
                                existing.AplicaRecargo = pulled.AplicaRecargo;
                                existing.IsActive = pulled.IsActive;
                                existing.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                tipoVentaNew++;
                                var newTv = new TipoVenta
                                {
                                    Id = pulled.Id,
                                    Nombre = pulled.Nombre,
                                    AplicaRecargo = pulled.AplicaRecargo,
                                    IsActive = pulled.IsActive,
                                    CreatedAt = pulled.CreatedAt,
                                    UpdatedAt = pulled.UpdatedAt
                                };
                                context.TiposVenta.Add(newTv);
                            }
                        }

                        // Deactivar locales que no estén en la lista del pull (sync de borrado lógico)
                        var pulledIds = pulledTvs.Select(t => t.Id).ToHashSet();
                        foreach (var localTv in existingTvs)
                        {
                            if (!pulledIds.Contains(localTv.Id) && localTv.IsActive)
                            {
                                localTv.IsActive = false;
                                localTv.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }
                else
                {
                    errores.Add($"TiposVenta: HTTP {(int)tvResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                errores.Add($"TiposVenta: {ex.Message}");
            }

            // F. PULL Categorias
            try
            {
                var catUrl = $"{baseUrl.TrimEnd('/')}/api/categoria?includeInactive=true";
                var catResponse = await client.GetAsync(catUrl, ct);
                if (catResponse.IsSuccessStatusCode)
                {
                    var json = await catResponse.Content.ReadAsStringAsync(ct);
                    var pulledCats = JsonSerializer.Deserialize<List<SyncCategoriaDto>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    if (pulledCats != null)
                    {
                        categoriaCount = pulledCats.Count;
                        var existingCats = await context.Categorias.ToListAsync(ct);
                        var existingMap = existingCats.ToDictionary(c => c.Id);

                        foreach (var pulled in pulledCats)
                        {
                            if (existingMap.TryGetValue(pulled.Id, out var existing))
                            {
                                existing.Nombre = pulled.Nombre;
                                existing.OrdenVisual = pulled.OrdenVisual;
                                existing.IsActive = pulled.IsActive;
                                existing.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                categoriaNew++;
                                var newCat = new Categoria
                                {
                                    Id = pulled.Id,
                                    Nombre = pulled.Nombre,
                                    OrdenVisual = pulled.OrdenVisual,
                                    IsActive = pulled.IsActive,
                                    CreatedAt = pulled.CreatedAt,
                                    UpdatedAt = pulled.UpdatedAt
                                };
                                context.Categorias.Add(newCat);
                            }
                        }

                        // Deactivar locales que no estén en la lista del pull (sync de borrado lógico)
                        var pulledIds = pulledCats.Select(c => c.Id).ToHashSet();
                        foreach (var localCat in existingCats)
                        {
                            if (!pulledIds.Contains(localCat.Id) && localCat.IsActive)
                            {
                                localCat.IsActive = false;
                                localCat.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }
                else
                {
                    errores.Add($"Categorias: HTTP {(int)catResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                errores.Add($"Categorias: {ex.Message}");
            }

            // G. PULL Productos
            try
            {
                var prodUrl = $"{baseUrl.TrimEnd('/')}/api/producto?includeInactive=true";
                var prodResponse = await client.GetAsync(prodUrl, ct);
                if (prodResponse.IsSuccessStatusCode)
                {
                    var json = await prodResponse.Content.ReadAsStringAsync(ct);
                    var pulledProds = JsonSerializer.Deserialize<List<SyncProductoDto>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    if (pulledProds != null)
                    {
                        productoCount = pulledProds.Count;
                        var existingProds = await context.Productos.ToListAsync(ct);
                        var existingMap = existingProds.ToDictionary(p => p.Id);

                        foreach (var pulled in pulledProds)
                        {
                            // Validar que la CategoriaId exista localmente para evitar error de FK.
                            var categoriaExiste = await context.Categorias.AnyAsync(c => c.Id == pulled.CategoriaId, ct);
                            if (!categoriaExiste) continue;

                            if (existingMap.TryGetValue(pulled.Id, out var existing))
                            {
                                existing.CategoriaId = pulled.CategoriaId;
                                existing.Nombre = pulled.Nombre;
                                existing.ColorUi = pulled.ColorUi;
                                existing.RequiereCocina = pulled.RequiereCocina;
                                existing.IsActive = pulled.IsActive;
                                existing.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                productoNew++;
                                var newProd = new Producto
                                {
                                    Id = pulled.Id,
                                    CategoriaId = pulled.CategoriaId,
                                    Nombre = pulled.Nombre,
                                    ColorUi = pulled.ColorUi,
                                    RequiereCocina = pulled.RequiereCocina,
                                    IsActive = pulled.IsActive,
                                    CreatedAt = pulled.CreatedAt,
                                    UpdatedAt = pulled.UpdatedAt
                                };
                                context.Productos.Add(newProd);
                            }
                        }

                        // Deactivar locales que no estén en la lista del pull (sync de borrado lógico)
                        var pulledIds = pulledProds.Select(p => p.Id).ToHashSet();
                        foreach (var localProd in existingProds)
                        {
                            if (!pulledIds.Contains(localProd.Id) && localProd.IsActive)
                            {
                                localProd.IsActive = false;
                                localProd.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }
                else
                {
                    errores.Add($"Productos: HTTP {(int)prodResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                errores.Add($"Productos: {ex.Message}");
            }

            // H. PULL Precios (ProductoPrecios)
            try
            {
                var preciosUrl = $"{baseUrl.TrimEnd('/')}/api/productoprecio/sucursal/{sucursalId}";
                var preciosResponse = await client.GetAsync(preciosUrl, ct);
                if (preciosResponse.IsSuccessStatusCode)
                {
                    var json = await preciosResponse.Content.ReadAsStringAsync(ct);
                    var pulledPrecios = JsonSerializer.Deserialize<List<SyncProductoPrecioDto>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    if (pulledPrecios != null)
                    {
                        precioCount = pulledPrecios.Count;
                        var existingPrecios = await context.ProductoPrecios.ToListAsync(ct);
                        var existingMap = existingPrecios.ToDictionary(p => p.Id);

                        foreach (var pulled in pulledPrecios)
                        {
                            // Validar integridad referencial local
                            var productoExiste = await context.Productos.AnyAsync(p => p.Id == pulled.ProductoId, ct);
                            var sucursalExiste = await context.Sucursales.AnyAsync(s => s.Id == pulled.SucursalId, ct);
                            var tipoVentaExiste = await context.TiposVenta.AnyAsync(t => t.Id == pulled.TipoVentaId, ct);

                            if (!productoExiste || !sucursalExiste || !tipoVentaExiste)
                                continue;

                            if (existingMap.TryGetValue(pulled.Id, out var existing))
                            {
                                existing.ProductoId = pulled.ProductoId;
                                existing.SucursalId = pulled.SucursalId;
                                existing.TipoVentaId = pulled.TipoVentaId;
                                existing.PrecioVenta = pulled.PrecioVenta;
                                existing.IsActive = pulled.IsActive;
                                existing.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                precioNew++;
                                var newPrecio = new ProductoPrecio
                                {
                                    Id = pulled.Id,
                                    ProductoId = pulled.ProductoId,
                                    SucursalId = pulled.SucursalId,
                                    TipoVentaId = pulled.TipoVentaId,
                                    PrecioVenta = pulled.PrecioVenta,
                                    IsActive = pulled.IsActive,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                context.ProductoPrecios.Add(newPrecio);
                            }
                        }

                        // Deactivar locales que no estén en la lista del pull (sync de borrado lógico)
                        var pulledIds = pulledPrecios.Select(p => p.Id).ToHashSet();
                        foreach (var localPrecio in existingPrecios)
                        {
                            if (!pulledIds.Contains(localPrecio.Id) && localPrecio.IsActive)
                            {
                                localPrecio.IsActive = false;
                                localPrecio.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }
                else
                {
                    errores.Add($"Precios: HTTP {(int)preciosResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                errores.Add($"Precios: {ex.Message}");
            }

            // I. PULL MetodosPago
            try
            {
                var mpUrl = $"{baseUrl.TrimEnd('/')}/api/metodopago?includeInactive=true";
                var mpResponse = await client.GetAsync(mpUrl, ct);
                if (mpResponse.IsSuccessStatusCode)
                {
                    var json = await mpResponse.Content.ReadAsStringAsync(ct);
                    var pulledMps = JsonSerializer.Deserialize<List<SyncMetodoPagoDto>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    if (pulledMps != null)
                    {
                        metodoPagoCount = pulledMps.Count;
                        var existingMps = await context.MetodosPago.ToListAsync(ct);
                        var existingMap = existingMps.ToDictionary(m => m.Id);
                        // Métodos sembrados independientemente en Local y Nube (p. ej. "Efectivo", "Cuenta Corriente")
                        // pueden tener Ids distintos aunque el Nombre coincida. Como el nombre es único, se
                        // usa como clave de reconciliación de respaldo para evitar duplicados/errores de índice único.
                        var existingByNombre = existingMps.ToDictionary(m => m.Nombre, StringComparer.OrdinalIgnoreCase);
                        var touchedLocalIds = new HashSet<Guid>();

                        foreach (var pulled in pulledMps)
                        {
                            if (existingMap.TryGetValue(pulled.Id, out var existing) ||
                                existingByNombre.TryGetValue(pulled.Nombre, out existing))
                            {
                                existing.Nombre = pulled.Nombre;
                                existing.ComisionPorcentaje = pulled.ComisionPorcentaje;
                                existing.RequiereFacturaAfip = pulled.RequiereFacturaAfip;
                                existing.EsCuentaCorriente = pulled.EsCuentaCorriente;
                                existing.IsActive = pulled.IsActive;
                                existing.UpdatedAt = DateTime.UtcNow;
                                // Nunca se conserva el Id remoto: los Pagos locales ya referencian el Id local existente.
                                touchedLocalIds.Add(existing.Id);
                            }
                            else
                            {
                                metodoPagoNew++;
                                var newMp = new MetodoPago
                                {
                                    Id = pulled.Id,
                                    Nombre = pulled.Nombre,
                                    ComisionPorcentaje = pulled.ComisionPorcentaje,
                                    RequiereFacturaAfip = pulled.RequiereFacturaAfip,
                                    EsCuentaCorriente = pulled.EsCuentaCorriente,
                                    IsActive = pulled.IsActive,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                context.MetodosPago.Add(newMp);
                                touchedLocalIds.Add(newMp.Id);
                            }
                        }

                        // Sync de borrado lógico (comparando por Id local reconciliado, no por el Id remoto crudo)
                        foreach (var localMp in existingMps)
                        {
                            if (!touchedLocalIds.Contains(localMp.Id) && localMp.IsActive)
                            {
                                localMp.IsActive = false;
                                localMp.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }
                else
                {
                    errores.Add($"MetodosPago: HTTP {(int)mpResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                errores.Add($"MetodosPago: {ex.Message}");
            }

            // Nota: Clientes se sincroniza en su propio ciclo independiente (SincronizarClientesAsync),
            // ejecutado en cada tick del worker (no solo en pull forzado), para que las altas hechas
            // desde el Backoffice aparezcan en el POS sin depender de una sincronización manual.

            await context.SaveChangesAsync(ct);
            sw.Stop();

            // ── Log resumen del PULL ──
            if (errores.Count > 0)
            {
                var errMsg = string.Join("; ", errores);
                _logger.LogWarning(
                    "📥 [PULL] ⚠️ Sincronización de catálogos finalizada con advertencias/errores ({Ms}ms):\n" +
                    "  ▪️ Planos POS: {Config} ({ConfigNew} nuevos/modificados)\n" +
                    "  ▪️ Mesas: {Mesas} ({MesaNew} nuevas/modificadas)\n" +
                    "  ▪️ Roles: {Roles} ({RolNew} nuevos/modificados)\n" +
                    "  ▪️ Empleados: {Usuarios} ({UsuarioNew} nuevos/modificados)\n" +
                    "  ▪️ Tipos Venta: {TiposVenta} ({TiposVentaNew} nuevos/modificados)\n" +
                    "  ▪️ Categorías: {Categorias} ({CategoriasNew} nuevas/modificadas)\n" +
                    "  ▪️ Productos: {Productos} ({ProductosNew} nuevos/modificados)\n" +
                    "  ▪️ Precios: {Precios} ({PreciosNew} nuevos/modificados)\n" +
                    "  ▪️ Métodos Pago: {MetodosPago} ({MetodosPagoNew} nuevos/modificados)\n" +
                    "  ❌ Errores: {Errores}",
                    sw.ElapsedMilliseconds, configCount, configNew, mesaCount, mesaNew, rolCount, rolNew, usuarioCount, usuarioNew,
                    tipoVentaCount, tipoVentaNew, categoriaCount, categoriaNew, productoCount, productoNew, precioCount, precioNew,
                    metodoPagoCount, metodoPagoNew, errMsg);

                var summary = $"📥 [PULL] ⚠️ Parcial con errores ({sw.ElapsedMilliseconds}ms) — Planos: {configCount} (+{configNew}), Mesas: {mesaCount} (+{mesaNew}), Roles: {rolCount} (+{rolNew}), Empleados: {usuarioCount} (+{usuarioNew}), Categorías: {categoriaCount} (+{categoriaNew}), Productos: {productoCount} (+{productoNew}), Precios: {precioCount} (+{precioNew}), Métodos Pago: {metodoPagoCount} (+{metodoPagoNew}). Errores: {errMsg}";
                LocalSyncLogStore.AddLog("PULL", $"📥 PULL: ⚠️ Descarga finalizada con advertencias: {errMsg}", false);
                await EnviarLogANubeAsync(sucursalId, sucursalNombre, "PULL", summary, false, jwtToken, ct);
            }
            else
            {
                _logger.LogInformation(
                    "📥 [PULL] ✅ Sincronización de catálogos finalizada con éxito ({Ms}ms):\n" +
                    "  ▪️ Planos POS: {Config} ({ConfigNew} nuevos/modificados)\n" +
                    "  ▪️ Mesas: {Mesas} ({MesaNew} nuevas/modificadas)\n" +
                    "  ▪️ Roles: {Roles} ({RolNew} nuevos/modificados)\n" +
                    "  ▪️ Empleados: {Usuarios} ({UsuarioNew} nuevos/modificados)\n" +
                    "  ▪️ Tipos Venta: {TiposVenta} ({TiposVentaNew} nuevos/modificados)\n" +
                    "  ▪️ Categorías: {Categorias} ({CategoriasNew} nuevas/modificadas)\n" +
                    "  ▪️ Productos: {Productos} ({ProductosNew} nuevos/modificados)\n" +
                    "  ▪️ Precios: {Precios} ({PreciosNew} nuevos/modificados)\n" +
                    "  ▪️ Métodos Pago: {MetodosPago} ({MetodosPagoNew} nuevos/modificados)",
                    sw.ElapsedMilliseconds, configCount, configNew, mesaCount, mesaNew, rolCount, rolNew, usuarioCount, usuarioNew,
                    tipoVentaCount, tipoVentaNew, categoriaCount, categoriaNew, productoCount, productoNew, precioCount, precioNew,
                    metodoPagoCount, metodoPagoNew);

                var summary = $"📥 [PULL] ✅ Éxito ({sw.ElapsedMilliseconds}ms) — Planos: {configCount} (+{configNew}), Mesas: {mesaCount} (+{mesaNew}), Roles: {rolCount} (+{rolNew}), Empleados: {usuarioCount} (+{usuarioNew}), Categorías: {categoriaCount} (+{categoriaNew}), Productos: {productoCount} (+{productoNew}), Precios: {precioCount} (+{precioNew}), Métodos Pago: {metodoPagoCount} (+{metodoPagoNew})";
                LocalSyncLogStore.AddLog("PULL", $"📥 PULL: ✅ Descarga de catálogos completada con éxito. Planos: {configCount}, Mesas: {mesaCount}, Empleados: {usuarioCount}, Productos: {productoCount}.", true);
                await EnviarLogANubeAsync(sucursalId, sucursalNombre, "PULL", summary, true, jwtToken, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "📥 PULL ❌ Error general al descargar catálogos de la Nube.");
            LocalSyncLogStore.AddLog("ERROR", $"📥 PULL ❌ Error general de descarga: {ex.Message}", false);
            await EnviarLogANubeAsync(sucursalId, sucursalNombre, "ERROR", $"📥 PULL ❌ Error general al descargar catálogos: {ex.Message}", false, jwtToken, ct);
        }
    }

    /// <summary>
    /// Sincroniza el catálogo de Clientes desde la Nube en cada ciclo del worker (no solo en pull forzado),
    /// para que las altas hechas desde el Backoffice aparezcan en el POS sin depender de una sincronización manual.
    /// Los datos de contacto (Teléfono, Email, LímiteCredito) de un cliente ya existente localmente NUNCA
    /// se pisan desde acá: son propiedad del lado que los cargó (POS o Backoffice) igual que el SaldoActual.
    /// Solo se actualizan Nombre/Apellido/IsActive para reflejar ediciones o bajas hechas en el Backoffice.
    /// </summary>
    private async Task SincronizarClientesAsync(LocalContext context, string jwtToken, CancellationToken ct)
    {
        var baseUrl = _configuration["NubeApi:BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return;

        try
        {
            var client = _httpClientFactory.CreateClient("NubeApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

            var clienteUrl = $"{baseUrl.TrimEnd('/')}/api/cliente";
            var clienteResponse = await client.GetAsync(clienteUrl, ct);
            if (!clienteResponse.IsSuccessStatusCode) return;

            var json = await clienteResponse.Content.ReadAsStringAsync(ct);
            var pulledClientes = JsonSerializer.Deserialize<List<SyncClienteDto>>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (pulledClientes == null) return;

            var existingClientes = await context.Clientes.ToListAsync(ct);
            var existingMap = existingClientes.ToDictionary(c => c.Id);
            var nuevos = 0;

            foreach (var pulled in pulledClientes)
            {
                if (existingMap.TryGetValue(pulled.Id, out var existing))
                {
                    existing.Nombre = pulled.Nombre;
                    existing.Apellido = pulled.Apellido;
                    existing.IsActive = pulled.IsActive;
                    existing.SyncEstado = SyncEstado.Sincronizado;
                    existing.UpdatedAt = DateTime.UtcNow;
                    // Telefono/Email/LimiteCredito/SaldoActual: nunca se pisan acá.
                }
                else
                {
                    nuevos++;
                    var newCliente = new Cliente
                    {
                        Id = pulled.Id,
                        Nombre = pulled.Nombre,
                        Apellido = pulled.Apellido,
                        Telefono = pulled.Telefono,
                        Email = pulled.Email,
                        LimiteCredito = pulled.LimiteCredito,
                        IsActive = pulled.IsActive,
                        SyncEstado = SyncEstado.Sincronizado,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.Clientes.Add(newCliente);
                    await context.SaveChangesAsync(ct);

                    var cuentaExiste = await context.CuentasCorrientes.AnyAsync(cc => cc.ClienteId == pulled.Id, ct);
                    if (!cuentaExiste)
                    {
                        context.CuentasCorrientes.Add(new CuentaCorriente
                        {
                            ClienteId = pulled.Id,
                            SaldoActual = pulled.SaldoActual,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        });
                        await context.SaveChangesAsync(ct);
                    }
                }
            }

            // Sync de borrado lógico (un cliente desactivado en el Backoffice se desactiva también localmente)
            var pulledIds = pulledClientes.Select(c => c.Id).ToHashSet();
            foreach (var localCliente in existingClientes)
            {
                if (!pulledIds.Contains(localCliente.Id) && localCliente.IsActive)
                {
                    localCliente.IsActive = false;
                    localCliente.UpdatedAt = DateTime.UtcNow;
                }
            }

            await context.SaveChangesAsync(ct);

            if (nuevos > 0)
            {
                _logger.LogInformation("👥 Clientes: {Count} sincronizados desde la Nube ({Nuevos} nuevos).", pulledClientes.Count, nuevos);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("No se pudo sincronizar el catálogo de clientes: {Message}", ex.Message);
        }
    }

    private async Task EnviarLogANubeAsync(Guid sucursalId, string sucursalNombre, string tipo, string mensaje, bool exitoso, string jwtToken, CancellationToken ct)
    {
        var baseUrl = _configuration["NubeApi:BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl)) return;

        try
        {
            var client = _httpClientFactory.CreateClient("NubeApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            var url = $"{baseUrl.TrimEnd('/')}/api/sync/log";
            var payload = new 
            { 
                sucursalId, 
                sucursalNombre, 
                tipo, 
                mensaje, 
                exitoso 
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await client.PostAsync(url, content, ct);
        }
        catch { }
    }

    private async Task<(bool Exitoso, string? ErrorDetail)> EnviarANubeAsync(SyncPayload payload, string jwtToken, CancellationToken ct)
    {
        var baseUrl = _configuration["NubeApi:BaseUrl"];

        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.LogWarning("📤 PUSH ❌ NubeApi:BaseUrl no configurada. Sincronización deshabilitada.");
            return (false, "NubeApi:BaseUrl no configurada");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("NubeApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{baseUrl.TrimEnd('/')}/api/sync/recibir";

            _logger.LogDebug("📤 POST {Url} — {Bytes} bytes", url, json.Length);

            var response = await client.PostAsync(url, content, ct);

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("📤 PUSH ❌ Nube respondió HTTP {StatusCode}: {Body}", (int)response.StatusCode, body);
                
                string errorDetail = body;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("message", out var msgProp))
                    {
                        errorDetail = msgProp.GetString() ?? body;
                    }
                }
                catch {}
                
                if (errorDetail.Length > 150) errorDetail = errorDetail.Substring(0, 150) + "...";
                
                return (false, $"HTTP {(int)response.StatusCode}: {errorDetail}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("📤 PUSH ❌ Sin conectividad con la Nube: {Message}. Operando OFFLINE.", ex.Message);
            return (false, $"Error de conexión: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("📤 PUSH ❌ Timeout al conectar con la Nube. Operando OFFLINE.");
            return (false, "Timeout de red al conectar con la Nube");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("📤 PUSH ❌ Error inesperado: {Message}.", ex.Message);
            return (false, $"Error inesperado: {ex.Message}");
        }
    }
}

// ═══════════════════════════════════
// DTOs de Sincronización
// ═══════════════════════════════════

public class SyncPayload
{
    public DateTime Timestamp { get; set; }
    public List<SyncComanda> Comandas { get; set; } = [];
    public List<SyncPago> Pagos { get; set; } = [];
    public List<SyncMovimiento> Movimientos { get; set; } = [];
    public List<SyncCierreDiario> CierresDiarios { get; set; } = [];
    public List<SyncCliente> Clientes { get; set; } = [];
    public List<SyncMovimientoCuentaCorriente> MovimientosCuentaCorriente { get; set; } = [];
    public List<SyncTurnoCaja> TurnosCaja { get; set; } = [];
    public List<SyncCaja> Cajas { get; set; } = [];
}

/// <summary>
/// DTO de PUSH (Local → Nube) de la Caja real de la sucursal, para que los TurnoCaja
/// reales puedan enlazar por FK contra la misma Caja que existe en el POS local
/// (antes la Nube fabricaba su propia "Caja Sincronizada Principal" con otro Id).
/// </summary>
public class SyncCaja
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string TipoCaja { get; set; } = string.Empty;
}

/// <summary>
/// DTO de PUSH (Local → Nube) del turno de caja real (apertura/cierre/fondo/arqueo),
/// para que la Nube deje de fabricar turnos "stub" al recibir Pagos/Movimientos que
/// los referencian por FK.
/// </summary>
public class SyncTurnoCaja
{
    public Guid Id { get; set; }
    public Guid CajaId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public DateTime FechaContable { get; set; }
    public string Turno { get; set; } = string.Empty;
    public decimal FondoInicial { get; set; }
    public decimal? DiferenciaArqueo { get; set; }
}

/// <summary>
/// DTO de PUSH (Local → Nube) para clientes altados en el POS.
/// Solo lleva el nombre: el resto de los datos de contacto son "solo del POS" y no se sincronizan.
/// </summary>
public class SyncCliente
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SyncMovimientoCuentaCorriente
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid? ComandaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SyncClienteDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public decimal LimiteCredito { get; set; }
    public decimal SaldoActual { get; set; }
    public bool IsActive { get; set; }
}

public class SyncCierreDiario
{
    public Guid Id { get; set; }
    public Guid CajaId { get; set; }
    public DateTime Fecha { get; set; }
    public Guid UsuarioCierreId { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal TotalNeto { get; set; }
    public string? ResumenJson { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SyncMetodoPagoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal ComisionPorcentaje { get; set; }
    public bool RequiereFacturaAfip { get; set; }
    public bool EsCuentaCorriente { get; set; }
    public bool IsActive { get; set; }
}

public class SyncComanda
{
    public Guid Id { get; set; }
    public Guid TipoVentaId { get; set; }
    public Guid? MesaId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
    public DateTime FechaContable { get; set; }
    public string Turno { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<SyncComandaItem> Items { get; set; } = [];
}

public class SyncComandaItem
{
    public Guid Id { get; set; }
    public Guid ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public string? Notas { get; set; }
    public bool Cancelado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public Guid? AnuladoPorUsuarioId { get; set; }
    public DateTime? FechaAnulacion { get; set; }
}

public class SyncPago
{
    public Guid Id { get; set; }
    public Guid ComandaId { get; set; }
    public Guid TurnoCajaId { get; set; }
    public Guid MetodoPagoId { get; set; }
    public decimal Monto { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SyncMovimiento
{
    public Guid Id { get; set; }
    public Guid TurnoCajaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SyncConfigPosDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ConfiguracionJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncMesaDto
{
    public Guid Id { get; set; }
    public string Etiqueta { get; set; } = string.Empty;
    public int Capacidad { get; set; }
    public double PosX { get; set; }
    public double PosY { get; set; }
    public BaresFamilia.Core.Models.Enums.FormaMesa Forma { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncRolDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public List<string> Permisos { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncUsuarioDto
{
    public Guid Id { get; set; }
    public Guid RolId { get; set; }
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PinAcceso { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncActivationData
{
    public string Token { get; set; } = string.Empty;
    public Guid SucursalId { get; set; }
    public Guid DispositivoId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
}

public class SyncTipoVentaDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool AplicaRecargo { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncCategoriaDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int OrdenVisual { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncProductoDto
{
    public Guid Id { get; set; }
    public Guid CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ColorUi { get; set; } = string.Empty;
    public bool RequiereCocina { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncProductoPrecioDto
{
    public Guid Id { get; set; }
    public Guid ProductoId { get; set; }
    public Guid SucursalId { get; set; }
    public Guid TipoVentaId { get; set; }
    public decimal PrecioVenta { get; set; }
    public bool IsActive { get; set; }
}
