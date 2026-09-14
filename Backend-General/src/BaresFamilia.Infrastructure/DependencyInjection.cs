using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Core.Models.Entities;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Fiscal;
using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Core.Models.Services;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Infrastructure.Repositories;
using BaresFamilia.Infrastructure.Sincronizacion;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaresFamilia.Infrastructure;

/// <summary>
/// Extensiones de registro de dependencias para la capa de Infrastructure.
/// Registra DbContexts, repositorios y servicios genéricos.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra NubeContext (PostgreSQL) con todos los repositorios y servicios.
    /// </summary>
    public static IServiceCollection AddNubeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NubeContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("NubeConnection"),
                b => b.MigrationsAssembly(typeof(NubeContext).Assembly.FullName)));

        // Registrar repositorios y servicios genéricos usando NubeContext como DbContext
        RegisterRepositoriesAndServices<NubeContext>(services);

        // Catálogo (categorías, productos, precios, tipos de venta, métodos de pago)
        RegistrarCatalogo<NubeContext>(services);

        // Transaccional (caja, turnos, pagos, movimientos, cierres y comandas)
        RegistrarTransaccional<NubeContext>(services);

        AddProteccionFiscal(services, configuration, "BaresFamilia.Nube");
        AddClientesArca(services, configuration);

        // Inventario
        services.AddScoped<IInsumoRepository, InsumoRepository>();
        services.AddScoped<IInsumoService, InsumoService>();
        services.AddScoped<IRecetaRepository, RecetaRepository>();
        services.AddScoped<IRecetaService, RecetaService>();

        // Sincronización: el monitor es singleton porque su estado operativo
        // (actividad, intervalos, dispositivos en línea) se comparte entre requests.
        services.AddSingleton<IMonitorSincronizacion, MonitorSincronizacion>();
        services.AddScoped<ISincronizacionNubeRepository>(sp => new SincronizacionNubeRepository(sp.GetRequiredService<NubeContext>()));
        services.AddScoped<ISincronizacionNubeService, SincronizacionNubeService>();

        // Backoffice: empleados, autenticación y activación de dispositivos POS
        services.AddScoped<IEmpleadoService, EmpleadoService>();
        services.AddScoped<IActivacionDispositivoService, ActivacionDispositivoService>();
        services.AddScoped<ISesionUsuarioRepository>(sp => new SesionUsuarioRepository(sp.GetRequiredService<NubeContext>()));
        services.AddScoped<IAutenticacionBackofficeService, AutenticacionBackofficeService>();

        // Reportería consolidada del Backoffice
        services.AddScoped<IReporteVentasRepository>(sp => new ReporteVentasRepository(sp.GetRequiredService<NubeContext>()));
        services.AddScoped<IReporteVentasService, ReporteVentasService>();
        services.AddScoped<ICierreDiarioService, CierreDiarioService>();
        services.AddScoped<IDashboardRepository>(sp => new DashboardRepository(sp.GetRequiredService<NubeContext>()));
        services.AddScoped<IDashboardService, DashboardService>();

        // Cuentas Corrientes
        services.AddScoped<IClienteRepository>(sp => new ClienteRepository(sp.GetRequiredService<NubeContext>()));
        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<ICuentaCorrienteRepository>(sp => new CuentaCorrienteRepository(sp.GetRequiredService<NubeContext>()));
        services.AddScoped<ICuentaCorrienteService, CuentaCorrienteService>();

        return services;
    }

    /// <summary>
    /// Registra LocalContext (PostgreSQL) con todos los repositorios y servicios.
    /// </summary>
    public static IServiceCollection AddLocalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LocalContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("LocalConnection"),
                b => b.MigrationsAssembly(typeof(LocalContext).Assembly.FullName)));

        // Registrar repositorios y servicios genéricos usando LocalContext como DbContext
        RegisterRepositoriesAndServices<LocalContext>(services);

        // Catálogo (categorías, productos, precios, tipos de venta, métodos de pago)
        RegistrarCatalogo<LocalContext>(services);

        // Transaccional (caja, turnos, pagos, movimientos, cierres y comandas)
        RegistrarTransaccional<LocalContext>(services);

        AddProteccionFiscal(services, configuration, "BaresFamilia.Local");
        AddClientesArca(services, configuration);

        // Punto de venta: comandas, ciclo de caja, catálogo y acceso por PIN
        services.AddScoped<IComandaService, ComandaService>();
        services.AddScoped<ICajaService, CajaService>();
        services.AddScoped<ICatalogoPosService, CatalogoPosService>();
        services.AddScoped<IAutenticacionPosService, AutenticacionPosService>();

        // Motor de sincronización de la sucursal
        services.AddScoped<ISincronizacionLocalRepository>(sp => new SincronizacionLocalRepository(sp.GetRequiredService<LocalContext>()));

        // Activación del POS contra la Nube
        services.AddHttpClient<IActivacionNubeClient, ActivacionNubeClient>(client =>
        {
            var baseUrl = configuration["NubeApi:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<IActivacionPosService, ActivacionPosService>();

        // Impresora de cocina → Servicio de impresión configurable
        services.AddScoped<IImpresoraService, ImpresoraService>();

        // Cuentas Corrientes (para poder escribir Cliente desde Local.Api)
        services.AddScoped<IClienteRepository>(sp => new ClienteRepository(sp.GetRequiredService<LocalContext>()));
        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<ICuentaCorrienteRepository>(sp => new CuentaCorrienteRepository(sp.GetRequiredService<LocalContext>()));
        services.AddScoped<ICuentaCorrienteService, CuentaCorrienteService>();

        return services;
    }

    /// <summary>
    /// Registra los repositorios y servicios específicos del catálogo sobre el
    /// TContext indicado. Los repositorios reciben el DbContext por abstracción,
    /// así la misma clase sirve tanto a la Nube como a la sucursal.
    /// </summary>
    private static void RegistrarCatalogo<TContext>(IServiceCollection services) where TContext : DbContext
    {
        services.AddScoped<ICategoriaRepository>(sp => new CategoriaRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<ICategoriaService, CategoriaService>();

        services.AddScoped<IProductoRepository>(sp => new ProductoRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<IProductoService, ProductoService>();

        services.AddScoped<IProductoPrecioRepository>(sp => new ProductoPrecioRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<IProductoPrecioService, ProductoPrecioService>();

        services.AddScoped<ITipoVentaRepository>(sp => new TipoVentaRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<ITipoVentaService, TipoVentaService>();

        services.AddScoped<IMetodoPagoRepository>(sp => new MetodoPagoRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<IMetodoPagoService, MetodoPagoService>();

        services.AddScoped<IMesaRepository>(sp => new MesaRepository(sp.GetRequiredService<TContext>()));
    }

    /// <summary>
    /// Registra los repositorios específicos del área transaccional sobre el TContext
    /// indicado. Ambas APIs los comparten: la sucursal los usa para operar y la Nube
    /// para consolidar y reportar.
    /// </summary>
    private static void RegistrarTransaccional<TContext>(IServiceCollection services) where TContext : DbContext
    {
        services.AddScoped<ICajaRepository>(sp => new CajaRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<ITurnoCajaRepository>(sp => new TurnoCajaRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<IPagoRepository>(sp => new PagoRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<IMovimientoCajaRepository>(sp => new MovimientoCajaRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<ICierreDiarioRepository>(sp => new CierreDiarioRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<IComandaRepository>(sp => new ComandaRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<IUsuarioRepository>(sp => new UsuarioRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<IPrintJobRepository>(sp => new PrintJobRepository(sp.GetRequiredService<TContext>()));
        services.AddScoped<IPrintJobService, PrintJobService>();

        services.AddScoped<IDispositivoActivacionRepository>(sp => new DispositivoActivacionRepository(sp.GetRequiredService<TContext>()));
    }

    /// <summary>
    /// Configura Data Protection para el cifrado del certificado fiscal de cada sucursal.
    /// El anillo de claves se persiste en disco: si se pierde, los certificados guardados
    /// quedan indescifrables y hay que volver a cargarlos desde el Backoffice.
    /// </summary>
    private static void AddProteccionFiscal(IServiceCollection services, IConfiguration configuration, string nombreAplicacion)
    {
        var rutaClaves = configuration["DataProtection:KeyRingPath"];

        var dataProtection = services.AddDataProtection()
                                     .SetApplicationName(nombreAplicacion);

        if (!string.IsNullOrWhiteSpace(rutaClaves))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(rutaClaves));

            // En Windows el anillo se cifra con DPAPI de la máquina; en Linux (contenedores)
            // no hay equivalente disponible y queda protegido por los permisos del volumen.
            if (OperatingSystem.IsWindows())
            {
                dataProtection.ProtectKeysWithDpapi();
            }
        }

        services.AddScoped<IProtectorFiscal, ProtectorFiscal>();
    }

    /// <summary>
    /// Registra los clientes de los Web Services de ARCA. Se usan tanto desde la Nube
    /// (verificación de la configuración de una sucursal) como desde la sucursal (emisión).
    /// </summary>
    private static void AddClientesArca(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AfipSettings>(configuration.GetSection(AfipSettings.SectionName));
        services.AddHttpClient<IWsaaClient, WsaaClient>();
        services.AddHttpClient<IWsfeClient, WsfeClient>();
        services.AddScoped<IFacturacionElectronicaService, FacturacionElectronicaService>();
        services.AddScoped<IConfiguracionFiscalService, ConfiguracionFiscalService>();
    }

    /// <summary>
    /// Registra IRepository{T} -> GenericRepository{T} e IService{T} -> GenericService{T}
    /// para todas las entidades del dominio, usando el TContext especificado.
    /// </summary>
    private static void RegisterRepositoriesAndServices<TContext>(IServiceCollection services) where TContext : DbContext
    {
        // Helper para registrar repositorio + servicio de una entidad
        void Register<TEntity>() where TEntity : BaseEntity
        {
            services.AddScoped<IRepository<TEntity>>(sp =>
                new GenericRepository<TEntity>(sp.GetRequiredService<TContext>()));

            services.AddScoped<IService<TEntity>, GenericService<TEntity>>();
        }

        // Catálogo
        Register<Sucursal>();
        Register<Rol>();
        Register<Usuario>();
        Register<Categoria>();
        Register<Producto>();
        Register<TipoVenta>();
        Register<ProductoPrecio>();
        Register<MetodoPago>();
        Register<Mesa>();
        Register<ConfiguracionPos>();

        // Transaccional
        Register<Caja>();
        Register<TurnoCaja>();
        Register<Comanda>();
        Register<ComandaItem>();
        Register<Pago>();
        Register<MovimientoCaja>();
        Register<CierreDiario>();

        // Seguridad
        Register<DispositivoActivacion>();

        // Inventario
        Register<Insumo>();
        Register<Receta>();
        Register<StockSucursal>();

        // Cuentas Corrientes
        Register<Cliente>();
        Register<CuentaCorriente>();
        Register<MovimientoCuentaCorriente>();

        // Impresión
        Register<Impresora>();
        Register<TipoTicket>();
        Register<ImpresoraTicketTipo>();
        Register<PrintJob>();

        // Facturación Electrónica (ARCA)
        Register<ConfiguracionFiscalSucursal>();
        Register<TicketAccesoWsaa>();
        Register<Comprobante>();
        Register<ComprobanteAlicuota>();
    }
}
