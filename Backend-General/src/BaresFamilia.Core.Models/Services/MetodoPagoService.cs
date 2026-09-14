using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para MetodoPago.
/// El nombre es la clave funcional del método de pago (la sincronización con las
/// sucursales lo usa para identificarlo), por eso se valida que sea único.
/// </summary>
public class MetodoPagoService : GenericService<MetodoPago>, IMetodoPagoService
{
    private readonly IMetodoPagoRepository _metodoPagoRepository;

    public MetodoPagoService(IMetodoPagoRepository metodoPagoRepository) : base(metodoPagoRepository)
    {
        _metodoPagoRepository = metodoPagoRepository;
    }

    public async Task<IEnumerable<MetodoPago>> GetOrdenadosPorNombreAsync(bool incluirInactivos, CancellationToken ct = default)
    {
        await SembrarMetodosPorDefectoSiNoHayNingunoAsync(ct);
        return await _metodoPagoRepository.GetOrdenadosPorNombreAsync(incluirInactivos, ct);
    }

    public async Task<MetodoPago> CrearAsync(MetodoPago metodoPago, CancellationToken ct = default)
    {
        metodoPago.Nombre = NormalizarNombre(metodoPago.Nombre);

        if (await _metodoPagoRepository.ExisteNombreAsync(metodoPago.Nombre, idExcluido: null, ct))
            throw new ReglaNegocioException($"Ya existe un método de pago con el nombre '{metodoPago.Nombre}'.");

        metodoPago.IsActive = true;
        return await CreateAsync(metodoPago, ct);
    }

    public async Task ActualizarAsync(MetodoPago metodoPago, CancellationToken ct = default)
    {
        metodoPago.Nombre = NormalizarNombre(metodoPago.Nombre);

        if (await _metodoPagoRepository.ExisteNombreAsync(metodoPago.Nombre, idExcluido: metodoPago.Id, ct))
            throw new ReglaNegocioException($"Ya existe otro método de pago con el nombre '{metodoPago.Nombre}'.");

        await UpdateAsync(metodoPago, ct);
    }

    private async Task SembrarMetodosPorDefectoSiNoHayNingunoAsync(CancellationToken ct)
    {
        if (await _metodoPagoRepository.ExisteAlgunoAsync(ct))
            return;

        var ahora = DateTime.UtcNow;
        await _metodoPagoRepository.AddRangeAsync(
        [
            new MetodoPago { Id = Guid.NewGuid(), Nombre = "Efectivo", ComisionPorcentaje = 0m, RequiereFacturaAfip = false, IsActive = true, CreatedAt = ahora, UpdatedAt = ahora },
            new MetodoPago { Id = Guid.NewGuid(), Nombre = "Tarjeta Débito", ComisionPorcentaje = 1.5m, RequiereFacturaAfip = true, IsActive = true, CreatedAt = ahora, UpdatedAt = ahora },
            new MetodoPago { Id = Guid.NewGuid(), Nombre = "Tarjeta Crédito", ComisionPorcentaje = 3.5m, RequiereFacturaAfip = true, IsActive = true, CreatedAt = ahora, UpdatedAt = ahora },
            new MetodoPago { Id = Guid.NewGuid(), Nombre = "MercadoPago / QR", ComisionPorcentaje = 4.5m, RequiereFacturaAfip = true, IsActive = true, CreatedAt = ahora, UpdatedAt = ahora },
            new MetodoPago { Id = Guid.NewGuid(), Nombre = "Transferencia Bancaria", ComisionPorcentaje = 0m, RequiereFacturaAfip = false, IsActive = true, CreatedAt = ahora, UpdatedAt = ahora }
        ], ct);
    }

    private static string NormalizarNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre es obligatorio.");

        return nombre.Trim();
    }
}
