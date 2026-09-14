using System.Reflection;
using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure;
using BaresFamilia.Local.Api.Workers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaresFamilia.Tests.Integracion;

/// <summary>
/// Verifica que el contenedor pueda construir cada controlador con el registro real
/// de dependencias de cada API.
///
/// Es la red que atrapa un servicio o repositorio que quedó sin registrar: sin esto,
/// el error recién aparecería al pegarle al endpoint en producción.
/// </summary>
public class CableadoDeDependenciasTests
{
    private static readonly Assembly NubeApi = typeof(BaresFamilia.Nube.Api.Controllers.SyncController).Assembly;
    private static readonly Assembly LocalApi = typeof(BaresFamilia.Local.Api.Controllers.CajaController).Assembly;

    public static TheoryData<Type> ControladoresDeNube => Controladores(NubeApi);
    public static TheoryData<Type> ControladoresLocales => Controladores(LocalApi);

    [Theory]
    [MemberData(nameof(ControladoresDeNube))]
    public void CadaControladorDeLaNube_SeConstruyeConElRegistroReal(Type controlador)
        => AssertSeResuelvenLasDependencias(ConstruirContenedorDeNube(), controlador);

    [Theory]
    [MemberData(nameof(ControladoresLocales))]
    public void CadaControladorLocal_SeConstruyeConElRegistroReal(Type controlador)
        => AssertSeResuelvenLasDependencias(ConstruirContenedorLocal(), controlador);

    [Fact]
    public void ElHubDeImpresion_SeConstruyeConElRegistroReal()
        => AssertSeResuelvenLasDependencias(ConstruirContenedorLocal(), typeof(BaresFamilia.Local.Api.Hubs.PrintHub));

    [Fact]
    public void ElMonitorDeSincronizacion_EsCompartidoEntreRequests()
    {
        var proveedor = ConstruirContenedorDeNube();

        using var primerAlcance = proveedor.CreateScope();
        using var segundoAlcance = proveedor.CreateScope();

        var primero = primerAlcance.ServiceProvider.GetRequiredService<IMonitorSincronizacion>();
        var segundo = segundoAlcance.ServiceProvider.GetRequiredService<IMonitorSincronizacion>();

        // Si no fuese la misma instancia se perdería el estado de sincronización
        // entre una request y la siguiente.
        Assert.Same(primero, segundo);
    }

    // ══════════════════════════════════════════════════════════
    // Armado de los contenedores
    // ══════════════════════════════════════════════════════════

    private static ServiceProvider ConstruirContenedorDeNube()
    {
        var services = ServiciosBase(new Dictionary<string, string?>
        {
            ["ConnectionStrings:NubeConnection"] = "Host=localhost;Database=pruebas;Username=u;Password=p"
        }, out var configuration);

        services.AddNubeInfrastructure(configuration);
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        return services.BuildServiceProvider();
    }

    private static ServiceProvider ConstruirContenedorLocal()
    {
        var services = ServiciosBase(new Dictionary<string, string?>
        {
            ["ConnectionStrings:LocalConnection"] = "Host=localhost;Database=pruebas;Username=u;Password=p",
            ["NubeApi:BaseUrl"] = "http://localhost:5280"
        }, out var configuration);

        services.AddLocalInfrastructure(configuration);

        // El worker es un componente del host, no de Infrastructure: se registra igual
        // que en Program.cs para que el controlador de sincronización pueda resolverse.
        services.AddSingleton<SincronizacionWorker>();
        services.AddSingleton<IMotorSincronizacionLocal>(sp => sp.GetRequiredService<SincronizacionWorker>());

        return services.BuildServiceProvider();
    }

    private static IServiceCollection ServiciosBase(Dictionary<string, string?> valores, out IConfiguration configuration)
    {
        configuration = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddHttpClient();

        return services;
    }

    // ══════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// No se instancia el controlador (necesitaría un HttpContext): alcanza con
    /// comprobar que cada dependencia declarada en su constructor se puede resolver.
    /// </summary>
    private static void AssertSeResuelvenLasDependencias(ServiceProvider proveedor, Type tipo)
    {
        using (proveedor)
        {
            using var alcance = proveedor.CreateScope();

            var constructor = tipo.GetConstructors().Single();

            foreach (var parametro in constructor.GetParameters())
            {
                var resuelto = alcance.ServiceProvider.GetService(parametro.ParameterType);

                Assert.True(
                    resuelto is not null,
                    $"{tipo.Name} pide '{parametro.ParameterType.Name}' pero no está registrado en el contenedor.");
            }
        }
    }

    private static TheoryData<Type> Controladores(Assembly ensamblado)
    {
        var datos = new TheoryData<Type>();

        foreach (var tipo in ensamblado.GetTypes()
                     .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t)))
        {
            datos.Add(tipo);
        }

        return datos;
    }
}
