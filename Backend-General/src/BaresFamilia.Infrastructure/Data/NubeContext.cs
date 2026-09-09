using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Entities.Transaccional;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Infrastructure.Data;

/// <summary>
/// DbContext para la API de Nube (cloud). Utiliza PostgreSQL.
/// Es la base de datos central de sincronización y reportes.
/// </summary>
public class NubeContext : DbContext
{
    public NubeContext(DbContextOptions<NubeContext> options) : base(options) { }

    // Catálogo
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<TipoVenta> TiposVenta => Set<TipoVenta>();
    public DbSet<ProductoPrecio> ProductoPrecios => Set<ProductoPrecio>();
    public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<ConfiguracionPos> ConfiguracionesPos => Set<ConfiguracionPos>();

    // Transaccional
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<TurnoCaja> TurnosCaja => Set<TurnoCaja>();
    public DbSet<Comanda> Comandas => Set<Comanda>();
    public DbSet<ComandaItem> ComandaItems => Set<ComandaItem>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<CierreDiario> CierresDiarios => Set<CierreDiario>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

    // Seguridad
    public DbSet<DispositivoActivacion> DispositivosActivacion => Set<DispositivoActivacion>();

    // Inventario
    public DbSet<Insumo> Insumos => Set<Insumo>();
    public DbSet<Receta> Recetas => Set<Receta>();
    public DbSet<StockSucursal> StockSucursales => Set<StockSucursal>();

    // Cuentas Corrientes
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<CuentaCorriente> CuentasCorrientes => Set<CuentaCorriente>();

    // Impresión
    public DbSet<Impresora> Impresoras => Set<Impresora>();
    public DbSet<TipoTicket> TiposTicket => Set<TipoTicket>();
    public DbSet<ImpresoraTicketTipo> ImpresoraTicketTipos => Set<ImpresoraTicketTipo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        SharedModelConfiguration.ConfigureModel(modelBuilder);
    }
}
