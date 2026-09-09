using BaresFamilia.Core.Models.Entities;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Entities.Transaccional;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Data;

/// <summary>
/// Configuración base compartida entre NubeContext y LocalContext.
/// Contiene la Fluent API para todas las entidades del dominio.
/// </summary>
public static class SharedModelConfiguration
{
    /// <summary>
    /// Aplica la configuración completa de Fluent API al ModelBuilder.
    /// </summary>
    public static void ConfigureModel(ModelBuilder modelBuilder)
    {
        ConfigureBaseEntity<Sucursal>(modelBuilder);
        ConfigureBaseEntity<Rol>(modelBuilder);
        ConfigureBaseEntity<Usuario>(modelBuilder);
        ConfigureBaseEntity<Categoria>(modelBuilder);
        ConfigureBaseEntity<Producto>(modelBuilder);
        ConfigureBaseEntity<TipoVenta>(modelBuilder);
        ConfigureBaseEntity<ProductoPrecio>(modelBuilder);
        ConfigureBaseEntity<MetodoPago>(modelBuilder);
        ConfigureBaseEntity<Mesa>(modelBuilder);
        ConfigureBaseEntity<Caja>(modelBuilder);
        ConfigureBaseEntity<TurnoCaja>(modelBuilder);
        ConfigureBaseEntity<Comanda>(modelBuilder);
        ConfigureBaseEntity<ComandaItem>(modelBuilder);
        ConfigureBaseEntity<Pago>(modelBuilder);
        ConfigureBaseEntity<MovimientoCaja>(modelBuilder);

        ConfigureSucursal(modelBuilder);
        ConfigureRol(modelBuilder);
        ConfigureUsuario(modelBuilder);
        ConfigureCategoria(modelBuilder);
        ConfigureProducto(modelBuilder);
        ConfigureTipoVenta(modelBuilder);
        ConfigureProductoPrecio(modelBuilder);
        ConfigureMetodoPago(modelBuilder);
        ConfigureMesa(modelBuilder);
        ConfigureCaja(modelBuilder);
        ConfigureTurnoCaja(modelBuilder);
        ConfigureComanda(modelBuilder);
        ConfigureComandaItem(modelBuilder);
        ConfigurePago(modelBuilder);
        ConfigureMovimientoCaja(modelBuilder);

        // Caja Diaria
        ConfigureBaseEntity<CierreDiario>(modelBuilder);
        ConfigureCierreDiario(modelBuilder);

        // Seguridad
        ConfigureBaseEntity<DispositivoActivacion>(modelBuilder);
        ConfigureDispositivoActivacion(modelBuilder);

        // ========================
        // INVENTARIO
        // ========================
        ConfigureBaseEntity<Insumo>(modelBuilder);
        ConfigureBaseEntity<Receta>(modelBuilder);
        ConfigureBaseEntity<StockSucursal>(modelBuilder);
        ConfigureInsumo(modelBuilder);
        ConfigureReceta(modelBuilder);
        ConfigureStockSucursal(modelBuilder);

        // ========================
        // CUENTAS CORRIENTES
        // ========================
        ConfigureBaseEntity<Cliente>(modelBuilder);
        ConfigureBaseEntity<CuentaCorriente>(modelBuilder);
        ConfigureBaseEntity<MovimientoCuentaCorriente>(modelBuilder);
        ConfigureCliente(modelBuilder);
        ConfigureCuentaCorriente(modelBuilder);
        ConfigureMovimientoCuentaCorriente(modelBuilder);

        // ========================
        // CONFIGURACIÓN POS
        // ========================
        ConfigureBaseEntity<ConfiguracionPos>(modelBuilder);
        ConfigureConfiguracionPos(modelBuilder);

        // ========================
        // IMPRESIÓN
        // ========================
        ConfigureBaseEntity<Impresora>(modelBuilder);
        ConfigureBaseEntity<TipoTicket>(modelBuilder);
        ConfigureBaseEntity<ImpresoraTicketTipo>(modelBuilder);
        ConfigureImpresora(modelBuilder);
        ConfigureTipoTicket(modelBuilder);
        ConfigureImpresoraTicketTipo(modelBuilder);
    }

