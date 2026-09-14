using System.Reflection;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BaresFamilia.Tests;

/// <summary>
/// Verifica que se respete el flujo de capas del sistema:
/// Controller → Interfaz de servicio → Servicio → Interfaz de repositorio → Repositorio → DbContext.
///
/// Son las pruebas que impiden que la separación se vuelva a romper: si alguien
/// vuelve a inyectar un DbContext en un controlador, estas fallan.
/// </summary>
public class ArquitecturaPorCapasTests
{
    private static readonly Assembly NubeApi = typeof(BaresFamilia.Nube.Api.Controllers.SyncController).Assembly;
    private static readonly Assembly LocalApi = typeof(BaresFamilia.Local.Api.Controllers.CajaController).Assembly;
    private static readonly Assembly CoreModels = typeof(IService<>).Assembly;
    private static readonly Assembly Infrastructure = typeof(NubeContext).Assembly;

    public static TheoryData<Type> Controladores => CargarTipos(
        t => typeof(ControllerBase).IsAssignableFrom(t), NubeApi, LocalApi);

    public static TheoryData<Type> Servicios => CargarTipos(
        t => t.Namespace == "BaresFamilia.Core.Models.Services" && !t.IsAbstract, CoreModels);

    public static TheoryData<Type> ComponentesDeFondo => CargarTipos(
        t => typeof(IHostedService).IsAssignableFrom(t) || typeof(Hub).IsAssignableFrom(t), LocalApi, NubeApi);

    // ══════════════════════════════════════════════════════════
    // Los controladores solo hablan con interfaces de servicio
    // ══════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(Controladores))]
    public void UnControlador_NoDebeDependerDelDbContext(Type controlador)
    {
        var dependenciasProhibidas = DependenciasDeConstructor(controlador)
            .Where(EsAccesoADatos)
            .ToList();

        Assert.True(
            dependenciasProhibidas.Count == 0,
            $"{controlador.Name} inyecta acceso a datos directo ({string.Join(", ", dependenciasProhibidas.Select(d => d.Name))}). " +
            "Debe depender de una interfaz de servicio.");
    }

    [Theory]
    [MemberData(nameof(Controladores))]
    public void UnControlador_NoDebeDependerDeImplementacionesConcretas(Type controlador)
    {
        var concretas = DependenciasDeConstructor(controlador)
            .Where(EsComponenteDeNegocioConcreto)
            .ToList();

        Assert.True(
            concretas.Count == 0,
            $"{controlador.Name} depende de implementaciones concretas ({string.Join(", ", concretas.Select(d => d.Name))}). " +
            "Debe depender de sus interfaces.");
    }

    // ══════════════════════════════════════════════════════════
    // Los servicios solo hablan con interfaces de repositorio
    // ══════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(Servicios))]
    public void UnServicio_NoDebeDependerDelDbContext(Type servicio)
    {
        var dependenciasProhibidas = DependenciasDeConstructor(servicio)
            .Where(EsAccesoADatos)
            .ToList();

        Assert.True(
            dependenciasProhibidas.Count == 0,
            $"{servicio.Name} inyecta acceso a datos directo ({string.Join(", ", dependenciasProhibidas.Select(d => d.Name))}). " +
            "La persistencia va en un repositorio.");
    }

    [Fact]
    public void LaCapaDeNegocio_NoDebeReferenciarEntityFramework()
    {
        var referencias = CoreModels.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", referencias);
        Assert.DoesNotContain("Npgsql.EntityFrameworkCore.PostgreSQL", referencias);
    }

    [Fact]
    public void LaCapaDeNegocio_NoDebeReferenciarLaCapaDeInfraestructura()
    {
        var referencias = CoreModels.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain("BaresFamilia.Infrastructure", referencias);
    }

    // ══════════════════════════════════════════════════════════
    // Workers y hubs también respetan las capas
    // ══════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(ComponentesDeFondo))]
    public void UnComponenteDeFondo_NoDebeDependerDelDbContext(Type componente)
    {
        var dependenciasProhibidas = DependenciasDeConstructor(componente)
            .Where(EsAccesoADatos)
            .ToList();

        Assert.True(
            dependenciasProhibidas.Count == 0,
            $"{componente.Name} inyecta acceso a datos directo ({string.Join(", ", dependenciasProhibidas.Select(d => d.Name))}).");
    }

    // ══════════════════════════════════════════════════════════
    // Cada repositorio concreto cumple su interfaz declarada
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void CadaRepositorio_DebeImplementarUnaInterfazDeRepositorio()
    {
        var repositorios = Infrastructure.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Repository"))
            .ToList();

        Assert.NotEmpty(repositorios);

        foreach (var repositorio in repositorios)
        {
            var implementaContrato = repositorio.GetInterfaces()
                .Any(i => i.Assembly == CoreModels && i.Name.StartsWith('I'));

            Assert.True(
                implementaContrato,
                $"{repositorio.Name} no implementa ninguna interfaz de repositorio declarada en Core.Models.");
        }
    }

    [Fact]
    public void CadaServicioDeNegocio_DebeImplementarUnaInterfazDeServicio()
    {
        var servicios = CoreModels.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Service") && t.Namespace == "BaresFamilia.Core.Models.Services")
            .ToList();

        Assert.NotEmpty(servicios);

        foreach (var servicio in servicios)
        {
            var implementaContrato = servicio.GetInterfaces().Any(i => i.Assembly == CoreModels);

            Assert.True(
                implementaContrato,
                $"{servicio.Name} no implementa ninguna interfaz de servicio. Los controladores no deben poder depender de él directamente.");
        }
    }

