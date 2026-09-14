using BaresFamilia.Core.Models.Contratos.Catalogos;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Core.Models.Services;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Infrastructure.Repositories;
using BaresFamilia.Tests.Infraestructura;
using Xunit;

namespace BaresFamilia.Tests.Integracion;

/// <summary>
/// Pruebas de integración del catálogo: categorías, productos y su grilla de precios.
/// </summary>
public class CatalogoTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _baseDeDatos = new();
    private readonly NubeContext _context;

    private readonly ICategoriaService _categoriaService;
    private readonly IProductoService _productoService;
    private readonly IProductoPrecioService _precioService;
    private readonly ITipoVentaService _tipoVentaService;
    private readonly IMetodoPagoService _metodoPagoService;

    private readonly Guid _sucursalId = Guid.NewGuid();
    private readonly Guid _otraSucursalId = Guid.NewGuid();

    public CatalogoTests()
    {
        _context = _baseDeDatos.NuevoContextoNube();

        var categoriaRepo = new CategoriaRepository(_context);
        var productoRepo = new ProductoRepository(_context);
        var tipoVentaRepo = new TipoVentaRepository(_context);

        _categoriaService = new CategoriaService(categoriaRepo, new GenericRepository<Sucursal>(_context));
        _productoService = new ProductoService(productoRepo, categoriaRepo);
        _precioService = new ProductoPrecioService(new ProductoPrecioRepository(_context), tipoVentaRepo);
        _tipoVentaService = new TipoVentaService(tipoVentaRepo);
        _metodoPagoService = new MetodoPagoService(new MetodoPagoRepository(_context));

        _context.Sucursales.AddRange(
            new Sucursal { Id = _sucursalId, Nombre = "Centro", Direccion = "Calle 1" },
            new Sucursal { Id = _otraSucursalId, Nombre = "Norte", Direccion = "Calle 2" });
        _context.SaveChanges();
    }

    // ══════════════════════════════════════════════════════════
    // Categorías
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CrearCategoria_NormalizaElNombreYLaPersiste()
    {
        var categoria = await _categoriaService.CrearAsync(new Categoria
        {
            SucursalId = _sucursalId,
            Nombre = "   Tragos   ",
            OrdenVisual = 2
        });

        Assert.Equal("Tragos", categoria.Nombre);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();
        Assert.Equal("Tragos", otroContexto.Categorias.Single().Nombre);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CrearCategoria_RechazaNombresVacios(string nombre)
        => await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _categoriaService.CrearAsync(new Categoria { SucursalId = _sucursalId, Nombre = nombre }));

    [Fact]
    public async Task CrearCategoria_RechazaUnaSucursalInexistente()
        => await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _categoriaService.CrearAsync(new Categoria { SucursalId = Guid.NewGuid(), Nombre = "Postres" }));

    [Fact]
    public async Task LasCategorias_SeListanPorSucursalYOrdenVisual()
    {
        await _categoriaService.CrearAsync(new Categoria { SucursalId = _sucursalId, Nombre = "Postres", OrdenVisual = 3 });
        await _categoriaService.CrearAsync(new Categoria { SucursalId = _sucursalId, Nombre = "Tragos", OrdenVisual = 1 });
        await _categoriaService.CrearAsync(new Categoria { SucursalId = _otraSucursalId, Nombre = "Cervezas", OrdenVisual = 1 });

        var deLaSucursal = (await _categoriaService.GetPorSucursalAsync(_sucursalId, incluirInactivas: false)).ToList();

        Assert.Equal(2, deLaSucursal.Count);
        Assert.Equal(["Tragos", "Postres"], deLaSucursal.Select(c => c.Nombre));

        // Sin filtro de sucursal, el backoffice ve el catálogo consolidado.
        var todas = await _categoriaService.GetPorSucursalAsync(null, incluirInactivas: false);
        Assert.Equal(3, todas.Count());
    }

    [Fact]
    public async Task UnaCategoriaDadaDeBaja_SoloApareceSiSeLaPideExplicitamente()
    {
        var categoria = await _categoriaService.CrearAsync(new Categoria { SucursalId = _sucursalId, Nombre = "Vinos" });
        await _categoriaService.DeleteAsync(categoria.Id);

        Assert.Empty(await _categoriaService.GetPorSucursalAsync(_sucursalId, incluirInactivas: false));
        Assert.Single(await _categoriaService.GetPorSucursalAsync(_sucursalId, incluirInactivas: true));

        // Baja lógica: la fila sigue existiendo.
        using var otroContexto = _baseDeDatos.NuevoContextoNube();
        Assert.False(otroContexto.Categorias.Single().IsActive);
    }

    // ══════════════════════════════════════════════════════════
    // Productos
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CrearProducto_ExigeQueLaCategoriaSeaDeLaMismaSucursal()
    {
        var categoriaDeOtraSucursal = await _categoriaService.CrearAsync(
            new Categoria { SucursalId = _otraSucursalId, Nombre = "Cervezas" });

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _productoService.CrearAsync(new Producto
            {
                SucursalId = _sucursalId,
                CategoriaId = categoriaDeOtraSucursal.Id,
                Nombre = "Porrón"
            }));

        Assert.Contains("pertenece a otra sucursal", error.Message);
    }

    [Fact]
    public async Task CrearProducto_RechazaUnaCategoriaInexistente()
        => await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _productoService.CrearAsync(new Producto
            {
                SucursalId = _sucursalId,
                CategoriaId = Guid.NewGuid(),
                Nombre = "Fernet"
            }));

    [Fact]
    public async Task CrearProducto_RechazaNombreVacio()
    {
        var categoria = await _categoriaService.CrearAsync(new Categoria { SucursalId = _sucursalId, Nombre = "Tragos" });

        await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _productoService.CrearAsync(new Producto
            {
                SucursalId = _sucursalId,
                CategoriaId = categoria.Id,
                Nombre = "  "
            }));
    }

    [Fact]
    public async Task UnProductoDadoDeBaja_SigueSiendoConsultablePorSusPrecios()
    {
        var producto = await CrearProductoDePruebaAsync("Gin Tonic");
        await _productoService.DeleteAsync(producto.Id);

        // La consulta con filtro de activos ya no lo devuelve...
        Assert.Null(await _productoService.GetByIdAsync(producto.Id));

        // ...pero la grilla de precios sí debe poder ubicarlo.
        var recuperado = await _productoService.GetPorIdIncluyendoInactivosAsync(producto.Id);
        Assert.NotNull(recuperado);
        Assert.Equal("Gin Tonic", recuperado.Nombre);
    }

    // ══════════════════════════════════════════════════════════
    // Grilla de precios
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GuardarPrecios_DaDeAltaLosEnviados()
    {
        var producto = await CrearProductoDePruebaAsync("Aperol");
        var (salon, delivery) = await CrearTiposDeVentaAsync();

        await _precioService.ReemplazarPreciosDeProductoAsync(producto.Id, _sucursalId,
        [
            new PrecioPorTipoVenta(salon, 5000m),
            new PrecioPorTipoVenta(delivery, 5800m)
        ]);

        var precios = (await _precioService.GetActivosPorProductoAsync(producto.Id)).ToList();

        Assert.Equal(2, precios.Count);
        Assert.Equal(5000m, precios.Single(p => p.TipoVentaId == salon).PrecioVenta);
        Assert.Equal(5800m, precios.Single(p => p.TipoVentaId == delivery).PrecioVenta);
        Assert.All(precios, p => Assert.Equal(_sucursalId, p.SucursalId));
    }

    [Fact]
    public async Task GuardarPrecios_ActualizaLosExistentesYDaDeBajaLosQueNoVienenEnElLote()
    {
        var producto = await CrearProductoDePruebaAsync("Campari");
        var (salon, delivery) = await CrearTiposDeVentaAsync();

        await _precioService.ReemplazarPreciosDeProductoAsync(producto.Id, _sucursalId,
        [
            new PrecioPorTipoVenta(salon, 5000m),
            new PrecioPorTipoVenta(delivery, 5800m)
        ]);

        // El segundo lote solo trae Salón, con otro precio.
        await _precioService.ReemplazarPreciosDeProductoAsync(producto.Id, _sucursalId,
        [
            new PrecioPorTipoVenta(salon, 6000m)
        ]);

        var activos = (await _precioService.GetActivosPorProductoAsync(producto.Id)).ToList();

        var precioSalon = Assert.Single(activos);
        Assert.Equal(salon, precioSalon.TipoVentaId);
        Assert.Equal(6000m, precioSalon.PrecioVenta);

        // El de delivery quedó dado de baja, no borrado, y no se duplicó ninguna fila.
        var todos = (await _precioService.GetTodosPorSucursalAsync(_sucursalId)).ToList();
        Assert.Equal(2, todos.Count);
        Assert.False(todos.Single(p => p.TipoVentaId == delivery).IsActive);
    }

    [Fact]
    public async Task GuardarPrecios_ReactivaUnPrecioDadoDeBajaEnLugarDeDuplicarlo()
    {
        var producto = await CrearProductoDePruebaAsync("Negroni");
        var (salon, _) = await CrearTiposDeVentaAsync();

        await _precioService.ReemplazarPreciosDeProductoAsync(producto.Id, _sucursalId, [new PrecioPorTipoVenta(salon, 5000m)]);
        await _precioService.ReemplazarPreciosDeProductoAsync(producto.Id, _sucursalId, []);
        await _precioService.ReemplazarPreciosDeProductoAsync(producto.Id, _sucursalId, [new PrecioPorTipoVenta(salon, 7000m)]);

        var todos = (await _precioService.GetTodosPorSucursalAsync(_sucursalId)).ToList();

        var precio = Assert.Single(todos);
        Assert.True(precio.IsActive);
        Assert.Equal(7000m, precio.PrecioVenta);
    }

    [Fact]
    public async Task GuardarPrecios_IgnoraTiposDeVentaInexistentesSinAbortarElLote()
    {
        var producto = await CrearProductoDePruebaAsync("Spritz");
        var (salon, _) = await CrearTiposDeVentaAsync();

        await _precioService.ReemplazarPreciosDeProductoAsync(producto.Id, _sucursalId,
        [
            new PrecioPorTipoVenta(salon, 5000m),
            new PrecioPorTipoVenta(Guid.NewGuid(), 9999m)
        ]);

        var precio = Assert.Single(await _precioService.GetActivosPorProductoAsync(producto.Id));
        Assert.Equal(salon, precio.TipoVentaId);
    }

    // ══════════════════════════════════════════════════════════
    // Siembra de catálogos maestros
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task LosTiposDeVenta_SeSiembranLaPrimeraVezQueSeConsultan()
    {
        var tipos = (await _tipoVentaService.GetOrdenadosPorNombreAsync(incluirInactivos: false)).ToList();

        Assert.Equal(3, tipos.Count);
        Assert.Contains(tipos, t => t.Nombre == "Salón");
        Assert.Contains(tipos, t => t.Nombre == "Delivery" && t.AplicaRecargo);

        // Una segunda consulta no vuelve a sembrar.
        Assert.Equal(3, (await _tipoVentaService.GetOrdenadosPorNombreAsync(false)).Count());
    }

    [Fact]
    public async Task LosMetodosDePago_SeSiembranYExigenNombreUnico()
    {
        var metodos = (await _metodoPagoService.GetOrdenadosPorNombreAsync(incluirInactivos: false)).ToList();
        Assert.Equal(5, metodos.Count);

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => _metodoPagoService.CrearAsync(new MetodoPago { Nombre = "Efectivo" }));

        Assert.Contains("Ya existe un método de pago", error.Message);
    }

    [Fact]
    public async Task ActualizarMetodoDePago_PermiteConservarSuPropioNombre()
    {
        await _metodoPagoService.GetOrdenadosPorNombreAsync(false);

        var efectivo = (await _metodoPagoService.GetAllAsync()).Single(m => m.Nombre == "Efectivo");
        efectivo.ComisionPorcentaje = 1m;

        // No debe chocar consigo mismo al validar la unicidad.
        await _metodoPagoService.ActualizarAsync(efectivo);

        using var otroContexto = _baseDeDatos.NuevoContextoNube();
        Assert.Equal(1m, otroContexto.MetodosPago.Single(m => m.Nombre == "Efectivo").ComisionPorcentaje);
    }

    // ══════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════

    private async Task<Producto> CrearProductoDePruebaAsync(string nombre)
    {
        var categoria = _context.Categorias.FirstOrDefault(c => c.SucursalId == _sucursalId)
            ?? await _categoriaService.CrearAsync(new Categoria { SucursalId = _sucursalId, Nombre = "Tragos" });

        return await _productoService.CrearAsync(new Producto
        {
            SucursalId = _sucursalId,
            CategoriaId = categoria.Id,
            Nombre = nombre
        });
    }

    private async Task<(Guid Salon, Guid Delivery)> CrearTiposDeVentaAsync()
    {
        var tipos = (await _tipoVentaService.GetOrdenadosPorNombreAsync(false)).ToList();
        return (tipos.Single(t => t.Nombre == "Salón").Id, tipos.Single(t => t.Nombre == "Delivery").Id);
    }

    public void Dispose()
    {
        _context.Dispose();
        _baseDeDatos.Dispose();
    }
}
