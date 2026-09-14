using BaresFamilia.Core.Models.Entities.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaresFamilia.Infrastructure.Data;

/// <summary>
/// Puesta a punto de la base de datos de la Nube al arrancar: aplica migraciones
/// pendientes y siembra los datos mínimos sin los que el sistema no puede operar
/// (tipos de ticket, método de pago de cuenta corriente y usuario administrador).
///
/// Vive en Infrastructure porque es la capa dueña del DbContext: la API solo
/// dispara la inicializacion, no conoce el esquema.
/// </summary>
public static class InicializadorNube
{
    /// <summary>
    /// Aplica migraciones y siembra los datos por defecto. Si las migraciones fallan
    /// se cae a EnsureCreated para que un entorno nuevo pueda levantar igual.
    /// </summary>
    public static void Inicializar(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NubeContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<NubeContext>>();

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
                new TipoTicket
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
                new TipoTicket
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
                new TipoTicket
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

        // ── Seed: Ticket de Factura C (emisores monotributistas/exentos) ──
        if (!context.TiposTicket.Any(t => t.Codigo == "FacturaC"))
        {
            logger.LogInformation("Creando tipo de ticket 'FacturaC'...");

            context.TiposTicket.Add(new TipoTicket
            {
                Codigo = "FacturaC",
                Nombre = "Factura C",
                TemplateContenido = """
                    ================================
                           {{NEGOCIO}}
                         FACTURA C
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
            });

            context.SaveChanges();
            logger.LogInformation("✓ Tipo de ticket 'FacturaC' creado.");
        }

        // ── Seed: Método de Pago "Cuenta Corriente" ──────────────
        if (!context.MetodosPago.Any(m => m.EsCuentaCorriente))
        {
            logger.LogInformation("Creando método de pago 'Cuenta Corriente'...");
            context.MetodosPago.Add(new MetodoPago
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
            var sucursalCentral = context.Set<Sucursal>()
                .FirstOrDefault(s => s.Nombre == "Central");

            if (sucursalCentral is null)
            {
                sucursalCentral = new Sucursal
                {
                    Nombre = "Central",
                    Direccion = "Casa Matriz"
                };
                context.Set<Sucursal>().Add(sucursalCentral);
                context.SaveChanges();
            }

            // Crear rol Administrador si no existe
            var rolAdmin = context.Roles.FirstOrDefault(r => r.Nombre == "Administrador");
            if (rolAdmin is null)
            {
                rolAdmin = new Rol
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
            var admin = new Usuario
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
}