    // ══════════════════════════════════════════════════════════
    // Los contratos de datos viven en el modelo, no en la API
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void LosDtos_NoDebenDeclararseEnLosEnsambladosDeApi()
    {
        var declaradosEnApi = new[] { NubeApi, LocalApi }
            .SelectMany(a => a.GetTypes())
            .Where(EsContratoDeDatos)
            .Select(t => $"{t.Assembly.GetName().Name}::{t.Name}")
            .ToList();

        Assert.True(
            declaradosEnApi.Count == 0,
            "Estos contratos de datos están declarados en la capa de API y deberían vivir en " +
            $"BaresFamilia.Core.Models.Dtos: {string.Join(", ", declaradosEnApi)}");
    }

    [Fact]
    public void LosDtos_DebenVivirBajoDtosOContratosDelModelo()
    {
        var fueraDeLugar = CoreModels.GetTypes()
            .Where(EsContratoDeDatos)
            .Where(t => !EstaEnDtos(t) && !EstaEnContratos(t))
            .Select(t => $"{t.Namespace}.{t.Name}")
            .ToList();

        Assert.True(
            fueraDeLugar.Count == 0,
            $"Contratos de datos fuera de Dtos/ y Contratos/: {string.Join(", ", fueraDeLugar)}");
    }

    [Fact]
    public void LaCarpetaDtos_SoloLlevaEspejosDeLaBase()
    {
        // Un Request o un Response describe una operación de la API, no una tabla:
        // su lugar es Contratos/.
        var intrusos = CoreModels.GetTypes()
            .Where(EstaEnDtos)
            .Where(t => t.Name.EndsWith("Request") || t.Name.EndsWith("Response"))
            .Select(t => $"{t.Namespace}.{t.Name}")
            .ToList();

        Assert.True(
            intrusos.Count == 0,
            $"Estos tipos son de operación y no espejos de la base, van en Contratos/: {string.Join(", ", intrusos)}");
    }

    [Fact]
    public void LaCarpetaDtos_NoDebeQuedarVacia()
    {
        // Si el filtro dejara de encontrar espejos, la prueba de arriba pasaría sola.
        Assert.NotEmpty(CoreModels.GetTypes().Where(EstaEnDtos));
    }

    private static bool EstaEnDtos(Type tipo)
        => tipo.Namespace?.StartsWith("BaresFamilia.Core.Models.Dtos") == true;

    private static bool EstaEnContratos(Type tipo)
        => tipo.Namespace?.StartsWith("BaresFamilia.Core.Models.Contratos") == true;

    /// <summary>
    /// Un request o response: tipo público de datos, sin comportamiento propio,
    /// reconocible por su sufijo. Se excluyen entidades, servicios y controladores.
    /// </summary>
    private static bool EsContratoDeDatos(Type tipo)
    {
        if (!tipo.IsClass || !tipo.IsPublic || tipo.IsAbstract)
            return false;

        if (typeof(ControllerBase).IsAssignableFrom(tipo) || typeof(Hub).IsAssignableFrom(tipo))
            return false;

        // Las entidades de dominio no son DTOs aunque compartan sufijos.
        if (tipo.Namespace?.Contains(".Entities") == true)
            return false;

        return tipo.Name.EndsWith("Request")
            || tipo.Name.EndsWith("Response")
            || tipo.Name.EndsWith("Dto");
    }

    // ══════════════════════════════════════════════════════════
    // Auto-verificación del guardián
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Controlador deliberadamente mal hecho: existe solo para comprobar que la
    /// detección funciona. Si esta prueba fallara, las de arriba estarían pasando
    /// por vacío y la arquitectura quedaría sin vigilancia.
    /// </summary>
    private sealed class ControladorQueRompeLasCapas : ControllerBase
    {
        public ControladorQueRompeLasCapas(NubeContext context) => _ = context;
    }

    [Fact]
    public void ElGuardian_DetectaUnControladorQueInyectaElDbContext()
    {
        var detectadas = DependenciasDeConstructor(typeof(ControladorQueRompeLasCapas))
            .Where(EsAccesoADatos)
            .ToList();

        Assert.Single(detectadas);
        Assert.Equal(typeof(NubeContext), detectadas[0]);
    }

    [Fact]
    public void ElGuardian_RevisaTodosLosControladoresDeAmbasApis()
    {
        // Si el filtro dejara de encontrar controladores, las pruebas por Theory
        // pasarían sin verificar nada.
        var vigilados = Controladores.Cast<object[]>().Count();

        Assert.True(vigilados > 20, $"Solo se están vigilando {vigilados} controladores; se esperaban los de ambas APIs.");
    }

    // ══════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════

    private static IEnumerable<Type> DependenciasDeConstructor(Type tipo)
        => tipo.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType);

    /// <summary>
    /// Un DbContext, o cualquier tipo declarado en la capa de acceso a datos.
    /// </summary>
    private static bool EsAccesoADatos(Type tipo)
        => typeof(DbContext).IsAssignableFrom(tipo)
            || tipo.Namespace == "BaresFamilia.Infrastructure.Data";

    /// <summary>
    /// Una clase concreta de servicio o repositorio: el controlador debería estar
    /// usando su interfaz en lugar de la implementación.
    /// </summary>
    private static bool EsComponenteDeNegocioConcreto(Type tipo)
        => tipo.IsClass
            && !tipo.IsAbstract
            && (tipo.Assembly == CoreModels || tipo.Assembly == Infrastructure)
            && (tipo.Name.EndsWith("Service") || tipo.Name.EndsWith("Repository"));

    private static TheoryData<Type> CargarTipos(Func<Type, bool> filtro, params Assembly[] ensamblados)
    {
        var datos = new TheoryData<Type>();

        foreach (var tipo in ensamblados.SelectMany(a => a.GetTypes()).Where(t => t.IsClass && !t.IsAbstract).Where(filtro))
            datos.Add(tipo);

        return datos;
    }
}
