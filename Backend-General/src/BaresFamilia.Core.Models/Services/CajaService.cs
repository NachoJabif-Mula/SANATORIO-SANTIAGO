using System.Text.Json;
using BaresFamilia.Core.Models.Contratos.Cajas;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.Extensions.Logging;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio del ciclo de caja de la sucursal.
///
/// La sucursal opera con dos turnos por día contable (AM y PM). El turno PM cierra
/// el día: al cerrarlo se genera automáticamente el cierre diario consolidado.
/// </summary>
public class CajaService : ICajaService
{
    private const string TurnoManana = "AM";
    private const string TurnoTarde = "PM";
    private const string MetodoPagoEfectivo = "Efectivo";

    private readonly ICajaRepository _cajaRepository;
    private readonly ITurnoCajaRepository _turnoCajaRepository;
    private readonly IPagoRepository _pagoRepository;
    private readonly IMovimientoCajaRepository _movimientoCajaRepository;
    private readonly ICierreDiarioRepository _cierreDiarioRepository;
    private readonly IComandaRepository _comandaRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ILogger<CajaService> _logger;

    public CajaService(
        ICajaRepository cajaRepository,
        ITurnoCajaRepository turnoCajaRepository,
        IPagoRepository pagoRepository,
        IMovimientoCajaRepository movimientoCajaRepository,
        ICierreDiarioRepository cierreDiarioRepository,
        IComandaRepository comandaRepository,
        IUsuarioRepository usuarioRepository,
        ILogger<CajaService> logger)
    {
        _cajaRepository = cajaRepository;
        _turnoCajaRepository = turnoCajaRepository;
        _pagoRepository = pagoRepository;
        _movimientoCajaRepository = movimientoCajaRepository;
        _cierreDiarioRepository = cierreDiarioRepository;
        _comandaRepository = comandaRepository;
        _usuarioRepository = usuarioRepository;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════
    // APERTURA Y CONSULTA DE TURNO
    // ═══════════════════════════════════════════════════════

    public async Task<EstadoCaja> GetEstadoAsync(CancellationToken ct = default)
    {
        var caja = await _cajaRepository.GetActivaAsync(ct);
        if (caja is null)
            return new EstadoCaja(DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc), TurnoManana, "SinCaja");

        var estado = await CalcularEstadoDeCajaAsync(caja.Id, ct);
        return estado with { FechaContable = DateTime.SpecifyKind(estado.FechaContable, DateTimeKind.Utc) };
    }

    /// <summary>
    /// Determina qué fecha contable y turno corresponde operar: el turno abierto si
    /// lo hay; si no, el que sigue al último turno cerrado (AM → PM del mismo día,
    /// PM → AM del día siguiente).
    /// </summary>
    private async Task<EstadoCaja> CalcularEstadoDeCajaAsync(Guid cajaId, CancellationToken ct)
    {
        var turnoAbierto = await _turnoCajaRepository.GetAbiertoPorCajaAsync(cajaId, ct);
        if (turnoAbierto is not null)
        {
            var estadoAbierto = turnoAbierto.Turno == TurnoManana ? "AbiertoAM" : "AbiertoPM";
            return new EstadoCaja(turnoAbierto.FechaContable, turnoAbierto.Turno, estadoAbierto);
        }

        var ultimoCerrado = await _turnoCajaRepository.GetUltimoCerradoPorCajaAsync(cajaId, ct);
        if (ultimoCerrado is null)
            return new EstadoCaja(DateTime.UtcNow.Date, TurnoManana, "CerradoEsperandoAM");

        return ultimoCerrado.Turno == TurnoManana
            ? new EstadoCaja(ultimoCerrado.FechaContable, TurnoTarde, "CerradoEsperandoPM")
            : new EstadoCaja(ultimoCerrado.FechaContable.AddDays(1), TurnoManana, "CerradoEsperandoAM");
    }

    public async Task<TurnoActivoResponse?> GetTurnoActivoAsync(CancellationToken ct = default)
    {
        var turno = await _turnoCajaRepository.GetAbiertoConDetallesAsync(ct);
        return turno is null ? null : MapearTurnoActivo(turno, turno.Caja.Nombre, turno.Usuario.Nombre);
    }

