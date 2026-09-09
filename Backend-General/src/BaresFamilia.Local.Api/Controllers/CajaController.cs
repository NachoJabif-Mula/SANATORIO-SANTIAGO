using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Local.Api.Controllers;

/// <summary>
/// Endpoints para gestión de turnos de caja, egresos, cierre de turno y cierre diario.
/// Los turnos de caja son gestionados por usuarios con rol "encargado".
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CajaController : ControllerBase
{
    private readonly IService<MovimientoCaja> _movimientoService;
    private readonly IService<TurnoCaja> _turnoCajaService;
    private readonly LocalContext _context;
    private readonly ILogger<CajaController> _logger;

    public CajaController(
        IService<MovimientoCaja> movimientoService,
        IService<TurnoCaja> turnoCajaService,
        LocalContext context,
        ILogger<CajaController> logger)
    {
        _movimientoService = movimientoService;
        _turnoCajaService = turnoCajaService;
        _context = context;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════
    // APERTURA Y CONSULTA DE TURNO
    // ═══════════════════════════════════════════════════════

    private async Task<(DateTime FechaContable, string Turno, string Estado)> GetEstadoCajaAsync(Guid cajaId, CancellationToken ct)
    {
        // 1. Buscar si hay algún turno activo (sin FechaCierre)
        var activeTurno = await _context.TurnosCaja
            .Where(t => t.CajaId == cajaId && t.FechaCierre == null && t.IsActive)
            .OrderByDescending(t => t.FechaApertura)
            .FirstOrDefaultAsync(ct);

        if (activeTurno != null)
        {
            return (activeTurno.FechaContable, activeTurno.Turno, activeTurno.Turno == "AM" ? "AbiertoAM" : "AbiertoPM");
        }

        // 2. Si no hay turno activo, buscar el último turno cerrado
        var lastClosedTurno = await _context.TurnosCaja
            .Where(t => t.CajaId == cajaId && t.FechaCierre != null && t.IsActive)
            .OrderByDescending(t => t.FechaApertura)
            .FirstOrDefaultAsync(ct);

        if (lastClosedTurno == null)
        {
            return (DateTime.UtcNow.Date, "AM", "CerradoEsperandoAM");
        }

        if (lastClosedTurno.Turno == "AM")
        {
            return (lastClosedTurno.FechaContable, "PM", "CerradoEsperandoPM");
        }
        else
        {
            return (lastClosedTurno.FechaContable.AddDays(1), "AM", "CerradoEsperandoAM");
        }
    }

    /// <summary>
    /// Endpoint para consultar el estado actual contable y turno disponible de la caja.
    /// </summary>
    [HttpGet("estado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEstadoCajaEndpoint(CancellationToken ct)
    {
        var caja = await _context.Cajas.FirstOrDefaultAsync(c => c.IsActive, ct);
        if (caja == null)
        {
            return Ok(new
            {
                fechaContable = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                turno = "AM",
                estado = "SinCaja"
            });
        }

        var (fechaContable, turno, estado) = await GetEstadoCajaAsync(caja.Id, ct);
        return Ok(new
        {
            fechaContable = DateTime.SpecifyKind(fechaContable, DateTimeKind.Utc),
            turno,
            estado
        });
    }

    /// <summary>
    /// Obtiene el turno de caja activo (sin FechaCierre). Retorna 204 si no hay turno abierto.
    /// </summary>
    [HttpGet("turno-activo")]
    [ProducesResponseType(typeof(TurnoActivoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetTurnoActivo(CancellationToken ct)
    {
        var turno = await _context.TurnosCaja
            .Include(t => t.Caja)
            .Include(t => t.Usuario)
            .Where(t => t.FechaCierre == null && t.IsActive)
            .OrderByDescending(t => t.FechaApertura)
            .FirstOrDefaultAsync(ct);

        if (turno is null)
            return NoContent();

        return Ok(new TurnoActivoResponse
        {
            TurnoId = turno.Id,
            CajaId = turno.CajaId,
            CajaNombre = turno.Caja.Nombre,
            UsuarioId = turno.UsuarioId,
            UsuarioNombre = turno.Usuario.Nombre,
            FechaApertura = turno.FechaApertura,
            FondoInicial = turno.FondoInicial,
            FechaContable = DateTime.SpecifyKind(turno.FechaContable, DateTimeKind.Utc),
            Turno = turno.Turno
        });
    }

    /// <summary>
    /// Abre un nuevo turno de caja. Solo permitido si no hay otro turno abierto para la misma caja.
    /// Requiere usuario con permiso de encargado.
    /// </summary>
    [HttpPost("abrir-turno")]
    [ProducesResponseType(typeof(TurnoActivoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AbrirTurno([FromBody] AbrirTurnoRequest request, CancellationToken ct)
    {
        if (request.FondoInicial < 0)
            return BadRequest(new { message = "El fondo inicial no puede ser negativo." });

        // Obtener o crear la caja principal de la sucursal
        var caja = await _context.Cajas
            .FirstOrDefaultAsync(c => c.IsActive, ct);

        if (caja is null)
        {
            // Obtener sucursalId del usuario
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == request.UsuarioId && u.IsActive, ct);
            
            if (usuario is null)
                return NotFound(new { message = "Usuario no encontrado." });

            caja = new Caja
            {
                Id = Guid.NewGuid(),
                SucursalId = usuario.SucursalId,
                Nombre = "Caja Principal",
                TipoCaja = TipoCaja.Principal,
                SyncEstado = SyncEstado.Pendiente,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.Cajas.Add(caja);
            await _context.SaveChangesAsync(ct);
        }

        // Verificar que no haya otro turno abierto para esta caja
        var turnoAbierto = await _context.TurnosCaja
            .AnyAsync(t => t.CajaId == caja.Id && t.FechaCierre == null && t.IsActive, ct);

        if (turnoAbierto)
            return BadRequest(new { message = "Ya existe un turno abierto para esta caja. Debe cerrarse antes de abrir uno nuevo." });

        // Verificar que el usuario existe
        var usr = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == request.UsuarioId && u.IsActive, ct);

        if (usr is null)
            return NotFound(new { message = "Usuario no encontrado." });

        // Determinar fecha contable y turno según el histórico
        var (fechaContable, turno, _) = await GetEstadoCajaAsync(caja.Id, ct);

        var nuevoTurno = new TurnoCaja
        {
            Id = Guid.NewGuid(),
            CajaId = caja.Id,
            UsuarioId = request.UsuarioId,
            FechaApertura = DateTime.UtcNow,
            FondoInicial = request.FondoInicial,
            FechaContable = DateTime.SpecifyKind(fechaContable.Date, DateTimeKind.Utc),
            Turno = turno,
            SyncEstado = SyncEstado.Pendiente,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.TurnosCaja.Add(nuevoTurno);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Turno de caja abierto. TurnoId: {TurnoId}. CajaId: {CajaId}. Usuario: {UsuarioNombre}. FondoInicial: {Fondo}. FechaContable: {FechaContable}. Turno: {Turno}",
            nuevoTurno.Id, caja.Id, usr.Nombre, request.FondoInicial, nuevoTurno.FechaContable, nuevoTurno.Turno);

        return Created(string.Empty, new TurnoActivoResponse
        {
            TurnoId = nuevoTurno.Id,
            CajaId = caja.Id,
            CajaNombre = caja.Nombre,
            UsuarioId = usr.Id,
            UsuarioNombre = usr.Nombre,
            FechaApertura = nuevoTurno.FechaApertura,
            FondoInicial = nuevoTurno.FondoInicial,
            FechaContable = DateTime.SpecifyKind(nuevoTurno.FechaContable, DateTimeKind.Utc),
            Turno = nuevoTurno.Turno
        });
    }

    // ═══════════════════════════════════════════════════════
    // EGRESOS / PAGO A PROVEEDORES
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Registra un egreso de caja (retiro de efectivo para pago a proveedores,
    /// gastos operativos, etc.) en el turno de caja indicado.
    /// </summary>
    [HttpPost("egresos")]
    [ProducesResponseType(typeof(MovimientoCaja), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarEgreso([FromBody] RegistrarEgresoRequest request, CancellationToken ct)
    {
        if (request.Monto <= 0)
            return BadRequest(new { message = "El monto del egreso debe ser mayor a 0." });

        if (string.IsNullOrWhiteSpace(request.Concepto))
            return BadRequest(new { message = "El concepto del egreso es obligatorio." });

        // Validar que el turno de caja existe y está abierto
        var turnoCaja = await _turnoCajaService.GetByIdAsync(request.TurnoCajaId, ct);
        if (turnoCaja is null)
            return NotFound(new { message = $"Turno de caja con ID '{request.TurnoCajaId}' no encontrado." });

        if (turnoCaja.FechaCierre is not null)
            return BadRequest(new { message = "El turno de caja ya está cerrado. No se pueden registrar más movimientos." });

        var movimiento = new MovimientoCaja
        {
            TurnoCajaId = request.TurnoCajaId,
            Tipo = TipoMovimientoCaja.Egreso,
            Monto = request.Monto,
            Concepto = request.Concepto.Trim(),
            ReferenciaComprobante = request.ReferenciaComprobante?.Trim(),
            SyncEstado = SyncEstado.Pendiente
        };

        var created = await _movimientoService.CreateAsync(movimiento, ct);

        _logger.LogInformation(
            "Egreso registrado: {MovimientoId}. TurnoCaja: {TurnoCajaId}. Monto: {Monto}. Concepto: {Concepto}",
            created.Id, request.TurnoCajaId, request.Monto, request.Concepto);

        return Created(string.Empty, created);
    }

    /// <summary>
    /// Obtiene el resumen parcial de un turno de caja (sin cerrarlo).
    /// </summary>
    [HttpGet("resumen-turno/{turnoId:guid}")]
    [ProducesResponseType(typeof(ResultadoCierreTurno), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResumenTurno(Guid turnoId, CancellationToken ct)
    {
        var turnoCaja = await _context.TurnosCaja
            .Include(t => t.Caja)
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.Id == turnoId && t.IsActive, ct);

        if (turnoCaja is null)
            return NotFound(new { message = $"Turno de caja con ID '{turnoId}' no encontrado." });

        // Calcular pagos del turno agrupados por método de pago
        var pagosPorMetodo = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Where(p => p.TurnoCajaId == turnoId && p.IsActive)
            .GroupBy(p => new { p.MetodoPagoId, p.MetodoPago.Nombre })
            .Select(g => new DesglosePorMetodo
            {
                MetodoPagoId = g.Key.MetodoPagoId,
                MetodoPagoNombre = g.Key.Nombre,
                CantidadOperaciones = g.Count(),
                Total = g.Sum(p => p.Monto)
            })
            .ToListAsync(ct);

        var totalVentas = pagosPorMetodo.Sum(p => p.Total);

        // Calcular movimientos (ingresos y egresos)
        var movimientos = await _movimientoService.FindAsync(m => m.TurnoCajaId == turnoId, ct);
        var movimientosList = movimientos.ToList();

        var totalIngresos = movimientosList
            .Where(m => m.Tipo == TipoMovimientoCaja.Ingreso)
            .Sum(m => m.Monto);

        var totalEgresos = movimientosList
            .Where(m => m.Tipo == TipoMovimientoCaja.Egreso)
            .Sum(m => m.Monto);

        var totalEfectivo = pagosPorMetodo
            .Where(p => p.MetodoPagoNombre.Equals("Efectivo", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Total);

        var montoEsperadoEfectivo = turnoCaja.FondoInicial + totalEfectivo + totalIngresos - totalEgresos;

        return Ok(new ResultadoCierreTurno
        {
            TurnoCajaId = turnoCaja.Id,
            FechaCierre = turnoCaja.FechaCierre ?? DateTime.UtcNow,
            FondoInicial = turnoCaja.FondoInicial,
            TotalVentas = totalVentas,
            TotalIngresos = totalIngresos,
            TotalEgresos = totalEgresos,
            MontoEsperadoEfectivo = montoEsperadoEfectivo,
            MontoDeclarado = 0,
            DiferenciaArqueo = 0,
            DesglosePorMetodo = pagosPorMetodo,
            Observaciones = null,
            EsUltimoTurnoDia = turnoCaja.Turno == "PM",
            TurnosAbiertosDia = 0
        });
    }

    // ═══════════════════════════════════════════════════════
    // CIERRE DE TURNO (CAJA CIEGA)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Realiza el cierre de turno (caja ciega). El encargado declara el efectivo que tiene
    /// sin ver el total calculado. El sistema calcula la diferencia (arqueo).
    /// Incluye desglose por método de pago, verificación de mesas abiertas y cierre diario automático si es PM.
    /// </summary>
    [HttpPost("cerrar-turno")]
    [ProducesResponseType(typeof(ResultadoCierreTurno), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CerrarTurno([FromBody] CerrarTurnoRequest request, CancellationToken ct)
    {
        // 1. Validar turno
        var turnoCaja = await _context.TurnosCaja
            .Include(t => t.Caja)
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.Id == request.TurnoCajaId, ct);

        if (turnoCaja is null)
            return NotFound(new { message = $"Turno de caja con ID '{request.TurnoCajaId}' no encontrado." });

        if (turnoCaja.FechaCierre is not null)
            return BadRequest(new { message = "Este turno de caja ya fue cerrado." });

        if (request.MontoDeclarado < 0)
            return BadRequest(new { message = "El monto declarado no puede ser negativo." });

        // Verificar mesas con comandas abiertas en este turno y fecha contable.
        // Las cuentas corrientes abiertas (FechaContable null, exentas de turno) no bloquean el cierre.
        var openComandasCount = await _context.Comandas
            .CountAsync(c => c.FechaContable != null && c.FechaContable.Value.Date == turnoCaja.FechaContable.Date
                && c.Turno == turnoCaja.Turno
                && c.Estado == ComandaEstado.Abierta
                && c.IsActive, ct);

        if (openComandasCount > 0)
        {
            if (turnoCaja.Turno == "PM")
            {
                return BadRequest(new { 
                    error = "MesasAbiertasPM", 
                    message = $"No se puede realizar el cierre de turno PM ni el cierre diario porque existen {openComandasCount} comanda(s) abierta(s). Debe cerrarlas todas antes de proceder.", 
                    count = openComandasCount 
                });
            }

            if (request.TransferirMesasAbiertas != true)
            {
                return BadRequest(new { 
                    error = "MesasAbiertas", 
                    message = $"Hay {openComandasCount} mesa(s) con comandas abiertas en este turno.", 
                    count = openComandasCount 
                });
            }
            else
            {
                // Transferirlas al siguiente turno
                DateTime nextFecha;
                string nextTurno;
                if (turnoCaja.Turno == "AM")
                {
                    nextFecha = turnoCaja.FechaContable;
                    nextTurno = "PM";
                }
                else // PM
                {
                    nextFecha = turnoCaja.FechaContable.AddDays(1);
                    nextTurno = "AM";
                }

                var openComandas = await _context.Comandas
                    .Where(c => c.FechaContable != null && c.FechaContable.Value.Date == turnoCaja.FechaContable.Date
                        && c.Turno == turnoCaja.Turno
                        && c.Estado == ComandaEstado.Abierta
                        && c.IsActive)
                    .ToListAsync(ct);

                foreach (var comanda in openComandas)
                {
                    comanda.FechaContable = DateTime.SpecifyKind(nextFecha, DateTimeKind.Utc);
                    comanda.Turno = nextTurno;
                    comanda.UpdatedAt = DateTime.UtcNow;
                    comanda.SyncEstado = SyncEstado.Pendiente;
                }

                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("Se transfirieron {Count} comandas abiertas de {Fecha} {Turno} al turno {NextTurno} de {NextFecha}", 
                    openComandas.Count, turnoCaja.FechaContable, turnoCaja.Turno, nextTurno, nextFecha);
            }
        }

        // 2. Calcular pagos del turno agrupados por método de pago
        var pagosPorMetodo = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Where(p => p.TurnoCajaId == request.TurnoCajaId && p.IsActive)
            .GroupBy(p => new { p.MetodoPagoId, p.MetodoPago.Nombre })
            .Select(g => new DesglosePorMetodo
            {
                MetodoPagoId = g.Key.MetodoPagoId,
                MetodoPagoNombre = g.Key.Nombre,
                CantidadOperaciones = g.Count(),
                Total = g.Sum(p => p.Monto)
            })
            .ToListAsync(ct);

        var totalVentas = pagosPorMetodo.Sum(p => p.Total);

        // 3. Calcular movimientos (ingresos y egresos)
        var movimientos = await _movimientoService.FindAsync(m => m.TurnoCajaId == request.TurnoCajaId, ct);
        var movimientosList = movimientos.ToList();

        var totalIngresos = movimientosList
            .Where(m => m.Tipo == TipoMovimientoCaja.Ingreso)
            .Sum(m => m.Monto);

        var totalEgresos = movimientosList
            .Where(m => m.Tipo == TipoMovimientoCaja.Egreso)
            .Sum(m => m.Monto);

        // 4. Calcular monto esperado en efectivo
        var totalEfectivo = pagosPorMetodo
            .Where(p => p.MetodoPagoNombre.Equals("Efectivo", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Total);

        var montoEsperadoEfectivo = turnoCaja.FondoInicial + totalEfectivo + totalIngresos - totalEgresos;
        var diferencia = request.MontoDeclarado - montoEsperadoEfectivo;

        // 5. Cerrar el turno
        turnoCaja.FechaCierre = DateTime.UtcNow;
        turnoCaja.DiferenciaArqueo = diferencia;
        turnoCaja.SyncEstado = SyncEstado.Pendiente;
        turnoCaja.UpdatedAt = DateTime.UtcNow;

        _context.TurnosCaja.Update(turnoCaja);
        await _context.SaveChangesAsync(ct);

        // Si es PM, disparar el Cierre Diario Automático
        if (turnoCaja.Turno == "PM")
        {
            try
            {
                await GenerarCierreDiarioInternoAsync(turnoCaja.CajaId, turnoCaja.FechaContable, turnoCaja.UsuarioId, "Cierre Diario Automático por cierre de turno PM.", ct);
                _logger.LogInformation("Cierre diario automático generado con éxito para Caja: {CajaId}, FechaContable: {Fecha}", turnoCaja.CajaId, turnoCaja.FechaContable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar cierre diario automático para Caja: {CajaId}, FechaContable: {Fecha}", turnoCaja.CajaId, turnoCaja.FechaContable);
            }
        }

        _logger.LogInformation(
            "Cierre de turno realizado. TurnoCaja: {TurnoId}. Declarado: {Declarado}. Esperado: {Esperado}. Diferencia: {Diferencia}. Turno: {Turno}",
            request.TurnoCajaId, request.MontoDeclarado, montoEsperadoEfectivo, diferencia, turnoCaja.Turno);

        return Ok(new ResultadoCierreTurno
        {
            TurnoCajaId = turnoCaja.Id,
            FechaCierre = turnoCaja.FechaCierre!.Value,
            FondoInicial = turnoCaja.FondoInicial,
            TotalVentas = totalVentas,
            TotalIngresos = totalIngresos,
            TotalEgresos = totalEgresos,
            MontoEsperadoEfectivo = montoEsperadoEfectivo,
            MontoDeclarado = request.MontoDeclarado,
            DiferenciaArqueo = diferencia,
            DesglosePorMetodo = pagosPorMetodo,
            Observaciones = request.Observaciones,
            EsUltimoTurnoDia = turnoCaja.Turno == "PM",
            TurnosAbiertosDia = turnoCaja.Turno == "PM" ? 0 : 1
        });
    }

    private async Task<CierreDiario> GenerarCierreDiarioInternoAsync(Guid cajaId, DateTime fechaContable, Guid usuarioId, string? observaciones, CancellationToken ct)
    {
        // 1. Verificar si ya existe un cierre diario para esta fecha contable
        var cierreExistente = await _context.CierresDiarios
            .AnyAsync(c => c.CajaId == cajaId && c.Fecha.Date == fechaContable.Date, ct);
        if (cierreExistente)
        {
            throw new InvalidOperationException($"Ya se realizó el cierre diario para la fecha contable {fechaContable:dd/MM/yyyy}.");
        }

        // 2. Obtener todos los turnos cerrados de esta fecha contable
        var turnosDia = await _context.TurnosCaja
            .Include(t => t.Usuario)
            .Where(t => t.CajaId == cajaId && t.FechaContable.Date == fechaContable.Date && t.IsActive)
            .ToListAsync(ct);

        if (turnosDia.Count == 0)
        {
            throw new InvalidOperationException($"No hay turnos registrados para la fecha contable {fechaContable:dd/MM/yyyy}.");
        }

        // 3. Calcular totales consolidados
        var turnoIds = turnosDia.Select(t => t.Id).ToList();

        var pagosDia = await _context.Pagos
            .Include(p => p.MetodoPago)
            .Where(p => turnoIds.Contains(p.TurnoCajaId) && p.IsActive)
            .ToListAsync(ct);

        var totalVentas = pagosDia.Sum(p => p.Monto);

        var movimientosDia = await _context.MovimientosCaja
            .Where(m => turnoIds.Contains(m.TurnoCajaId) && m.IsActive)
            .ToListAsync(ct);

        var totalEgresos = movimientosDia
            .Where(m => m.Tipo == TipoMovimientoCaja.Egreso)
            .Sum(m => m.Monto);

        var totalIngresos = movimientosDia
            .Where(m => m.Tipo == TipoMovimientoCaja.Ingreso)
            .Sum(m => m.Monto);

        var totalNeto = totalVentas + totalIngresos - totalEgresos;

        // Desglose por método
        var desglosePorMetodo = pagosDia
            .GroupBy(p => new { p.MetodoPagoId, p.MetodoPago.Nombre })
            .Select(g => new DesglosePorMetodo
            {
                MetodoPagoId = g.Key.MetodoPagoId,
                MetodoPagoNombre = g.Key.Nombre,
                CantidadOperaciones = g.Count(),
                Total = g.Sum(p => p.Monto)
            })
            .ToList();

        // Detalle de turnos
        var detalleTurnos = turnosDia.Select(t => new DetalleTurno
        {
            TurnoId = t.Id,
            UsuarioNombre = t.Usuario?.Nombre ?? "—",
            FechaApertura = t.FechaApertura,
            FechaCierre = t.FechaCierre,
            FondoInicial = t.FondoInicial,
            DiferenciaArqueo = t.DiferenciaArqueo ?? 0
        }).ToList();

        // Crear resumen JSON
        var resumen = new
        {
            turnos = detalleTurnos,
            desglosePorMetodo,
            totalVentas,
            totalEgresos,
            totalIngresos,
            totalNeto
        };
        var resumenJson = System.Text.Json.JsonSerializer.Serialize(resumen);

        // Persistir el cierre diario
        var cierreDiario = new CierreDiario
        {
            Id = Guid.NewGuid(),
            CajaId = cajaId,
            Fecha = DateTime.SpecifyKind(fechaContable.Date, DateTimeKind.Utc),
            UsuarioCierreId = usuarioId,
            TotalVentas = totalVentas,
            TotalEgresos = totalEgresos,
            TotalNeto = totalNeto,
            ResumenJson = resumenJson,
            Observaciones = observaciones?.Trim(),
            SyncEstado = SyncEstado.Pendiente,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.CierresDiarios.Add(cierreDiario);
        await _context.SaveChangesAsync(ct);

        return cierreDiario;
    }

    // ═══════════════════════════════════════════════════════
    // CIERRE DIARIO
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Genera el cierre diario consolidando todos los turnos del día para una caja.
    /// Requiere que todos los turnos del día estén cerrados.
    /// </summary>
    [HttpPost("cierre-diario")]
    [ProducesResponseType(typeof(ResultadoCierreDiario), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CierreDiario([FromBody] CierreDiarioRequest request, CancellationToken ct)
    {
        // Obtener la caja
        var caja = await _context.Cajas.FirstOrDefaultAsync(c => c.IsActive, ct);
        if (caja is null)
            return BadRequest(new { message = "No hay caja configurada." });

        var (fechaContable, _, _) = await GetEstadoCajaAsync(caja.Id, ct);

        // Validar que no haya comandas abiertas para esta fecha contable.
        // Las cuentas corrientes abiertas (FechaContable null, exentas de turno) no bloquean el cierre diario.
        var openComandasCount = await _context.Comandas
            .CountAsync(c => c.FechaContable != null && c.FechaContable.Value.Date == fechaContable.Date
                && c.Estado == ComandaEstado.Abierta
                && c.IsActive, ct);

        if (openComandasCount > 0)
        {
            return BadRequest(new { 
                error = "MesasAbiertasDiario", 
                message = $"No se puede realizar el cierre diario porque existen {openComandasCount} comanda(s) abierta(s) para la fecha contable {fechaContable:dd/MM/yyyy}.", 
                count = openComandasCount 
            });
        }

        try
        {
            var cd = await GenerarCierreDiarioInternoAsync(caja.Id, fechaContable, request.UsuarioId, request.Observaciones, ct);
            
            // Reconstruir ResultadoCierreDiario
            var turnosDia = await _context.TurnosCaja
                .Include(t => t.Usuario)
                .Where(t => t.CajaId == caja.Id && t.FechaContable.Date == fechaContable.Date && t.IsActive)
                .ToListAsync(ct);
            var detalleTurnos = turnosDia.Select(t => new DetalleTurno
            {
                TurnoId = t.Id,
                UsuarioNombre = t.Usuario?.Nombre ?? "—",
                FechaApertura = t.FechaApertura,
                FechaCierre = t.FechaCierre,
                FondoInicial = t.FondoInicial,
                DiferenciaArqueo = t.DiferenciaArqueo ?? 0
            }).ToList();

            var pagosDia = await _context.Pagos
                .Include(p => p.MetodoPago)
                .Where(p => turnosDia.Select(t => t.Id).Contains(p.TurnoCajaId) && p.IsActive)
                .ToListAsync(ct);
            var desglosePorMetodo = pagosDia
                .GroupBy(p => new { p.MetodoPagoId, p.MetodoPago.Nombre })
                .Select(g => new DesglosePorMetodo
                {
                    MetodoPagoId = g.Key.MetodoPagoId,
                    MetodoPagoNombre = g.Key.Nombre,
                    CantidadOperaciones = g.Count(),
                    Total = g.Sum(p => p.Monto)
                })
                .ToList();

            return Ok(new ResultadoCierreDiario
            {
                CierreId = cd.Id,
                Fecha = cd.Fecha,
                CajaNombre = caja.Nombre,
                TotalTurnos = turnosDia.Count,
                TotalVentas = cd.TotalVentas,
                TotalIngresos = cd.TotalNeto - cd.TotalVentas + cd.TotalEgresos,
                TotalEgresos = cd.TotalEgresos,
                TotalNeto = cd.TotalNeto,
                DesglosePorMetodo = desglosePorMetodo,
                DetalleTurnos = detalleTurnos,
                Observaciones = cd.Observaciones
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene el resumen del día actual (turnos, ventas, egresos).
    /// </summary>
    [HttpGet("resumen-dia")]
    [ProducesResponseType(typeof(ResumenDia), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResumenDia(CancellationToken ct)
    {
        var caja = await _context.Cajas.FirstOrDefaultAsync(c => c.IsActive, ct);
        if (caja is null)
            return Ok(new ResumenDia());

        var (fechaContable, _, _) = await GetEstadoCajaAsync(caja.Id, ct);

        var turnosDia = await _context.TurnosCaja
            .Include(t => t.Usuario)
            .Where(t => t.CajaId == caja.Id && t.FechaContable.Date == fechaContable.Date && t.IsActive)
            .ToListAsync(ct);

        var turnoIds = turnosDia.Select(t => t.Id).ToList();

        var totalVentas = turnoIds.Count > 0
            ? await _context.Pagos.Where(p => turnoIds.Contains(p.TurnoCajaId) && p.IsActive).SumAsync(p => p.Monto, ct)
            : 0m;

        var totalEgresos = turnoIds.Count > 0
            ? await _context.MovimientosCaja
                .Where(m => turnoIds.Contains(m.TurnoCajaId) && m.Tipo == TipoMovimientoCaja.Egreso && m.IsActive)
                .SumAsync(m => m.Monto, ct)
            : 0m;

        var turnoActivo = turnosDia.FirstOrDefault(t => t.FechaCierre == null);
        var cierreRealizado = await _context.CierresDiarios
            .AnyAsync(c => c.CajaId == caja.Id && c.Fecha.Date == fechaContable.Date, ct);

        return Ok(new ResumenDia
        {
            Fecha = DateTime.SpecifyKind(fechaContable, DateTimeKind.Utc),
            TotalTurnos = turnosDia.Count,
            TurnosAbiertos = turnosDia.Count(t => t.FechaCierre == null),
            TurnosCerrados = turnosDia.Count(t => t.FechaCierre != null),
            TotalVentas = totalVentas,
            TotalEgresos = totalEgresos,
            TurnoActivoId = turnoActivo?.Id,
            CierreDiarioRealizado = cierreRealizado
        });
    }
}

// ═══════════════════════════════════
// DTOs de Request
// ═══════════════════════════════════

public record AbrirTurnoRequest(
    Guid UsuarioId,
    decimal FondoInicial
);

public record RegistrarEgresoRequest(
    Guid TurnoCajaId,
    decimal Monto,
    string Concepto,
    string? ReferenciaComprobante
);

public record CerrarTurnoRequest(
    Guid TurnoCajaId,
    decimal MontoDeclarado,
    string? Observaciones,
    bool? TransferirMesasAbiertas = false
);

public record CierreDiarioRequest(
    Guid UsuarioId,
    string? Observaciones
);

// ═══════════════════════════════════
// DTOs de Response
// ═══════════════════════════════════

public class TurnoActivoResponse
{
    public Guid TurnoId { get; set; }
    public Guid CajaId { get; set; }
    public string CajaNombre { get; set; } = string.Empty;
    public Guid UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime FechaApertura { get; set; }
    public decimal FondoInicial { get; set; }
    public DateTime FechaContable { get; set; }
    public string Turno { get; set; } = string.Empty;
}

public class DesglosePorMetodo
{
    public Guid MetodoPagoId { get; set; }
    public string MetodoPagoNombre { get; set; } = string.Empty;
    public int CantidadOperaciones { get; set; }
    public decimal Total { get; set; }
}

public class ResultadoCierreTurno
{
    public Guid TurnoCajaId { get; set; }
    public DateTime FechaCierre { get; set; }
    public decimal FondoInicial { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal MontoEsperadoEfectivo { get; set; }
    public decimal MontoDeclarado { get; set; }
    public decimal DiferenciaArqueo { get; set; }
    public List<DesglosePorMetodo> DesglosePorMetodo { get; set; } = [];
    public string? Observaciones { get; set; }
    public bool EsUltimoTurnoDia { get; set; }
    public int TurnosAbiertosDia { get; set; }
}

public class DetalleTurno
{
    public Guid TurnoId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal FondoInicial { get; set; }
    public decimal DiferenciaArqueo { get; set; }
}

public class ResultadoCierreDiario
{
    public Guid CierreId { get; set; }
    public DateTime Fecha { get; set; }
    public string CajaNombre { get; set; } = string.Empty;
    public int TotalTurnos { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal TotalNeto { get; set; }
    public List<DesglosePorMetodo> DesglosePorMetodo { get; set; } = [];
    public List<DetalleTurno> DetalleTurnos { get; set; } = [];
    public string? Observaciones { get; set; }
}

public class ResumenDia
{
    public DateTime Fecha { get; set; } = DateTime.UtcNow.Date;
    public int TotalTurnos { get; set; }
    public int TurnosAbiertos { get; set; }
    public int TurnosCerrados { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalEgresos { get; set; }
    public Guid? TurnoActivoId { get; set; }
    public bool CierreDiarioRealizado { get; set; }
}
