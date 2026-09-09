using BaresFamilia.Core.Models.Entities;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Core.Models.Services;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Infrastructure.Repositories;
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

        // Registrar repositorios y servicios específicos (capas nominales)
        services.AddScoped<IProductoRepository, ProductoRepository>();
        services.AddScoped<IProductoService, ProductoService>();

        // Inventario
        services.AddScoped<IInsumoRepository, InsumoRepository>();
        services.AddScoped<IInsumoService, InsumoService>();
        services.AddScoped<IRecetaRepository, RecetaRepository>();
        services.AddScoped<IRecetaService, RecetaService>();

        // Cuentas Corrientes
        services.AddScoped<IClienteRepository>(sp => new ClienteRepository(sp.GetRequiredService<NubeContext>()));
        services.AddScoped<IClienteService, ClienteService>();

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

        // Registrar repositorios y servicios específicos (capas nominales)
        services.AddScoped<IComandaRepository, ComandaRepository>();
        services.AddScoped<IComandaService, ComandaService>();

        // Impresora de cocina → Servicio de impresión configurable
        services.AddScoped<IImpresoraService, ImpresoraService>();

        // Cuentas Corrientes (para poder escribir Cliente desde Local.Api)
        services.AddScoped<IClienteRepository>(sp => new ClienteRepository(sp.GetRequiredService<LocalContext>()));
        services.AddScoped<IClienteService, ClienteService>();

        return services;
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
    }
}