    public async Task<TurnoActivoResponse> AbrirTurnoAsync(Guid usuarioId, decimal fondoInicial, CancellationToken ct = default)
    {
        if (fondoInicial < 0)
            throw new ReglaNegocioException("El fondo inicial no puede ser negativo.");

        var usuario = await _usuarioRepository.GetActivoConRolAsync(usuarioId, ct)
            ?? throw new RecursoNoEncontradoException("Usuario no encontrado.");

        var caja = await _cajaRepository.GetActivaAsync(ct) ?? await CrearCajaPrincipalAsync(usuario.SucursalId, ct);

        if (await _turnoCajaRepository.ExisteAbiertoEnCajaAsync(caja.Id, ct))
            throw new ReglaNegocioException("Ya existe un turno abierto para esta caja. Debe cerrarse antes de abrir uno nuevo.");

        var estado = await CalcularEstadoDeCajaAsync(caja.Id, ct);

        var ahora = DateTime.UtcNow;
        var nuevoTurno = await _turnoCajaRepository.AddAsync(new TurnoCaja
        {
            Id = Guid.NewGuid(),
            CajaId = caja.Id,
            UsuarioId = usuarioId,
            FechaApertura = ahora,
            FondoInicial = fondoInicial,
            FechaContable = DateTime.SpecifyKind(estado.FechaContable.Date, DateTimeKind.Utc),
            Turno = estado.Turno,
            SyncEstado = SyncEstado.Pendiente,
            CreatedAt = ahora,
            UpdatedAt = ahora,
            IsActive = true
        }, ct);

        _logger.LogInformation(
            "Turno de caja abierto. TurnoId: {TurnoId}. CajaId: {CajaId}. Usuario: {UsuarioNombre}. FondoInicial: {Fondo}. FechaContable: {FechaContable}. Turno: {Turno}",
            nuevoTurno.Id, caja.Id, usuario.Nombre, fondoInicial, nuevoTurno.FechaContable, nuevoTurno.Turno);

        return MapearTurnoActivo(nuevoTurno, caja.Nombre, usuario.Nombre);
    }

    private async Task<Caja> CrearCajaPrincipalAsync(Guid sucursalId, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        return await _cajaRepository.AddAsync(new Caja
        {
            Id = Guid.NewGuid(),
            SucursalId = sucursalId,
            Nombre = "Caja Principal",
            TipoCaja = TipoCaja.Principal,
            SyncEstado = SyncEstado.Pendiente,
            CreatedAt = ahora,
            UpdatedAt = ahora,
            IsActive = true
        }, ct);
    }

    private static TurnoActivoResponse MapearTurnoActivo(TurnoCaja turno, string cajaNombre, string usuarioNombre)
        => new()
        {
            TurnoId = turno.Id,
            CajaId = turno.CajaId,
            CajaNombre = cajaNombre,
            UsuarioId = turno.UsuarioId,
            UsuarioNombre = usuarioNombre,
            FechaApertura = turno.FechaApertura,
            FondoInicial = turno.FondoInicial,
            FechaContable = DateTime.SpecifyKind(turno.FechaContable, DateTimeKind.Utc),
            Turno = turno.Turno
        };

    // ═══════════════════════════════════════════════════════
    // EGRESOS / PAGO A PROVEEDORES
    // ═══════════════════════════════════════════════════════

