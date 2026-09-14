using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
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
/// Pruebas de integración del cobro de cuentas corrientes, que toca a la vez la
/// cuenta del cliente y el arqueo de la caja.
/// </summary>
public class CuentaCorrienteTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _baseDeDatos = new();
    private readonly LocalContext _context;
    private readonly ICuentaCorrienteService _cuentaCorrienteService;

    private readonly Guid _clienteId = Guid.NewGuid();
    private Guid _turnoAbiertoId;
    private Guid _metodoEfectivoId;
    private Guid _metodoTarjetaId;
    private Guid _metodoCuentaCorrienteId;

    public CuentaCorrienteTests()
    {
        _context = _baseDeDatos.NuevoContextoLocal();

        _cuentaCorrienteService = new CuentaCorrienteService(
            new CuentaCorrienteRepository(_context),
            new GenericRepository<MovimientoCuentaCorriente>(_context),
            new MetodoPagoRepository(_context),
            new TurnoCajaRepository(_context),
            new MovimientoCajaRepository(_context),
            NullLogger<CuentaCorrienteService>.Instance);

        SembrarDatosBase();
    }

    private void SembrarDatosBase()
    {
        var rol = new Rol { Nombre = "Encargado", Permisos = [] };
        var sucursal = new Sucursal { Nombre = "Centro", Direccion = "Calle 1" };
        var usuario = new Usuario
        {
            Nombre = "Marta",
            Email = "marta@baresfamilia.com",
            PasswordHash = "hash",
            PinAcceso = "4321",
            RolId = rol.Id,
            SucursalId = sucursal.Id
        };
        var caja = new Caja { SucursalId = sucursal.Id, Nombre = "Caja Principal", TipoCaja = TipoCaja.Principal };
        var turno = new TurnoCaja
        {
            CajaId = caja.Id,
            UsuarioId = usuario.Id,
            FechaApertura = DateTime.UtcNow,
            FechaContable = DateTime.UtcNow.Date,
            Turno = "AM",
            FondoInicial = 1000m
        };
        _turnoAbiertoId = turno.Id;

        var efectivo = new MetodoPago { Nombre = "Efectivo" };
        var tarjeta = new MetodoPago { Nombre = "Tarjeta Débito", ComisionPorcentaje = 1.5m };
        var cuentaCorriente = new MetodoPago { Nombre = "Cuenta Corriente", EsCuentaCorriente = true };
        _metodoEfectivoId = efectivo.Id;
        _metodoTarjetaId = tarjeta.Id;
        _metodoCuentaCorrienteId = cuentaCorriente.Id;

        var cliente = new Cliente { Id = _clienteId, Nombre = "Juan", Apellido = "Pérez" };

        _context.AddRange(rol, sucursal, usuario, caja, turno, efectivo, tarjeta, cuentaCorriente, cliente);
        _context.CuentasCorrientes.Add(new CuentaCorriente { ClienteId = _clienteId, SaldoActual = 10000m });
        _context.SaveChanges();
    }

    [Fact]
    public async Task Abonar_DescuentaElSaldoYRegistraElMovimiento()
    {
        var resultado = await _cuentaCorrienteService.AbonarAsync(_clienteId, _turnoAbiertoId, _metodoEfectivoId, 4000m);

        Assert.Equal(10000m, resultado.SaldoAnterior);
        Assert.Equal(6000m, resultado.SaldoActual);
        Assert.Equal(4000m, resultado.MontoAbonado);

        using var otroContexto = _baseDeDatos.NuevoContextoLocal();
        Assert.Equal(6000m, otroContexto.CuentasCorrientes.Single().SaldoActual);

        var movimiento = otroContexto.Set<MovimientoCuentaCorriente>().Single();
        Assert.Equal(TipoMovimientoCuentaCorriente.Pago, movimiento.Tipo);
        Assert.Equal(4000m, movimiento.Monto);
        Assert.Contains("Juan Pérez", movimiento.Detalle);
        Assert.Equal(SyncEstado.Pendiente, movimiento.SyncEstado);
    }

    [Fact]
    public async Task Abonar_EnEfectivo_ImpactaElArqueoDeCaja()
    {
        await _cuentaCorrienteService.AbonarAsync(_clienteId, _turnoAbiertoId, _metodoEfectivoId, 4000m);

        using var otroContexto = _baseDeDatos.NuevoContextoLocal();
        var movimientoCaja = otroContexto.MovimientosCaja.Single();

        Assert.Equal(TipoMovimientoCaja.Ingreso, movimientoCaja.Tipo);
        Assert.Equal(4000m, movimientoCaja.Monto);
        Assert.Equal(_turnoAbiertoId, movimientoCaja.TurnoCajaId);
    }

    [Fact]
    public async Task Abonar_ConOtroMetodo_NoTocaElArqueoDeCaja()
    {
        await _cuentaCorrienteService.AbonarAsync(_clienteId, _turnoAbiertoId, _metodoTarjetaId, 4000m);

        using var otroContexto = _baseDeDatos.NuevoContextoLocal();

        // El dinero no entró físicamente a la caja: el arqueo no se altera.
        Assert.Empty(otroContexto.MovimientosCaja);
        Assert.Equal(6000m, otroContexto.CuentasCorrientes.Single().SaldoActual);
    }

    [Fact]
    public async Task Abonar_RechazaPagarUnaCuentaCorrienteConCuentaCorriente()
    {
        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cuentaCorrienteService.AbonarAsync(_clienteId, _turnoAbiertoId, _metodoCuentaCorrienteId, 1000m));

        Assert.Contains("No se puede liquidar una cuenta corriente", error.Message);
    }

    [Fact]
    public async Task Abonar_RechazaUnMontoMayorAlSaldoAdeudado()
    {
        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cuentaCorrienteService.AbonarAsync(_clienteId, _turnoAbiertoId, _metodoEfectivoId, 10001m));

        Assert.Contains("supera el saldo adeudado", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task Abonar_RechazaMontosNoPositivos(decimal monto)
        => await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cuentaCorrienteService.AbonarAsync(_clienteId, _turnoAbiertoId, _metodoEfectivoId, monto));

    [Fact]
    public async Task Abonar_RequiereUnTurnoDeCajaAbierto()
    {
        var turno = _context.TurnosCaja.Single();
        turno.FechaCierre = DateTime.UtcNow;
        _context.SaveChanges();

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _cuentaCorrienteService.AbonarAsync(_clienteId, _turnoAbiertoId, _metodoEfectivoId, 1000m));

        Assert.Contains("turno de caja activo", error.Message);
    }

    [Fact]
    public async Task Abonar_FallaSiElClienteNoTieneCuentaCorriente()
        => await Assert.ThrowsAsync<RecursoNoEncontradoException>(
            () => _cuentaCorrienteService.AbonarAsync(Guid.NewGuid(), _turnoAbiertoId, _metodoEfectivoId, 100m));

    [Fact]
    public async Task ElResumen_DevuelveElSaldoYLosMovimientosDelMasNuevoAlMasViejo()
    {
        await _cuentaCorrienteService.AbonarAsync(_clienteId, _turnoAbiertoId, _metodoEfectivoId, 1000m);
        await _cuentaCorrienteService.AbonarAsync(_clienteId, _turnoAbiertoId, _metodoTarjetaId, 2000m);

        var resumen = await _cuentaCorrienteService.GetResumenPorClienteAsync(_clienteId);

        Assert.Equal(7000m, resumen.SaldoActual);
        Assert.Equal(2, resumen.Movimientos.Count);
        Assert.True(resumen.Movimientos[0].CreatedAt >= resumen.Movimientos[1].CreatedAt);
    }

    public void Dispose()
    {
        _context.Dispose();
        _baseDeDatos.Dispose();
    }
}
