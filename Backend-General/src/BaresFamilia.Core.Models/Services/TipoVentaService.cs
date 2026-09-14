using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para TipoVenta.
/// Incluye la siembra de los tipos por defecto: el catálogo nunca puede quedar
/// vacío porque toda comanda necesita un tipo de venta asociado.
/// </summary>
public class TipoVentaService : GenericService<TipoVenta>, ITipoVentaService
{
    private readonly ITipoVentaRepository _tipoVentaRepository;

    public TipoVentaService(ITipoVentaRepository tipoVentaRepository) : base(tipoVentaRepository)
    {
        _tipoVentaRepository = tipoVentaRepository;
    }

    public async Task<IEnumerable<TipoVenta>> GetOrdenadosPorNombreAsync(bool incluirInactivos, CancellationToken ct = default)
    {
        await SembrarTiposPorDefectoSiNoHayNingunoAsync(ct);
        return await _tipoVentaRepository.GetOrdenadosPorNombreAsync(incluirInactivos, ct);
    }

    public async Task<TipoVenta> CrearAsync(TipoVenta tipoVenta, CancellationToken ct = default)
    {
        tipoVenta.Nombre = NormalizarNombre(tipoVenta.Nombre);
        tipoVenta.IsActive = true;
        return await CreateAsync(tipoVenta, ct);
    }

    public async Task ActualizarAsync(TipoVenta tipoVenta, CancellationToken ct = default)
    {
        tipoVenta.Nombre = NormalizarNombre(tipoVenta.Nombre);
        await UpdateAsync(tipoVenta, ct);
    }

    private async Task SembrarTiposPorDefectoSiNoHayNingunoAsync(CancellationToken ct)
    {
        if (await _tipoVentaRepository.ExisteAlgunoAsync(ct))
            return;

        var ahora = DateTime.UtcNow;
        await _tipoVentaRepository.AddRangeAsync(
        [
            new TipoVenta { Id = Guid.NewGuid(), Nombre = "Salón", AplicaRecargo = false, IsActive = true, CreatedAt = ahora, UpdatedAt = ahora },
            new TipoVenta { Id = Guid.NewGuid(), Nombre = "Delivery", AplicaRecargo = true, IsActive = true, CreatedAt = ahora, UpdatedAt = ahora },
            new TipoVenta { Id = Guid.NewGuid(), Nombre = "Mostrador", AplicaRecargo = false, IsActive = true, CreatedAt = ahora, UpdatedAt = ahora }
        ], ct);
    }

    private static string NormalizarNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre es obligatorio.");

        return nombre.Trim();
    }
}