    public async Task<MovimientoCaja> RegistrarEgresoAsync(
        Guid turnoCajaId,
        decimal monto,
        string concepto,
        string? referenciaComprobante,
        CancellationToken ct = default)
    {
        if (monto <= 0)
            throw new ReglaNegocioException("El monto del egreso debe ser mayor a 0.");

        if (string.IsNullOrWhiteSpace(concepto))
            throw new ReglaNegocioException("El concepto del egreso es obligatorio.");

        var turnoCaja = await _turnoCajaRepository.GetByIdAsync(turnoCajaId, ct)
            ?? throw RecursoNoEncontradoException.ParaId("Turno de caja", turnoCajaId);

        if (turnoCaja.FechaCierre is not null)
            throw new ReglaNegocioException("El turno de caja ya está cerrado. No se pueden registrar más movimientos.");

        var ahora = DateTime.UtcNow;
        var creado = await _movimientoCajaRepository.AddAsync(new MovimientoCaja
        {
            TurnoCajaId = turnoCajaId,
            Tipo = TipoMovimientoCaja.Egreso,
            Monto = monto,
            Concepto = concepto.Trim(),
            ReferenciaComprobante = referenciaComprobante?.Trim(),
            SyncEstado = SyncEstado.Pendiente,
            CreatedAt = ahora,
            UpdatedAt = ahora
        }, ct);

        _logger.LogInformation(
            "Egreso registrado: {MovimientoId}. TurnoCaja: {TurnoCajaId}. Monto: {Monto}. Concepto: {Concepto}",
            creado.Id, turnoCajaId, monto, concepto);

        return creado;
    }

    // ═══════════════════════════════════════════════════════
    // ARQUEO Y CIERRE DE TURNO (CAJA CIEGA)
    // ═══════════════════════════════════════════════════════

    public async Task<ResultadoCierreTurno> GetResumenTurnoAsync(Guid turnoId, CancellationToken ct = default)
    {
        var turnoCaja = await _turnoCajaRepository.GetConDetallesAsync(turnoId, ct)
            ?? throw RecursoNoEncontradoException.ParaId("Turno de caja", turnoId);

        var arqueo = await CalcularArqueoAsync(turnoCaja, ct);

        return new ResultadoCierreTurno
        {
            TurnoCajaId = turnoCaja.Id,
            FechaCierre = turnoCaja.FechaCierre ?? DateTime.UtcNow,
            FondoInicial = turnoCaja.FondoInicial,
            TotalVentas = arqueo.TotalVentas,
            TotalIngresos = arqueo.TotalIngresos,
            TotalEgresos = arqueo.TotalEgresos,
            MontoEsperadoEfectivo = arqueo.MontoEsperadoEfectivo,
            MontoDeclarado = 0,
            DiferenciaArqueo = 0,
            DesglosePorMetodo = arqueo.DesglosePorMetodo,
            Observaciones = null,
            EsUltimoTurnoDia = turnoCaja.Turno == TurnoTarde,
            TurnosAbiertosDia = 0
        };
    }

    public async Task<ResultadoCierreTurno> CerrarTurnoAsync(
        Guid turnoCajaId,
        decimal montoDeclarado,
        string? observaciones,
        bool transferirMesasAbiertas,
        CancellationToken ct = default)
    {
        var turnoCaja = await _turnoCajaRepository.GetConDetallesIncluyendoInactivosAsync(turnoCajaId, ct)
            ?? throw RecursoNoEncontradoException.ParaId("Turno de caja", turnoCajaId);

        if (turnoCaja.FechaCierre is not null)
            throw new ReglaNegocioException("Este turno de caja ya fue cerrado.");

        if (montoDeclarado < 0)
            throw new ReglaNegocioException("El monto declarado no puede ser negativo.");

        await ResolverComandasAbiertasAsync(turnoCaja, transferirMesasAbiertas, ct);

        var arqueo = await CalcularArqueoAsync(turnoCaja, ct);
        var diferencia = montoDeclarado - arqueo.MontoEsperadoEfectivo;

        turnoCaja.FechaCierre = DateTime.UtcNow;
        turnoCaja.DiferenciaArqueo = diferencia;
        turnoCaja.SyncEstado = SyncEstado.Pendiente;
        await _turnoCajaRepository.UpdateAsync(turnoCaja, ct);

        if (turnoCaja.Turno == TurnoTarde)
            await GenerarCierreDiarioAutomaticoAsync(turnoCaja, ct);

        _logger.LogInformation(
            "Cierre de turno realizado. TurnoCaja: {TurnoId}. Declarado: {Declarado}. Esperado: {Esperado}. Diferencia: {Diferencia}. Turno: {Turno}",
            turnoCajaId, montoDeclarado, arqueo.MontoEsperadoEfectivo, diferencia, turnoCaja.Turno);

        return new ResultadoCierreTurno
        {
            TurnoCajaId = turnoCaja.Id,
            FechaCierre = turnoCaja.FechaCierre!.Value,
            FondoInicial = turnoCaja.FondoInicial,
            TotalVentas = arqueo.TotalVentas,
            TotalIngresos = arqueo.TotalIngresos,
            TotalEgresos = arqueo.TotalEgresos,
            MontoEsperadoEfectivo = arqueo.MontoEsperadoEfectivo,
            MontoDeclarado = montoDeclarado,
            DiferenciaArqueo = diferencia,
            DesglosePorMetodo = arqueo.DesglosePorMetodo,
            Observaciones = observaciones,
            EsUltimoTurnoDia = turnoCaja.Turno == TurnoTarde,
            TurnosAbiertosDia = turnoCaja.Turno == TurnoTarde ? 0 : 1
        };
    }

