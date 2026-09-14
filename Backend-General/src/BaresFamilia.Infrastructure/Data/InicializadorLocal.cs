using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaresFamilia.Infrastructure.Data;

/// <summary>
/// Puesta a punto de la base de datos de la sucursal al arrancar.
///
/// A diferencia de la Nube, acá no se siembra nada: todo el catálogo (incluidos
/// los métodos de pago y la cuenta corriente) es maestro de la Nube y llega
/// únicamente por sincronización. Sembrarlo localmente generaría filas con un Id
/// distinto al de la Nube y rompería el pull al violar la unicidad del nombre.
/// </summary>
public static class InicializadorLocal
{
    /// <summary>
    /// Aplica las migraciones pendientes. Si fallan, cae a EnsureCreated para que
    /// una terminal nueva pueda levantar igual.
    /// </summary>
    public static void Inicializar(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LocalContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LocalContext>>();

        try
        {
            logger.LogInformation("Aplicando migraciones pendientes en la base de datos Local...");
            context.Database.Migrate();
            logger.LogInformation("Migraciones aplicadas correctamente.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al aplicar migraciones. Intentando EnsureCreated como fallback...");
            context.Database.EnsureCreated();
            logger.LogWarning("Base de datos creada con EnsureCreated (sin historial de migraciones).");
        }
    }
}
