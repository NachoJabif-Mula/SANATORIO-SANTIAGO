using BaresFamilia.Infrastructure;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Local.Api.Hubs;
using BaresFamilia.Local.Api.Workers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// SERVICIO DE WINDOWS
// ========================================
// Permite ejecutar la API como un Windows Service (sc.exe create)
// En desarrollo funciona como consola normal.
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "BaresFamilia Local API";
});

// ========================================
// SERVICIOS
// ========================================

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Registra Infrastructure: LocalContext (PostgreSQL) + Repositorios + Servicios
builder.Services.AddLocalInfrastructure(builder.Configuration);

// HttpClient para comunicación con la API Nube
builder.Services.AddHttpClient("NubeApi", client =>
{
    var baseUrl = builder.Configuration["NubeApi:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
        client.BaseAddress = new Uri(baseUrl);

    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>("NubeApi:TimeoutSeconds", 30));
});

// Motor Local-First: Sincronización en background cada 30 segundos
builder.Services.AddSingleton<SincronizacionWorker>();
builder.Services.AddHostedService<SincronizacionWorker>(sp => sp.GetRequiredService<SincronizacionWorker>());

var app = builder.Build();

// ========================================
// AUTO-MIGRATE: Crea/actualiza tablas al arrancar
// ========================================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LocalContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

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

    // NOTA: Los Métodos de Pago (incluida "Cuenta Corriente") son catálogo maestro
    // de la Nube y llegan a Local únicamente vía sincronización (SincronizacionWorker).
    // Sembrarlos también acá generaba un duplicado con Id distinto al de la Nube,
    // lo que rompía el pull por violar la restricción única de "Nombre".
}

// ========================================
// PIPELINE HTTP & SIGNALR
// ========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.UseCors();
app.MapControllers();
app.MapHub<PrintHub>("/hubs/print");

app.Run();