    /// <summary>
    /// Un turno no puede cerrarse con mesas abiertas. En el turno AM se admite
    /// arrastrarlas al turno siguiente si el encargado lo confirma; en el PM no,
    /// porque ese turno cierra el día contable.
    /// </summary>
    private async Task ResolverComandasAbiertasAsync(TurnoCaja turnoCaja, bool transferirMesasAbiertas, CancellationToken ct)
    {
        var comandasAbiertas = await _comandaRepository.ContarAbiertasDeTurnoAsync(turnoCaja.FechaContable, turnoCaja.Turno, ct);
        if (comandasAbiertas == 0)
            return;

        if (turnoCaja.Turno == TurnoTarde)
        {
            throw new ReglaNegocioException(
                $"No se puede realizar el cierre de turno PM ni el cierre diario porque existen {comandasAbiertas} comanda(s) abierta(s). Debe cerrarlas todas antes de proceder.",
                "MesasAbiertasPM",
                new Dictionary<string, object?> { ["count"] = comandasAbiertas });
        }

        if (!transferirMesasAbiertas)
        {
            throw new ReglaNegocioException(
                $"Hay {comandasAbiertas} mesa(s) con comandas abiertas en este turno.",
                "MesasAbiertas",
                new Dictionary<string, object?> { ["count"] = comandasAbiertas });
        }

        await TransferirComandasAlTurnoSiguienteAsync(turnoCaja, ct);
    }

    private async Task TransferirComandasAlTurnoSiguienteAsync(TurnoCaja turnoCaja, CancellationToken ct)
    {
        var (fechaSiguiente, turnoSiguiente) = turnoCaja.Turno == TurnoManana
            ? (turnoCaja.FechaContable, TurnoTarde)
            : (turnoCaja.FechaContable.AddDays(1), TurnoManana);

        var comandas = (await _comandaRepository.GetAbiertasDeTurnoAsync(turnoCaja.FechaContable, turnoCaja.Turno, ct)).ToList();

        foreach (var comanda in comandas)
        {
            comanda.FechaContable = DateTime.SpecifyKind(fechaSiguiente, DateTimeKind.Utc);
            comanda.Turno = turnoSiguiente;
            comanda.UpdatedAt = DateTime.UtcNow;
            comanda.SyncEstado = SyncEstado.Pendiente;
        }

        await _comandaRepository.GuardarCambiosAsync(comandas, ct);

        _logger.LogInformation(
            "Se transfirieron {Count} comandas abiertas de {Fecha} {Turno} al turno {NextTurno} de {NextFecha}",
            comandas.Count, turnoCaja.FechaContable, turnoCaja.Turno, turnoSiguiente, fechaSiguiente);
    }

    /// <summary>
    /// Totales calculados de un turno. El efectivo esperado en caja es el fondo
    /// inicial más lo cobrado en efectivo y los ingresos, menos los egresos.
    /// </summary>
    private sealed record ArqueoTurno(
        decimal TotalVentas,
        decimal TotalIngresos,
        decimal TotalEgresos,
        decimal MontoEsperadoEfectivo,
        List<DesglosePorMetodo> DesglosePorMetodo);

