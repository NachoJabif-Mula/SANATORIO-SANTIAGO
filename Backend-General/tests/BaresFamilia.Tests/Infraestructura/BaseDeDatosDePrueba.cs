using BaresFamilia.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Tests.Infraestructura;

/// <summary>
/// Base de datos real (SQLite en memoria) para las pruebas de integración.
///
/// Se usa SQLite y no el proveedor InMemory porque este último no soporta
/// transacciones ni restricciones relacionales, que es justamente lo que hay que
/// verificar en el arqueo de caja y en el guardado en lote de precios.
///
/// La conexión se mantiene abierta durante toda la prueba: al cerrarla, SQLite
/// descarta la base.
/// </summary>
public sealed class BaseDeDatosDePrueba : IDisposable
{
    private readonly SqliteConnection _conexion;

    public BaseDeDatosDePrueba()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
    }

    /// <summary>
    /// Crea un contexto de sucursal sobre la misma base. Cada llamada devuelve un
    /// contexto nuevo, lo que permite verificar que los datos quedaron realmente
    /// persistidos y no solo en el rastreador de cambios.
    /// </summary>
    public LocalContext NuevoContextoLocal()
    {
        var opciones = new DbContextOptionsBuilder<LocalContext>()
            .UseSqlite(_conexion)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new LocalContext(opciones);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Crea un contexto de Nube sobre la misma base.
    /// </summary>
    public NubeContext NuevoContextoNube()
    {
        var opciones = new DbContextOptionsBuilder<NubeContext>()
            .UseSqlite(_conexion)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new NubeContext(opciones);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _conexion.Dispose();
}
