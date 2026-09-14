using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Core.Models.Services;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Infrastructure.Repositories;
using BaresFamilia.Tests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BaresFamilia.Tests.Integracion;

/// <summary>
/// Pruebas de integración del ciclo de caja de la sucursal, recorriendo el flujo
/// completo Servicio → Repositorio → base de datos.
/// </summary>
public class CicloDeCajaTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _baseDeDatos = new();
    private readonly LocalContext _context;
    private readonly ICajaService _cajaService;

    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly Guid _sucursalId = Guid.NewGuid();
    private Guid _metodoEfectivoId;

    public CicloDeCajaTests()
    {
        _context = _baseDeDatos.NuevoContextoLocal();
        _cajaService = ConstruirCajaService(_context);
        SembrarDatosBase();
    }

    private static ICajaService ConstruirCajaService(LocalContext context)
        => new CajaService(
            new CajaRepository(context),
            new TurnoCajaRepository(context),
            new PagoRepository(context),
            new MovimientoCajaRepository(context),
            new CierreDiarioRepository(context),
            new ComandaRepository(context),
            new UsuarioRepository(context),
            NullLogger<CajaService>.Instance);

    private void SembrarDatosBase()
    {
        var rol = new Rol { Nombre = "Encargado", Permisos = ["caja.operar"] };
        var sucursal = new Sucursal { Id = _sucursalId, Nombre = "Sucursal Centro", Direccion = "Av. Siempreviva 742" };
        var metodoEfectivo = new MetodoPago { Nombre = "Efectivo", ComisionPorcentaje = 0m };
        _metodoEfectivoId = metodoEfectivo.Id;

        _context.AddRange(rol, sucursal, metodoEfectivo);
        _context.Usuarios.Add(new Usuario
        {
            Id = _usuarioId,
            Nombre = "Marta",
            Email = "marta@baresfamilia.com",
            PasswordHash = "hash",
            PinAcceso = "4321",
            RolId = rol.Id,
            SucursalId = _sucursalId
        });

        _context.SaveChanges();
    }

    // ══════════════════════════════════════════════════════════
    // Apertura de turno
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task AbrirTurno_CreaLaCajaPrincipalCuandoLaSucursalTodaviaNoTieneNinguna()
    {
        var turno = await _cajaService.AbrirTurnoAsync(_usuarioId, fondoInicial: 5000m);

        Assert.Equal("Caja Principal", turno.CajaNombre);
        Assert.Equal("AM", turno.Turno);
        Assert.Equal(5000m, turno.FondoInicial);
        Assert.Equal("Marta", turno.UsuarioNombre);

        // El turno quedó realmente persistido, no solo en el rastreador de cambios.
        using var otroContexto = _baseDeDatos.NuevoContextoLocal();
        Assert.Single(otroContexto.TurnosCaja);
        Assert.Single(otroContexto.Cajas);
    }

    [Fact]
    public async Task AbrirTurno_RechazaUnSegundoTurnoSiYaHayUnoAbierto()
    {
        await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cajaService.AbrirTurnoAsync(_usuarioId, 2000m));

        Assert.Contains("Ya existe un turno abierto", error.Message);
    }

    [Fact]
    public async Task AbrirTurno_RechazaUnFondoInicialNegativo()
        => await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cajaService.AbrirTurnoAsync(_usuarioId, fondoInicial: -1m));

    [Fact]
    public async Task AbrirTurno_RechazaUnUsuarioInexistente()
        => await Assert.ThrowsAsync<RecursoNoEncontradoException>(
            () => _cajaService.AbrirTurnoAsync(Guid.NewGuid(), 1000m));

    [Fact]
    public async Task DespuesDelTurnoAM_ElSiguienteEsPMDelMismoDiaContable()
    {
        var turnoAm = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        await _cajaService.CerrarTurnoAsync(turnoAm.TurnoId, montoDeclarado: 1000m, observaciones: null, transferirMesasAbiertas: false);

        var estado = await _cajaService.GetEstadoAsync();

        Assert.Equal("PM", estado.Turno);
        Assert.Equal("CerradoEsperandoPM", estado.Estado);
        Assert.Equal(turnoAm.FechaContable.Date, estado.FechaContable.Date);
    }

    // ══════════════════════════════════════════════════════════
    // Arqueo
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ElArqueo_SumaFondoInicialVentasEnEfectivoEIngresosYRestaEgresos()
    {
        var turno = await _cajaService.AbrirTurnoAsync(_usuarioId, fondoInicial: 1000m);

        RegistrarVentaEnEfectivo(turno.TurnoId, 2500m);
        await _cajaService.RegistrarEgresoAsync(turno.TurnoId, 300m, "Pago a proveedor", referenciaComprobante: "F-001");

        var resumen = await _cajaService.GetResumenTurnoAsync(turno.TurnoId);

        Assert.Equal(2500m, resumen.TotalVentas);
        Assert.Equal(300m, resumen.TotalEgresos);
        // 1000 de fondo + 2500 cobrado en efectivo - 300 de egreso
        Assert.Equal(3200m, resumen.MontoEsperadoEfectivo);

        var desglose = Assert.Single(resumen.DesglosePorMetodo);
        Assert.Equal("Efectivo", desglose.MetodoPagoNombre);
        Assert.Equal(1, desglose.CantidadOperaciones);
    }

    [Fact]
    public async Task CerrarTurno_CalculaLaDiferenciaEntreLoDeclaradoYLoEsperado()
    {
        var turno = await _cajaService.AbrirTurnoAsync(_usuarioId, fondoInicial: 1000m);
        RegistrarVentaEnEfectivo(turno.TurnoId, 2000m);

        // El encargado declara 50 de menos: faltante de caja.
        var resultado = await _cajaService.CerrarTurnoAsync(turno.TurnoId, montoDeclarado: 2950m, observaciones: "Faltante", transferirMesasAbiertas: false);

        Assert.Equal(3000m, resultado.MontoEsperadoEfectivo);
        Assert.Equal(-50m, resultado.DiferenciaArqueo);

        using var otroContexto = _baseDeDatos.NuevoContextoLocal();
        var persistido = otroContexto.TurnosCaja.Single();
        Assert.NotNull(persistido.FechaCierre);
        Assert.Equal(-50m, persistido.DiferenciaArqueo);
        Assert.Equal(SyncEstado.Pendiente, persistido.SyncEstado);
    }

    [Fact]
    public async Task CerrarTurno_RechazaCerrarDosVecesElMismoTurno()
    {
        var turno = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        await _cajaService.CerrarTurnoAsync(turno.TurnoId, 1000m, null, false);

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cajaService.CerrarTurnoAsync(turno.TurnoId, 1000m, null, false));

        Assert.Contains("ya fue cerrado", error.Message);
    }

    [Fact]
    public async Task RegistrarEgreso_RechazaMontosNoPositivosYConceptosVacios()
    {
        var turno = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);

        await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cajaService.RegistrarEgresoAsync(turno.TurnoId, 0m, "Gasto", null));

        await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cajaService.RegistrarEgresoAsync(turno.TurnoId, 100m, "   ", null));
    }

    [Fact]
    public async Task RegistrarEgreso_RechazaUnTurnoYaCerrado()
    {
        var turno = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        await _cajaService.CerrarTurnoAsync(turno.TurnoId, 1000m, null, false);

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cajaService.RegistrarEgresoAsync(turno.TurnoId, 100m, "Gasto tardío", null));

        Assert.Contains("ya está cerrado", error.Message);
    }

    // ══════════════════════════════════════════════════════════
    // Mesas abiertas al cierre
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CerrarTurnoAM_ConMesasAbiertas_PideConfirmacionAntesDeTransferir()
    {
        var turno = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        CrearComandaAbierta(turno.FechaContable, turno.Turno);

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cajaService.CerrarTurnoAsync(turno.TurnoId, 1000m, null, transferirMesasAbiertas: false));

        Assert.Equal("MesasAbiertas", error.Codigo);
        Assert.Equal(1, error.Detalles["count"]);
    }

    [Fact]
    public async Task CerrarTurnoAM_ConfirmandoLaTransferencia_MueveLasComandasAlTurnoPM()
    {
        var turno = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        var comandaId = CrearComandaAbierta(turno.FechaContable, turno.Turno);

        await _cajaService.CerrarTurnoAsync(turno.TurnoId, 1000m, null, transferirMesasAbiertas: true);

        using var otroContexto = _baseDeDatos.NuevoContextoLocal();
        var comanda = otroContexto.Comandas.Single(c => c.Id == comandaId);

        Assert.Equal("PM", comanda.Turno);
        Assert.Equal(turno.FechaContable.Date, comanda.FechaContable!.Value.Date);
        Assert.Equal(SyncEstado.Pendiente, comanda.SyncEstado);
    }

    [Fact]
    public async Task CerrarTurnoPM_ConMesasAbiertas_NoSePuedeCerrarNiTransferir()
    {
        // Se cierra el AM para que el siguiente turno sea PM.
        var turnoAm = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        await _cajaService.CerrarTurnoAsync(turnoAm.TurnoId, 1000m, null, false);

        var turnoPm = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        Assert.Equal("PM", turnoPm.Turno);

        CrearComandaAbierta(turnoPm.FechaContable, turnoPm.Turno);

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cajaService.CerrarTurnoAsync(turnoPm.TurnoId, 1000m, null, transferirMesasAbiertas: true));

        Assert.Equal("MesasAbiertasPM", error.Codigo);
    }

    // ══════════════════════════════════════════════════════════
    // Cierre diario
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CerrarElTurnoPM_GeneraElCierreDiarioAutomaticamente()
    {
        var turnoAm = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        RegistrarVentaEnEfectivo(turnoAm.TurnoId, 1500m);
        await _cajaService.CerrarTurnoAsync(turnoAm.TurnoId, 2500m, null, false);

        var turnoPm = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        RegistrarVentaEnEfectivo(turnoPm.TurnoId, 2000m);
        await _cajaService.CerrarTurnoAsync(turnoPm.TurnoId, 3000m, null, false);

        using var otroContexto = _baseDeDatos.NuevoContextoLocal();
        var cierre = Assert.Single(otroContexto.CierresDiarios);

        Assert.Equal(3500m, cierre.TotalVentas);
        Assert.Equal(3500m, cierre.TotalNeto);
        Assert.Contains("Cierre Diario Automático", cierre.Observaciones);
    }

    [Fact]
    public async Task GenerarCierreDiario_RechazaHacerloDosVecesParaLaMismaFecha()
    {
        var turno = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        await _cajaService.CerrarTurnoAsync(turno.TurnoId, 1000m, null, false);

        await _cajaService.GenerarCierreDiarioAsync(_usuarioId, "Cierre manual");

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cajaService.GenerarCierreDiarioAsync(_usuarioId, "Cierre repetido"));

        Assert.Contains("Ya se realizó el cierre diario", error.Message);
    }

    [Fact]
    public async Task ResumenDelDia_InformaTurnosVentasYEgresos()
    {
        var turno = await _cajaService.AbrirTurnoAsync(_usuarioId, 1000m);
        RegistrarVentaEnEfectivo(turno.TurnoId, 4000m);
        await _cajaService.RegistrarEgresoAsync(turno.TurnoId, 500m, "Compra de hielo", null);

        var resumen = await _cajaService.GetResumenDiaAsync();

        Assert.Equal(1, resumen.TotalTurnos);
        Assert.Equal(1, resumen.TurnosAbiertos);
        Assert.Equal(0, resumen.TurnosCerrados);
        Assert.Equal(4000m, resumen.TotalVentas);
        Assert.Equal(500m, resumen.TotalEgresos);
        Assert.Equal(turno.TurnoId, resumen.TurnoActivoId);
        Assert.False(resumen.CierreDiarioRealizado);
    }

    [Fact]
    public async Task SinCajaConfigurada_ElEstadoLoInformaEnLugarDeFallar()
    {
        var estado = await _cajaService.GetEstadoAsync();

        Assert.Equal("SinCaja", estado.Estado);
        Assert.Equal("AM", estado.Turno);
    }

    // ══════════════════════════════════════════════════════════
    // Helpers de datos
    // ══════════════════════════════════════════════════════════

    private void RegistrarVentaEnEfectivo(Guid turnoCajaId, decimal monto)
    {
        var comanda = new Comanda
        {
            UsuarioId = _usuarioId,
            TipoVentaId = ObtenerOCrearTipoVenta(),
            Estado = ComandaEstado.Cobrada,
            Subtotal = monto,
            Total = monto
        };

        _context.Comandas.Add(comanda);
        _context.Pagos.Add(new Pago
        {
            ComandaId = comanda.Id,
            TurnoCajaId = turnoCajaId,
            MetodoPagoId = _metodoEfectivoId,
            Monto = monto
        });

        _context.SaveChanges();
    }

    private Guid CrearComandaAbierta(DateTime fechaContable, string turno)
    {
        var comanda = new Comanda
        {
            UsuarioId = _usuarioId,
            TipoVentaId = ObtenerOCrearTipoVenta(),
            Estado = ComandaEstado.Abierta,
            FechaContable = fechaContable,
            Turno = turno,
            Subtotal = 800m,
            Total = 800m
        };

        _context.Comandas.Add(comanda);
        _context.SaveChanges();

        return comanda.Id;
    }

    private Guid ObtenerOCrearTipoVenta()
    {
        var existente = _context.TiposVenta.FirstOrDefault();
        if (existente is not null)
            return existente.Id;

        var tipoVenta = new TipoVenta { Nombre = "Salón", AplicaRecargo = false };
        _context.TiposVenta.Add(tipoVenta);
        _context.SaveChanges();

        return tipoVenta.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _baseDeDatos.Dispose();
    }
}
