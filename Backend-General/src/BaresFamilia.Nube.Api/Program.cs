using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Infrastructure;
using BaresFamilia.Infrastructure.Data;
using BaresFamilia.Nube.Api.Extensions;
using BaresFamilia.Nube.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// SERVICIOS
// ========================================

builder.Services.AddControllers();
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

// Registra Infrastructure: NubeContext (PostgreSQL) + Repositorios + Servicios
builder.Services.AddNubeInfrastructure(builder.Configuration);

// ── JWT ──────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
builder.Services.Configure<JwtSettings>(jwtSection);

var jwtSettings = jwtSection.Get<JwtSettings>()
    ?? throw new InvalidOperationException("La sección 'Jwt' no está configurada en appsettings.json.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudiences = new[] { jwtSettings.Audience, "BaresFamilia.Backoffice" },
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // El JWT firmado solo prueba que el token es auténtico y no expiró;
        // no sabe si el dispositivo fue revocado desde el Backoffice ni si una
        // sesión de usuario con "mantener sesión iniciada" fue cerrada. Estas
        // dos cosas viven en la base y hay que revalidarlas en cada request.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal is null)
                {
                    context.Fail("Token inválido.");
                    return;
                }

                var rawToken = context.HttpContext.Request.Headers.LeerTokenBearer();
                if (string.IsNullOrWhiteSpace(rawToken))
                    return;

                var autenticacion = context.HttpContext.RequestServices
                    .GetRequiredService<IAutenticacionBackofficeService>();

                if (principal.FindFirstValue("tipo") == "m2m")
                {
                    // Token M2M de un POS: verificar que el dispositivo siga activo.
                    // RevocarDispositivo() solo marca IsActive=false en la base; sin
                    // este chequeo, el JWT M2M seguiría siendo válido hasta sus 365
                    // días de vigencia aunque el dispositivo ya esté revocado.
                    if (!Guid.TryParse(principal.FindFirstValue("dispositivo_id"), out var dispositivoId))
                    {
                        context.Fail("Token M2M sin dispositivo_id.");
                        return;
                    }

                    if (!await autenticacion.EsDispositivoActivoAsync(dispositivoId))
                        context.Fail("Dispositivo revocado o inexistente.");

                    return;
                }

                // Token de usuario del Backoffice: si se emitió con "mantener sesión
                // iniciada" existe una fila en SesionesUsuario. Las sesiones cortas
                // (sin ese check) nunca se persisten, así que no encontrar fila es
                // normal para ellas y no invalida el token.
                if (!await autenticacion.ValidarSesionVigenteAsync(rawToken))
                    context.Fail("Sesión revocada o expirada.");
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Los tokens M2M de dispositivos POS solo llevan el claim "tipo"="m2m" y
    // nunca un rol de usuario: esta policy los excluye de los endpoints
    // administrativos del Backoffice (catálogo, roles, empleados, fiscal, etc.),
    // que antes aceptaban cualquier JWT válido con el [Authorize] a secas.
    options.AddPolicy("Backoffice", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.Identity?.IsAuthenticated == true &&
            ctx.User.FindFirstValue("tipo") != "m2m"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// ========================================
// AUTO-MIGRATE + SEED: la capa de Infrastructure prepara la base al arrancar
// ========================================
InicializadorNube.Inicializar(app.Services);

// ========================================
// PIPELINE HTTP
// ========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Traduce las excepciones de dominio (regla de negocio / recurso inexistente)
// al código HTTP correspondiente antes de que lleguen al cliente.
app.UseManejadorExcepciones();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
