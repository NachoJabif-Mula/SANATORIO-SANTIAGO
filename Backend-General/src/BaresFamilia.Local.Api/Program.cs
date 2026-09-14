using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Local.Api.Hubs;
using BaresFamilia.Local.Api.Middleware;
using BaresFamilia.Local.Api.Workers;

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
builder.Services.AddSingleton<IMotorSincronizacionLocal>(sp => sp.GetRequiredService<SincronizacionWorker>());

var app = builder.Build();

// ========================================
// AUTO-MIGRATE: la capa de Infrastructure prepara la base al arrancar
// ========================================
InicializadorLocal.Inicializar(app.Services);

// ========================================
// PIPELINE HTTP & SIGNALR
// ========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Traduce las excepciones de dominio (regla de negocio / recurso inexistente)
// al código HTTP correspondiente antes de que lleguen al cliente.
app.UseManejadorExcepciones();

app.UseAuthorization();
app.UseCors();
app.MapControllers();
app.MapHub<PrintHub>("/hubs/print");

app.Run();
