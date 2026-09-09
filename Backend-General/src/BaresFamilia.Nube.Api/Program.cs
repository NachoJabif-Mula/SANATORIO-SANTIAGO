using System.Text;
using BaresFamilia.Core.Models.Configuration;
using BaresFamilia.Infrastructure;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ========================================
// AUTO-MIGRATE: Crea/actualiza tablas al arrancar
// ========================================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NubeContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Aplicando migraciones pendientes en la base de datos Nube...");
        context.Database.Migrate();
        logger.LogInformation("Migraciones aplicadas correctamente.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al aplicar migraciones. Intentando EnsureCreated como fallback...");
        context.Database.EnsureCreated();
        logger.LogWarning("Base de datos creada con EnsureCreated (sin historial de migraciones).");
    }

    // ── Seed: Tipos de Ticket por defecto ──────────────────────
    if (!context.TiposTicket.Any())
    {
        logger.LogInformation("Creando tipos de ticket por defecto...");

        context.TiposTicket.AddRange(
            new BaresFamilia.Core.Models.Entities.Catalogo.TipoTicket
            {
                Codigo = "Comanda",
                Nombre = "Comanda de Pedido",
                TemplateContenido = """
                    *** COCINA / BARRA ***
                    ================================
                      COMANDA: {{COMANDA_ID}}
                      MESA: {{MESA}}
                      MOZO: {{MOZO}}
                      HORA: {{HORA}}
                    ================================
                    {{ITEMS}}
                    ================================
                      TOTAL ITEMS: {{TOTAL_ITEMS}}
                    ================================
                    """
            },
            new BaresFamilia.Core.Models.Entities.Catalogo.TipoTicket
            {
                Codigo = "FacturaA",
                Nombre = "Factura A",
                TemplateContenido = """
                    ================================
                           {{NEGOCIO}}
                         FACTURA A
                    ================================
                    Comp: {{COMPROBANTE_NRO}}
                    Fecha: {{FECHA}}  {{HORA}}
                    ================================
                    {{ITEMS}}
                    ================================
                    Subtotal:   ${{SUBTOTAL}}
                    Descuento:  ${{DESCUENTO}}
                    TOTAL:      ${{TOTAL}}
                    ================================
                    Metodo Pago: {{METODO_PAGO}}
                    Monto Pagado: ${{MONTO_PAGADO}}
                    ================================
                    CAE: {{CAE}}
                    Vto CAE: {{CAE_VTO}}
                    ================================
                    """
            },
            new BaresFamilia.Core.Models.Entities.Catalogo.TipoTicket
            {
                Codigo = "FacturaB",
                Nombre = "Factura B",
                TemplateContenido = """
                    ================================
                           {{NEGOCIO}}
                         FACTURA B
                    ================================
                    Comp: {{COMPROBANTE_NRO}}
                    Fecha: {{FECHA}}  {{HORA}}
                    ================================
                    {{ITEMS}}
                    ================================
                    Subtotal:   ${{SUBTOTAL}}
                    Descuento:  ${{DESCUENTO}}
                    TOTAL:      ${{TOTAL}}
                    ================================
                    Metodo Pago: {{METODO_PAGO}}
                    Monto Pagado: ${{MONTO_PAGADO}}
                    ================================
                    CAE: {{CAE}}
                    Vto CAE: {{CAE_VTO}}
                    ================================
                    """
            }
        );

        context.SaveChanges();
        logger.LogInformation("✓ 3 tipos de ticket creados: Comanda, FacturaA, FacturaB.");
    }

    // ── Seed: Método de Pago "Cuenta Corriente" ──────────────
    if (!context.MetodosPago.Any(m => m.EsCuentaCorriente))
    {
        logger.LogInformation("Creando método de pago 'Cuenta Corriente'...");
        context.MetodosPago.Add(new BaresFamilia.Core.Models.Entities.Catalogo.MetodoPago
        {
            Nombre = "Cuenta Corriente",
            ComisionPorcentaje = 0m,
            RequiereFacturaAfip = false,
            EsCuentaCorriente = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.SaveChanges();
        logger.LogInformation("✓ Método de pago 'Cuenta Corriente' creado.");
    }

    // ── Seed: Rol Administrador + Usuario Admin ──────────────
    if (!context.Usuarios.Any(u => u.Email == "admin@baresfamilia.com"))
    {
        logger.LogInformation("Creando usuario administrador por defecto...");

        // Crear sucursal central si no existe
        var sucursalCentral = context.Set<BaresFamilia.Core.Models.Entities.Catalogo.Sucursal>()
            .FirstOrDefault(s => s.Nombre == "Central");

        if (sucursalCentral is null)
        {
            sucursalCentral = new BaresFamilia.Core.Models.Entities.Catalogo.Sucursal
            {
                Nombre = "Central",
                Direccion = "Casa Matriz"
            };
            context.Set<BaresFamilia.Core.Models.Entities.Catalogo.Sucursal>().Add(sucursalCentral);
            context.SaveChanges();
        }

        // Crear rol Administrador si no existe
        var rolAdmin = context.Roles.FirstOrDefault(r => r.Nombre == "Administrador");
        if (rolAdmin is null)
        {
            rolAdmin = new BaresFamilia.Core.Models.Entities.Catalogo.Rol
            {
                Nombre = "Administrador",
                Permisos = new List<string>
                {
                    "dashboard.ver", "catalogo.ver", "catalogo.editar",
                    "sucursales.ver", "sucursales.editar", "inventario.ver",
                    "inventario.editar", "reportes.ver", "config.editar",
                    "usuarios.ver", "usuarios.editar"
                },
                EsGlobal = true
            };
            context.Roles.Add(rolAdmin);
            context.SaveChanges();
        }

        // Crear usuario admin con password hasheado
        var admin = new BaresFamilia.Core.Models.Entities.Catalogo.Usuario
        {
            Nombre = "Administrador",
            Email = "admin@baresfamilia.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            PinAcceso = "0000",
            RolId = rolAdmin.Id,
            SucursalId = sucursalCentral.Id
        };
        context.Usuarios.Add(admin);
        context.SaveChanges();

        logger.LogInformation("✓ Usuario admin creado: admin@baresfamilia.com / Admin123!");
    }
}

// ========================================
// PIPELINE HTTP
// ========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseCors();
app.MapControllers();

app.Run();