    private async Task<ArqueoTurno> CalcularArqueoAsync(TurnoCaja turnoCaja, CancellationToken ct)
    {
        var desglosePorMetodo = await _pagoRepository.GetDesglosePorMetodoDeTurnoAsync(turnoCaja.Id, ct);
        var movimientos = (await _movimientoCajaRepository.GetPorTurnoAsync(turnoCaja.Id, ct)).ToList();

        var totalVentas = desglosePorMetodo.Sum(p => p.Total);
        var totalIngresos = movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Monto);
        var totalEgresos = movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Monto);

        var totalEfectivo = desglosePorMetodo
            .Where(p => p.MetodoPagoNombre.Equals(MetodoPagoEfectivo, StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Total);

        var montoEsperadoEfectivo = turnoCaja.FondoInicial + totalEfectivo + totalIngresos - totalEgresos;

        return new ArqueoTurno(totalVentas, totalIngresos, totalEgresos, montoEsperadoEfectivo, desglosePorMetodo);
    }

    // ═══════════════════════════════════════════════════════
    // CIERRE DIARIO
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// El cierre diario automático no debe tumbar el cierre de turno: si falla
    /// (por ejemplo porque el día ya estaba cerrado) se registra y se sigue.
    /// </summary>
    private async Task GenerarCierreDiarioAutomaticoAsync(TurnoCaja turnoCaja, CancellationToken ct)
    {
        try
        {
            await ConsolidarCierreDiarioAsync(
                turnoCaja.CajaId,
                turnoCaja.FechaContable,
                turnoCaja.UsuarioId,
                "Cierre Diario Automático por cierre de turno PM.",
                ct);

            _logger.LogInformation(
                "Cierre diario automático generado con éxito para Caja: {CajaId}, FechaContable: {Fecha}",
                turnoCaja.CajaId, turnoCaja.FechaContable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al generar cierre diario automático para Caja: {CajaId}, FechaContable: {Fecha}",
                turnoCaja.CajaId, turnoCaja.FechaContable);
        }
    }

    public async Task<ResultadoCierreDiario> GenerarCierreDiarioAsync(Guid usuarioId, string? observaciones, CancellationToken ct = default)
    {
        var caja = await _cajaRepository.GetActivaAsync(ct)
            ?? throw new ReglaNegocioException("No hay caja configurada.");

        var estado = await CalcularEstadoDeCajaAsync(caja.Id, ct);
        var fechaContable = estado.FechaContable;

        var comandasAbiertas = await _comandaRepository.ContarAbiertasDeFechaAsync(fechaContable, ct);
        if (comandasAbiertas > 0)
        {
            throw new ReglaNegocioException(
                $"No se puede realizar el cierre diario porque existen {comandasAbiertas} comanda(s) abierta(s) para la fecha contable {fechaContable:dd/MM/yyyy}.",
                "MesasAbiertasDiario",
                new Dictionary<string, object?> { ["count"] = comandasAbiertas });
        }

        var cierre = await ConsolidarCierreDiarioAsync(caja.Id, fechaContable, usuarioId, observaciones, ct);

        var turnosDia = (await _turnoCajaRepository.GetDeFechaContableAsync(caja.Id, fechaContable, ct)).ToList();
        var turnoIds = turnosDia.Select(t => t.Id).ToList();

        return new ResultadoCierreDiario
        {
            CierreId = cierre.Id,
            Fecha = cierre.Fecha,
            CajaNombre = caja.Nombre,
            TotalTurnos = turnosDia.Count,
            TotalVentas = cierre.TotalVentas,
            TotalIngresos = cierre.TotalNeto - cierre.TotalVentas + cierre.TotalEgresos,
            TotalEgresos = cierre.TotalEgresos,
            TotalNeto = cierre.TotalNeto,
            DesglosePorMetodo = await _pagoRepository.GetDesglosePorMetodoDeTurnosAsync(turnoIds, ct),
            DetalleTurnos = turnosDia.Select(MapearDetalleTurno).ToList(),
            Observaciones = cierre.Observaciones
        };
    }

    /// <summary>
    /// Consolida y persiste el cierre diario de una fecha contable. Es idempotente
    /// por diseño: si el día ya fue cerrado, lo rechaza en lugar de duplicarlo.
    /// </summary>
    private async Task<CierreDiario> ConsolidarCierreDiarioAsync(
        Guid cajaId,
        DateTime fechaContable,
        Guid usuarioId,
        string? observaciones,
        CancellationToken ct)
    {
        if (await _cierreDiarioRepository.ExisteParaCajaYFechaAsync(cajaId, fechaContable, ct))
            throw new ReglaNegocioException($"Ya se realizó el cierre diario para la fecha contable {fechaContable:dd/MM/yyyy}.");

        var turnosDia = (await _turnoCajaRepository.GetDeFechaContableAsync(cajaId, fechaContable, ct)).ToList();
        if (turnosDia.Count == 0)
            throw new ReglaNegocioException($"No hay turnos registrados para la fecha contable {fechaContable:dd/MM/yyyy}.");

        var turnoIds = turnosDia.Select(t => t.Id).ToList();

        var totalVentas = await _pagoRepository.GetTotalDeTurnosAsync(turnoIds, ct);
        var desglosePorMetodo = await _pagoRepository.GetDesglosePorMetodoDeTurnosAsync(turnoIds, ct);

        var movimientosDia = (await _movimientoCajaRepository.GetPorTurnosAsync(turnoIds, ct)).ToList();
        var totalEgresos = movimientosDia.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Monto);
        var totalIngresos = movimientosDia.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Monto);
        var totalNeto = totalVentas + totalIngresos - totalEgresos;

        var detalleTurnos = turnosDia.Select(MapearDetalleTurno).ToList();

        var resumenJson = JsonSerializer.Serialize(new
        {
            turnos = detalleTurnos,
            desglosePorMetodo,
            totalVentas,
            totalEgresos,
            totalIngresos,
            totalNeto
        });

        var ahora = DateTime.UtcNow;
        return await _cierreDiarioRepository.AddAsync(new CierreDiario
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
            CreatedAt = ahora,
            UpdatedAt = ahora,
            IsActive = true
        }, ct);
    }

    private static DetalleTurno MapearDetalleTurno(TurnoCaja turno)
        => new()
        {
            TurnoId = turno.Id,
            UsuarioNombre = turno.Usuario?.Nombre ?? "—",
            FechaApertura = turno.FechaApertura,
            FechaCierre = turno.FechaCierre,
            FondoInicial = turno.FondoInicial,
            DiferenciaArqueo = turno.DiferenciaArqueo ?? 0
        };

    public async Task<ResumenDia> GetResumenDiaAsync(CancellationToken ct = default)
    {
        var caja = await _cajaRepository.GetActivaAsync(ct);
        if (caja is null)
            return new ResumenDia();

        var estado = await CalcularEstadoDeCajaAsync(caja.Id, ct);
        var fechaContable = estado.FechaContable;

        var turnosDia = (await _turnoCajaRepository.GetDeFechaContableAsync(caja.Id, fechaContable, ct)).ToList();
        var turnoIds = turnosDia.Select(t => t.Id).ToList();

        var totalVentas = await _pagoRepository.GetTotalDeTurnosAsync(turnoIds, ct);
        var movimientosDia = await _movimientoCajaRepository.GetPorTurnosAsync(turnoIds, ct);
        var totalEgresos = movimientosDia.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Monto);

        return new ResumenDia
        {
            Fecha = DateTime.SpecifyKind(fechaContable, DateTimeKind.Utc),
            TotalTurnos = turnosDia.Count,
            TurnosAbiertos = turnosDia.Count(t => t.FechaCierre == null),
            TurnosCerrados = turnosDia.Count(t => t.FechaCierre != null),
            TotalVentas = totalVentas,
            TotalEgresos = totalEgresos,
            TurnoActivoId = turnosDia.FirstOrDefault(t => t.FechaCierre == null)?.Id,
            CierreDiarioRealizado = await _cierreDiarioRepository.ExisteParaCajaYFechaAsync(caja.Id, fechaContable, ct)
        };
    }
}
