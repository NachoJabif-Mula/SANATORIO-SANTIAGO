using BaresFamilia.Core.Models.Dtos.Sincronizacion;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Core.Models.Services;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Infrastructure.Sincronizacion;
using BaresFamilia.Tests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BaresFamilia.Tests.Integracion;

/// <summary>
/// Pruebas de integración de la ingesta de lotes de sincronización en la Nube.
///
/// El objetivo del motor es que la sucursal nunca quede bloqueada: si un dato
/// referenciado todavía no llegó, el registro se saltea o se enlaza de forma
/// degradada, pero el resto del lote entra igual.
/// </summary>
public class SincronizacionNubeTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _baseDeDatos = new();
    private readonly NubeContext _context;
    private readonly ISincronizacionNubeService _sincronizacion;

    private readonly Guid _sucursalId = Guid.NewGuid();
    private Guid _usuarioId;
    private Guid _tipoVentaId;
    private Guid _productoId;
    private Guid _metodoPagoId;
    private Guid _cajaId;
    private Guid _turnoId;

    public SincronizacionNubeTests()
    {
        _context = _baseDeDatos.NuevoContextoNube();

        _sincronizacion = new SincronizacionNubeService(
            new SincronizacionNubeRepository(_context),
            new MonitorSincronizacion(NullLogger<MonitorSincronizacion>.Instance));

        SembrarCatalogoDeLaNube();
    }

    private void SembrarCatalogoDeLaNube()
    {
        var rol = new Rol { Nombre = "Mozo", Permisos = [] };
        var sucursal = new Sucursal { Id = _sucursalId, Nombre = "Centro", Direccion = "Calle 1" };
        var usuario = new Usuario
        {
            Nombre = "Marta",
            Email = "marta@baresfamilia.com",
            PasswordHash = "hash",
            PinAcceso = "1111",
            RolId = rol.Id,
            SucursalId = _sucursalId
        };
        var categoria = new Categoria { SucursalId = _sucursalId, Nombre = "Tragos" };
        var producto = new Producto { SucursalId = _sucursalId, CategoriaId = categoria.Id, Nombre = "Fernet" };
        var tipoVenta = new TipoVenta { Nombre = "Salón" };
        var metodoPago = new MetodoPago { Nombre = "Efectivo" };
        var caja = new Caja { SucursalId = _sucursalId, Nombre = "Caja Principal", TipoCaja = TipoCaja.Principal };
        var turno = new TurnoCaja
        {
            CajaId = caja.Id,
            UsuarioId = usuario.Id,
            FechaApertura = DateTime.UtcNow,
            FechaContable = DateTime.UtcNow.Date,
            Turno = "AM"
        };

        _usuarioId = usuario.Id;
        _tipoVentaId = tipoVenta.Id;
        _productoId = producto.Id;
        _metodoPagoId = metodoPago.Id;
        _cajaId = caja.Id;
        _turnoId = turno.Id;

        _context.AddRange(rol, sucursal, usuario, categoria, producto, tipoVenta, metodoPago, caja, turno);
        _context.SaveChanges();
    }

    // ══════════════════════════════════════════════════════════
    // Ingesta feliz
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task RecibirPayload_PersisteComandasItemsYPagos()
    {
        var comandaId = Guid.NewGuid();

        await _sincronizacion.RecibirPayloadAsync(new SyncPayloadDto
        {
            Comandas = [NuevaComanda(comandaId, total: 5000m)],
            Pagos = [NuevoPago(comandaId, 5000m)]
        }, _sucursalId);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();

        var comanda = otroContexto.Comandas.Single(c => c.Id == comandaId);
        Assert.Equal(ComandaEstado.Cobrada, comanda.Estado);
        Assert.Equal(5000m, comanda.Total);
        Assert.Equal(SyncEstado.Sincronizado, comanda.SyncEstado);

        Assert.Single(otroContexto.ComandaItems.Where(i => i.ComandaId == comandaId));
        Assert.Equal(5000m, otroContexto.Pagos.Single().Monto);
    }

    [Fact]
    public async Task RecibirElMismoLoteDosVeces_NoDuplicaNada()
    {
        var comandaId = Guid.NewGuid();
        var payload = new SyncPayloadDto
        {
            Comandas = [NuevaComanda(comandaId, 5000m)],
            Pagos = [NuevoPago(comandaId, 5000m)],
            Movimientos = [NuevoMovimiento(300m)]
        };

        await _sincronizacion.RecibirPayloadAsync(payload, _sucursalId);
        await _sincronizacion.RecibirPayloadAsync(payload, _sucursalId);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();

        Assert.Single(otroContexto.Comandas);
        Assert.Single(otroContexto.Pagos);
        Assert.Single(otroContexto.MovimientosCaja);
        Assert.Single(otroContexto.ComandaItems);
    }

    [Fact]
    public async Task RecibirPayload_ActualizaUnaComandaYaSincronizada()
    {
        var comandaId = Guid.NewGuid();

        await _sincronizacion.RecibirPayloadAsync(
            new SyncPayloadDto { Comandas = [NuevaComanda(comandaId, 5000m)] }, _sucursalId);

        var comandaAnulada = NuevaComanda(comandaId, 5000m);
        comandaAnulada.Estado = nameof(ComandaEstado.Anulada);

        await _sincronizacion.RecibirPayloadAsync(
            new SyncPayloadDto { Comandas = [comandaAnulada] }, _sucursalId);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();
        Assert.Equal(ComandaEstado.Anulada, otroContexto.Comandas.Single().Estado);
    }

    // ══════════════════════════════════════════════════════════
    // Integridad referencial degradada
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task UnItemConProductoDesconocido_SeSalteaSinTumbarLaComanda()
    {
        var comandaId = Guid.NewGuid();
        var comanda = NuevaComanda(comandaId, 5000m);
        comanda.Items.Add(new SyncComandaItemDto
        {
            Id = Guid.NewGuid(),
            ProductoId = Guid.NewGuid(), // todavía no llegó a la Nube
            Cantidad = 1,
            PrecioUnitario = 900m
        });

        await _sincronizacion.RecibirPayloadAsync(new SyncPayloadDto { Comandas = [comanda] }, _sucursalId);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();

        Assert.Single(otroContexto.Comandas);
        // Solo entró el ítem cuyo producto sí existe.
        Assert.Single(otroContexto.ComandaItems);
    }

    [Fact]
    public async Task UnPagoSinSuComanda_SeSalteaParaReintentarloDespues()
    {
        await _sincronizacion.RecibirPayloadAsync(new SyncPayloadDto
        {
            Pagos = [NuevoPago(Guid.NewGuid(), 5000m)]
        }, _sucursalId);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();
        Assert.Empty(otroContexto.Pagos);
    }

    [Fact]
    public async Task UnMovimientoQueReferenciaUnTurnoInexistente_GeneraElTurnoDeRespaldo()
    {
        var turnoDesconocido = Guid.NewGuid();

        await _sincronizacion.RecibirPayloadAsync(new SyncPayloadDto
        {
            Movimientos =
            [
                new SyncMovimientoDto
                {
                    Id = Guid.NewGuid(),
                    TurnoCajaId = turnoDesconocido,
                    Tipo = nameof(TipoMovimientoCaja.Egreso),
                    Monto = 700m,
                    Concepto = "Pago proveedor",
                    CreatedAt = DateTime.UtcNow
                }
            ]
        }, _sucursalId);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();

        // El movimiento de dinero no se pierde: se crea el turno mínimo que lo sostiene.
        Assert.Single(otroContexto.MovimientosCaja);
        Assert.Contains(otroContexto.TurnosCaja, t => t.Id == turnoDesconocido);
    }

    [Fact]
    public async Task UnaCajaEnviadaPorLaSucursal_SeAltaConSuPropioId()
    {
        var cajaLocalId = Guid.NewGuid();

        await _sincronizacion.RecibirPayloadAsync(new SyncPayloadDto
        {
            Cajas = [new SyncCajaDto { Id = cajaLocalId, Nombre = "Caja Barra", TipoCaja = nameof(TipoCaja.Principal) }]
        }, _sucursalId);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();
        var caja = otroContexto.Cajas.Single(c => c.Id == cajaLocalId);

        Assert.Equal("Caja Barra", caja.Nombre);
        Assert.Equal(_sucursalId, caja.SucursalId);
        Assert.Equal(SyncEstado.Sincronizado, caja.SyncEstado);
    }

    // ══════════════════════════════════════════════════════════
    // Clientes y cuenta corriente
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task UnClienteNuevo_LlegaConSuCuentaCorrienteEnCero()
    {
        var clienteId = Guid.NewGuid();

        await _sincronizacion.RecibirPayloadAsync(new SyncPayloadDto
        {
            Clientes = [new SyncClienteDto { Id = clienteId, Nombre = "Ana", Apellido = "Gómez", CreatedAt = DateTime.UtcNow }]
        }, _sucursalId);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();

        Assert.Equal("Ana", otroContexto.Clientes.Single().Nombre);
        Assert.Equal(0m, otroContexto.CuentasCorrientes.Single(cc => cc.ClienteId == clienteId).SaldoActual);
    }

    [Fact]
    public async Task LosMovimientosDeCuentaCorriente_AplicanElSaldoUnaSolaVez()
    {
        var clienteId = Guid.NewGuid();
        var payload = new SyncPayloadDto
        {
            Clientes = [new SyncClienteDto { Id = clienteId, Nombre = "Ana", Apellido = "Gómez", CreatedAt = DateTime.UtcNow }],
            MovimientosCuentaCorriente =
            [
                new SyncMovimientoCuentaCorrienteDto
                {
                    Id = Guid.NewGuid(),
                    ClienteId = clienteId,
                    Tipo = nameof(TipoMovimientoCuentaCorriente.Cargo),
                    Monto = 3000m,
                    Detalle = "Consumo",
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        await _sincronizacion.RecibirPayloadAsync(payload, _sucursalId);
        await _sincronizacion.RecibirPayloadAsync(payload, _sucursalId);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();

        Assert.Single(otroContexto.Set<MovimientoCuentaCorriente>());
        Assert.Equal(3000m, otroContexto.CuentasCorrientes.Single(cc => cc.ClienteId == clienteId).SaldoActual);
    }

    [Fact]
    public async Task UnPago_RestaDelSaldoDeLaCuentaCorriente()
    {
        var clienteId = Guid.NewGuid();

        await _sincronizacion.RecibirPayloadAsync(new SyncPayloadDto
        {
            Clientes = [new SyncClienteDto { Id = clienteId, Nombre = "Ana", Apellido = "Gómez", CreatedAt = DateTime.UtcNow }],
            MovimientosCuentaCorriente =
            [
                NuevoMovimientoCtaCte(clienteId, TipoMovimientoCuentaCorriente.Cargo, 5000m),
                NuevoMovimientoCtaCte(clienteId, TipoMovimientoCuentaCorriente.Pago, 2000m)
            ]
        }, _sucursalId);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();
        Assert.Equal(3000m, otroContexto.CuentasCorrientes.Single(cc => cc.ClienteId == clienteId).SaldoActual);
    }

    // ══════════════════════════════════════════════════════════
    // Monitoreo
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task LaIngesta_QuedaRegistradaEnElPanelDeSincronizacion()
    {
        var comandaId = Guid.NewGuid();

        await _sincronizacion.RecibirPayloadAsync(new SyncPayloadDto
        {
            Comandas = [NuevaComanda(comandaId, 5000m)],
            Pagos = [NuevoPago(comandaId, 5000m)]
        }, _sucursalId);

        var estadisticas = (await _sincronizacion.GetEstadisticasAsync()).ToList();
        var deLaSucursal = Assert.Single(estadisticas, e => e.SucursalId == _sucursalId);

        Assert.Equal(1, deLaSucursal.Conteos.Comandas);
        Assert.Equal(1, deLaSucursal.Conteos.Pagos);
        Assert.NotNull(deLaSucursal.UltimaActividad.LastPushComandas);
        Assert.NotNull(deLaSucursal.UltimaActividad.LastPushPagos);
    }

    [Fact]
    public void LaSincronizacionForzada_SeConsumeUnaSolaVez()
    {
        _sincronizacion.ForzarSincronizacion(_sucursalId);

        Assert.True(_sincronizacion.ChequearSincronizacionForzada(_sucursalId, Guid.NewGuid()).ForzarSincronizacion);
        Assert.False(_sincronizacion.ChequearSincronizacionForzada(_sucursalId, Guid.NewGuid()).ForzarSincronizacion);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(301)]
    public async Task ConfigurarIntervalo_RechazaValoresFueraDeRango(int segundos)
        => await Assert.ThrowsAsync<BaresFamilia.Core.Models.Exceptions.ReglaNegocioException>(
            () => _sincronizacion.ConfigurarIntervaloAsync(_sucursalId, segundos));

    [Fact]
    public async Task RegistrarReporte_RechazaSucursalesInexistentes()
        => await Assert.ThrowsAsync<BaresFamilia.Core.Models.Exceptions.ReglaNegocioException>(
            () => _sincronizacion.RegistrarReporteAsync(Guid.NewGuid(), "Fantasma", "PUSH", "hola", true));

    [Fact]
    public async Task RegistrarReporte_AcotaTextosDemasiadoLargos()
    {
        await _sincronizacion.RegistrarReporteAsync(_sucursalId, new string('x', 500), "PUSH", new string('y', 5000), true);

        var registro = _sincronizacion.GetRegistrosRecientes(10).First();

        Assert.Equal(200, registro.SucursalNombre.Length);
        Assert.Equal(1000, registro.Mensaje.Length);
    }

    // ══════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════

    private SyncComandaDto NuevaComanda(Guid comandaId, decimal total) => new()
    {
        Id = comandaId,
        TipoVentaId = _tipoVentaId,
        UsuarioId = _usuarioId,
        Estado = nameof(ComandaEstado.Cobrada),
        Subtotal = total,
        Total = total,
        FechaContable = DateTime.UtcNow.Date,
        Turno = "AM",
        CreatedAt = DateTime.UtcNow,
        Items =
        [
            new SyncComandaItemDto
            {
                Id = Guid.NewGuid(),
                ProductoId = _productoId,
                Cantidad = 2,
                PrecioUnitario = total / 2
            }
        ]
    };

    private SyncPagoDto NuevoPago(Guid comandaId, decimal monto) => new()
    {
        Id = Guid.NewGuid(),
        ComandaId = comandaId,
        TurnoCajaId = _turnoId,
        MetodoPagoId = _metodoPagoId,
        Monto = monto,
        CreatedAt = DateTime.UtcNow
    };

    private SyncMovimientoDto NuevoMovimiento(decimal monto) => new()
    {
        Id = Guid.NewGuid(),
        TurnoCajaId = _turnoId,
        Tipo = nameof(TipoMovimientoCaja.Egreso),
        Monto = monto,
        Concepto = "Gasto",
        CreatedAt = DateTime.UtcNow
    };

    private static SyncMovimientoCuentaCorrienteDto NuevoMovimientoCtaCte(
        Guid clienteId, TipoMovimientoCuentaCorriente tipo, decimal monto) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = clienteId,
        Tipo = tipo.ToString(),
        Monto = monto,
        Detalle = tipo.ToString(),
        CreatedAt = DateTime.UtcNow
    };

    public void Dispose()
    {
        _context.Dispose();
        _baseDeDatos.Dispose();
    }
}
