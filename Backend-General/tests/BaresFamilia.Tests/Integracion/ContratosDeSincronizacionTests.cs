using System.Text.Json;
using BaresFamilia.Core.Models.Contratos.Seguridad;
using BaresFamilia.Core.Models.Dtos.Sincronizacion;
using Xunit;

namespace BaresFamilia.Tests.Integracion;

/// <summary>
/// Verifica que los contratos de sincronización sigan viajando igual por el cable.
///
/// La sucursal y la Nube ahora comparten los mismos tipos (antes cada lado tenía su
/// propia copia con otro nombre). Estas pruebas fijan la forma del JSON para que
/// unificarlos no haya cambiado nada, y para detectar si alguien renombra una
/// propiedad y rompe la sincronización de una sucursal ya instalada.
/// </summary>
public class ContratosDeSincronizacionTests
{
    /// <summary>Exactamente las opciones que usa el worker al leer las respuestas de la Nube.</summary>
    private static readonly JsonSerializerOptions OpcionesDelWorker =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void UnUsuarioQueBajaDeLaNube_SeDeserializaConLaPoliticaCamelCase()
    {
        // Cuerpo tal como lo emite api/empleado/por-sucursal/{id}.
        const string json = """
            [{
                "id": "11111111-1111-1111-1111-111111111111",
                "rolId": "22222222-2222-2222-2222-222222222222",
                "sucursalId": "33333333-3333-3333-3333-333333333333",
                "nombre": "Marta",
                "email": "marta@baresfamilia.com",
                "pinAcceso": "4321",
                "isActive": true,
                "createdAt": "2026-01-15T10:00:00Z",
                "updatedAt": "2026-01-16T10:00:00Z"
            }]
            """;

        var usuarios = JsonSerializer.Deserialize<List<SyncUsuarioDto>>(json, OpcionesDelWorker);

        var usuario = Assert.Single(usuarios!);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), usuario.Id);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), usuario.RolId);
        Assert.Equal("Marta", usuario.Nombre);
        Assert.Equal("4321", usuario.PinAcceso);
        Assert.True(usuario.IsActive);
    }

    [Fact]
    public void ElLoteDeSubida_ConservaLosNombresDePropiedadQueEsperaLaNube()
    {
        var payload = new SyncPayloadDto
        {
            Timestamp = DateTime.UtcNow,
            Comandas = [new SyncComandaDto { Id = Guid.NewGuid(), Turno = "AM" }],
            Cajas = [new SyncCajaDto { Id = Guid.NewGuid(), Nombre = "Caja Principal", TipoCaja = "Principal" }]
        };

        using var documento = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var raiz = documento.RootElement;

        // Las colecciones del lote son el contrato que lee el endpoint api/sync/recibir.
        foreach (var propiedad in new[]
                 {
                     "Comandas", "Pagos", "Movimientos", "CierresDiarios",
                     "Clientes", "MovimientosCuentaCorriente", "TurnosCaja", "Cajas"
                 })
        {
            Assert.True(raiz.TryGetProperty(propiedad, out _), $"El lote dejó de exponer '{propiedad}'.");
        }
    }

    [Fact]
    public void ElContratoDeSubidaDeClientes_SoloLlevaElNombre()
    {
        // Los datos de contacto y el límite de crédito son propios de la sucursal:
        // si empezaran a viajar, se estaría filtrando información del POS a la Nube.
        var propiedades = typeof(SyncClienteDto).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal(["Id", "Nombre", "Apellido", "CreatedAt"], propiedades);
    }

    [Fact]
    public void ElContratoDeDescargaDeClientes_TraeSaldoYDatosDeContacto()
    {
        var propiedades = typeof(SyncClienteDescargaDto).GetProperties().Select(p => p.Name).ToList();

        Assert.Contains("SaldoActual", propiedades);
        Assert.Contains("LimiteCredito", propiedades);
        Assert.Contains("Telefono", propiedades);
    }

    [Fact]
    public void LaActivacionGuardadaLocalmente_SeRelee()
    {
        // El worker relee el token M2M del cuerpo crudo que guardó ActivacionPosService.
        const string json = """
            {
                "token": "jwt-m2m",
                "tokenType": "Bearer",
                "expiresAt": "2027-01-01T00:00:00Z",
                "sucursalId": "44444444-4444-4444-4444-444444444444",
                "sucursalNombre": "Centro",
                "dispositivoId": "55555555-5555-5555-5555-555555555555",
                "nombreDispositivo": "POS 1"
            }
            """;

        var datos = JsonSerializer.Deserialize<DatosActivacion>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(datos);
        Assert.Equal("jwt-m2m", datos.Token);
        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444444"), datos.SucursalId);
        Assert.Equal("POS 1", datos.NombreDispositivo);
    }
}