    /// <summary>
    /// Configuración base común a todas las entidades: PK, índice IsActive, valores por defecto.
    /// </summary>
    private static void ConfigureBaseEntity<T>(ModelBuilder modelBuilder) where T : BaseEntity
    {
        modelBuilder.Entity<T>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedNever();

            entity.Property(e => e.CreatedAt)
                  .IsRequired();

            entity.Property(e => e.UpdatedAt)
                  .IsRequired();

            entity.Property(e => e.IsActive)
                  .IsRequired()
                  .HasDefaultValue(true);

            // Índice filtrado para consultas que excluyen registros inactivos
            entity.HasIndex(e => e.IsActive)
                  .HasFilter("\"IsActive\" = true");

            // Índice para sincronización basada en timestamp
            entity.HasIndex(e => e.UpdatedAt);
        });
    }

    // ========================
    // DOMINIO CATÁLOGO
    // ========================

    private static void ConfigureSucursal(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sucursal>(entity =>
        {
            entity.ToTable("Sucursales");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(150);

            entity.Property(e => e.Direccion)
                  .IsRequired()
                  .HasMaxLength(300);

            entity.HasIndex(e => e.Nombre)
                  .IsUnique();
        });
    }

    private static void ConfigureRol(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rol>(entity =>
        {
            entity.ToTable("Roles");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(100);

            // Permisos como JSON serializado
            entity.Property(e => e.Permisos)
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                  .HasColumnType("text");

            entity.HasIndex(e => e.Nombre)
                  .IsUnique();
        });
    }

    private static void ConfigureUsuario(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.Email)
                  .IsRequired()
                  .HasMaxLength(250);

            entity.Property(e => e.PasswordHash)
                  .IsRequired()
                  .HasMaxLength(500);

            entity.Property(e => e.PinAcceso)
                  .IsRequired()
                  .HasMaxLength(10);

            entity.HasIndex(e => e.Email)
                  .IsUnique();

            entity.HasIndex(e => e.PinAcceso);

            entity.HasOne(e => e.Rol)
                  .WithMany(r => r.Usuarios)
                  .HasForeignKey(e => e.RolId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Sucursal)
                  .WithMany(s => s.Usuarios)
                  .HasForeignKey(e => e.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCategoria(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.ToTable("Categorias");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.OrdenVisual)
                  .IsRequired();

            entity.HasIndex(e => e.OrdenVisual);
        });
    }

    private static void ConfigureProducto(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.ColorUi)
                  .IsRequired()
                  .HasMaxLength(9);

            entity.Property(e => e.RequiereCocina)
                  .IsRequired();

            entity.HasOne(e => e.Categoria)
                  .WithMany(c => c.Productos)
                  .HasForeignKey(e => e.CategoriaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTipoVenta(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TipoVenta>(entity =>
        {
            entity.ToTable("TiposVenta");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.AplicaRecargo)
                  .IsRequired();

            entity.HasIndex(e => e.Nombre)
                  .IsUnique();
        });
    }

    private static void ConfigureProductoPrecio(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductoPrecio>(entity =>
        {
            entity.ToTable("ProductoPrecios");

            entity.Property(e => e.PrecioVenta)
                  .IsRequired()
                  .HasPrecision(18, 2);

            // Índice compuesto único: un producto tiene un solo precio por sucursal y tipo de venta
            entity.HasIndex(e => new { e.ProductoId, e.SucursalId, e.TipoVentaId })
                  .IsUnique();

            entity.HasOne(e => e.Producto)
                  .WithMany(p => p.ProductoPrecios)
                  .HasForeignKey(e => e.ProductoId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Sucursal)
                  .WithMany(s => s.ProductoPrecios)
                  .HasForeignKey(e => e.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TipoVenta)
                  .WithMany(t => t.ProductoPrecios)
                  .HasForeignKey(e => e.TipoVentaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMetodoPago(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetodoPago>(entity =>
        {
            entity.ToTable("MetodosPago");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.ComisionPorcentaje)
                  .IsRequired()
                  .HasPrecision(5, 2);

            entity.Property(e => e.RequiereFacturaAfip)
                  .IsRequired();

            entity.Property(e => e.EsCuentaCorriente)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.HasIndex(e => e.Nombre)
                  .IsUnique();
        });
    }

    private static void ConfigureMesa(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mesa>(entity =>
        {
            entity.ToTable("Mesas");

            entity.Property(e => e.Etiqueta)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(e => e.Capacidad)
                  .IsRequired();

            entity.Property(e => e.PosX)
                  .IsRequired();

            entity.Property(e => e.PosY)
                  .IsRequired();

            entity.Property(e => e.Forma)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            // Etiqueta única por sucursal
            entity.HasIndex(e => new { e.SucursalId, e.Etiqueta })
                  .IsUnique();

            entity.HasOne(e => e.Sucursal)
                  .WithMany(s => s.Mesas)
                  .HasForeignKey(e => e.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    // ========================
    // DOMINIO TRANSACCIONAL
    // ========================

    private static void ConfigureCaja(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Caja>(entity =>
        {
            entity.ToTable("Cajas");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.TipoCaja)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(e => e.SyncEstado)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasIndex(e => e.SyncEstado);

            entity.HasOne(e => e.Sucursal)
                  .WithMany(s => s.Cajas)
                  .HasForeignKey(e => e.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTurnoCaja(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TurnoCaja>(entity =>
        {
            entity.ToTable("TurnosCaja");

            entity.Property(e => e.FechaApertura)
                  .IsRequired();

            entity.Property(e => e.FondoInicial)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.DiferenciaArqueo)
                  .HasPrecision(18, 2);

            entity.Property(e => e.SyncEstado)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(e => e.FechaContable)
                  .IsRequired();

            entity.Property(e => e.Turno)
                  .IsRequired()
                  .HasMaxLength(10);

            entity.HasIndex(e => e.SyncEstado);
            entity.HasIndex(e => e.FechaApertura);
            entity.HasIndex(e => new { e.FechaContable, e.Turno });

            entity.HasOne(e => e.Caja)
                  .WithMany(c => c.TurnosCaja)
                  .HasForeignKey(e => e.CajaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Usuario)
                  .WithMany(u => u.TurnosCaja)
                  .HasForeignKey(e => e.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureComanda(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Comanda>(entity =>
        {
            entity.ToTable("Comandas");

            entity.Property(e => e.Estado)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(e => e.Subtotal)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.Descuento)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.Total)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.SyncEstado)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            // Nulos mientras la comanda es una cuenta corriente abierta (exenta de turno);
            // se asignan al cerrarla/cobrarla.
            entity.Property(e => e.FechaContable);

            entity.Property(e => e.Turno)
                  .HasMaxLength(10);

            entity.HasIndex(e => e.Estado);
            entity.HasIndex(e => e.SyncEstado);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.FechaContable, e.Turno });
            entity.HasIndex(e => e.ClienteId);

            entity.HasOne(e => e.TipoVenta)
                  .WithMany(t => t.Comandas)
                  .HasForeignKey(e => e.TipoVentaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Mesa)
                  .WithMany(m => m.Comandas)
                  .HasForeignKey(e => e.MesaId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Cliente)
                  .WithMany()
                  .HasForeignKey(e => e.ClienteId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Usuario)
                  .WithMany(u => u.Comandas)
                  .HasForeignKey(e => e.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureComandaItem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComandaItem>(entity =>
        {
            entity.ToTable("ComandaItems");

            entity.Property(e => e.Cantidad)
                  .IsRequired();

            entity.Property(e => e.PrecioUnitario)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.Notas)
                  .HasMaxLength(500);

            entity.Property(e => e.EstadoPreparacion)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(e => e.Cancelado)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.Property(e => e.MotivoAnulacion)
                  .HasMaxLength(500);

            entity.HasIndex(e => e.EstadoPreparacion);
            entity.HasIndex(e => e.Cancelado);

            entity.HasOne(e => e.Comanda)
                  .WithMany(c => c.Items)
                  .HasForeignKey(e => e.ComandaId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Producto)
                  .WithMany(p => p.ComandaItems)
                  .HasForeignKey(e => e.ProductoId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AnuladoPorUsuario)
                  .WithMany()
                  .HasForeignKey(e => e.AnuladoPorUsuarioId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePago(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pago>(entity =>
        {
            entity.ToTable("Pagos");

            entity.Property(e => e.Monto)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.SyncEstado)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasIndex(e => e.SyncEstado);

            entity.HasOne(e => e.Comanda)
                  .WithMany(c => c.Pagos)
                  .HasForeignKey(e => e.ComandaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TurnoCaja)
                  .WithMany(t => t.Pagos)
                  .HasForeignKey(e => e.TurnoCajaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.MetodoPago)
                  .WithMany(m => m.Pagos)
                  .HasForeignKey(e => e.MetodoPagoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMovimientoCaja(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MovimientoCaja>(entity =>
        {
            entity.ToTable("MovimientosCaja");

            entity.Property(e => e.Tipo)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(e => e.Monto)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.Concepto)
                  .IsRequired()
                  .HasMaxLength(300);

            entity.Property(e => e.ReferenciaComprobante)
                  .HasMaxLength(200);

            entity.Property(e => e.SyncEstado)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasIndex(e => e.SyncEstado);
            entity.HasIndex(e => e.Tipo);

            entity.HasOne(e => e.TurnoCaja)
                  .WithMany(t => t.MovimientosCaja)
                  .HasForeignKey(e => e.TurnoCajaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    // ========================
    // DOMINIO SEGURIDAD
    // ========================

    private static void ConfigureDispositivoActivacion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DispositivoActivacion>(entity =>
        {
            entity.ToTable("DispositivosActivacion");

            entity.Property(e => e.CodigoActivacion)
                  .IsRequired()
                  .HasMaxLength(20);

            entity.Property(e => e.NombreDispositivo)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.Activado)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.Property(e => e.ExpiraCodigo)
                  .IsRequired();

            entity.Property(e => e.TokenHash)
                  .HasColumnType("text");

            entity.HasIndex(e => e.CodigoActivacion)
                  .IsUnique();

            entity.HasIndex(e => e.SucursalId);

            entity.HasOne(e => e.Sucursal)
                  .WithMany()
                  .HasForeignKey(e => e.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    // ========================
    // DOMINIO INVENTARIO
    // ========================

    private static void ConfigureInsumo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Insumo>(entity =>
        {
            entity.ToTable("Insumos");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.UnidadMedida)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(e => e.StockMinimo)
                  .IsRequired()
                  .HasPrecision(18, 4);

            entity.HasIndex(e => e.Nombre)
                  .IsUnique();
        });
    }

    private static void ConfigureReceta(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Receta>(entity =>
        {
            entity.ToTable("Recetas");

            entity.Property(e => e.CantidadNecesaria)
                  .IsRequired()
                  .HasPrecision(18, 4);

            // Índice compuesto único: un producto tiene una sola entrada por insumo
            entity.HasIndex(e => new { e.ProductoId, e.InsumoId })
                  .IsUnique();

            entity.HasOne(e => e.Producto)
                  .WithMany(p => p.Recetas)
                  .HasForeignKey(e => e.ProductoId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Insumo)
                  .WithMany(i => i.Recetas)
                  .HasForeignKey(e => e.InsumoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureStockSucursal(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockSucursal>(entity =>
        {
            entity.ToTable("StockSucursales");

            entity.Property(e => e.CantidadActual)
                  .IsRequired()
                  .HasPrecision(18, 4);

            // Índice compuesto único: un insumo tiene un solo registro por sucursal
            entity.HasIndex(e => new { e.SucursalId, e.InsumoId })
                  .IsUnique();

            entity.HasOne(e => e.Sucursal)
                  .WithMany(s => s.StockSucursales)
                  .HasForeignKey(e => e.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Insumo)
                  .WithMany(i => i.StockSucursales)
                  .HasForeignKey(e => e.InsumoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    // ========================
    // DOMINIO CUENTAS CORRIENTES
    // ========================

    private static void ConfigureCliente(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("Clientes");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.Apellido)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.Telefono)
                  .HasMaxLength(50);

            entity.Property(e => e.Email)
                  .HasMaxLength(250);

            entity.Property(e => e.LimiteCredito)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.SyncEstado)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasIndex(e => e.Nombre);
            entity.HasIndex(e => new { e.Nombre, e.Apellido });
            entity.HasIndex(e => e.SyncEstado);
        });
    }

    private static void ConfigureCuentaCorriente(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CuentaCorriente>(entity =>
        {
            entity.ToTable("CuentasCorrientes");

            entity.Property(e => e.SaldoActual)
                  .IsRequired()
                  .HasPrecision(18, 2);

            // Relación 1:1 con Cliente
            entity.HasIndex(e => e.ClienteId)
                  .IsUnique();

            entity.HasOne(e => e.Cliente)
                  .WithOne(c => c.CuentaCorriente)
                  .HasForeignKey<CuentaCorriente>(e => e.ClienteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureMovimientoCuentaCorriente(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MovimientoCuentaCorriente>(entity =>
        {
            entity.ToTable("MovimientosCuentaCorriente");

            entity.Property(e => e.Tipo)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(e => e.Monto)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.Detalle)
                  .IsRequired()
                  .HasMaxLength(500);

            entity.Property(e => e.SyncEstado)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasIndex(e => e.SyncEstado);
            entity.HasIndex(e => e.CuentaCorrienteId);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.CuentaCorriente)
                  .WithMany()
                  .HasForeignKey(e => e.CuentaCorrienteId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Comanda)
                  .WithMany()
                  .HasForeignKey(e => e.ComandaId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }

    // ========================
    // CONFIGURACIÓN POS
    // ========================

    private static void ConfigureConfiguracionPos(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfiguracionPos>(entity =>
        {
            entity.ToTable("ConfiguracionesPos");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(150);

            entity.Property(e => e.ConfiguracionJson)
                  .IsRequired()
                  .HasColumnType("text");

            entity.HasIndex(e => new { e.SucursalId, e.Nombre })
                  .IsUnique();

            entity.HasOne(e => e.Sucursal)
                  .WithMany(s => s.ConfiguracionesPos)
                  .HasForeignKey(e => e.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    // ========================
    // IMPRESIÓN
    // ========================

    private static void ConfigureImpresora(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Impresora>(entity =>
        {
            entity.ToTable("Impresoras");

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(150);

            entity.Property(e => e.TipoConexion)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(10);

            entity.Property(e => e.Direccion)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.Puerto)
                  .IsRequired();

            // Nombre único por sucursal
            entity.HasIndex(e => new { e.SucursalId, e.Nombre })
                  .IsUnique();

            entity.HasOne(e => e.Sucursal)
                  .WithMany(s => s.Impresoras)
                  .HasForeignKey(e => e.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTipoTicket(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TipoTicket>(entity =>
        {
            entity.ToTable("TiposTicket");

            entity.Property(e => e.Codigo)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(e => e.Nombre)
                  .IsRequired()
                  .HasMaxLength(150);

            entity.Property(e => e.TemplateContenido)
                  .IsRequired()
                  .HasColumnType("text");

            entity.HasIndex(e => e.Codigo)
                  .IsUnique();
        });
    }

    private static void ConfigureImpresoraTicketTipo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImpresoraTicketTipo>(entity =>
        {
            entity.ToTable("ImpresoraTicketTipos");

            // Combinación única: una impresora solo puede tener un tipo de ticket una vez
            entity.HasIndex(e => new { e.ImpresoraId, e.TipoTicketId })
                  .IsUnique();

            entity.HasOne(e => e.Impresora)
                  .WithMany(i => i.TicketTiposHabilitados)
                  .HasForeignKey(e => e.ImpresoraId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TipoTicket)
                  .WithMany(t => t.Impresoras)
                  .HasForeignKey(e => e.TipoTicketId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    // ========================
    // CAJA DIARIA
    // ========================

    private static void ConfigureCierreDiario(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CierreDiario>(entity =>
        {
            entity.ToTable("CierresDiarios");

            entity.Property(e => e.Fecha)
                  .IsRequired();

            entity.Property(e => e.TotalVentas)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.TotalEgresos)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.TotalNeto)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.ResumenJson)
                  .HasColumnType("text");

            entity.Property(e => e.Observaciones)
                  .HasMaxLength(500);

            entity.Property(e => e.SyncEstado)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);

            // Índice compuesto único: una caja tiene un solo cierre por día
            entity.HasIndex(e => new { e.CajaId, e.Fecha })
                  .IsUnique();

            entity.HasIndex(e => e.SyncEstado);
            entity.HasIndex(e => e.Fecha);

            entity.HasOne(e => e.Caja)
                  .WithMany()
                  .HasForeignKey(e => e.CajaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.UsuarioCierre)
                  .WithMany()
                  .HasForeignKey(e => e.UsuarioCierreId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
