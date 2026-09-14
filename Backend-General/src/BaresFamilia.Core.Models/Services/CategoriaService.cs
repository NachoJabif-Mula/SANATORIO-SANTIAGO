using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para Categoria.
/// Concentra las reglas del catálogo: nombre obligatorio y sucursal válida.
/// </summary>
public class CategoriaService : GenericService<Categoria>, ICategoriaService
{
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IRepository<Sucursal> _sucursalRepository;

    public CategoriaService(ICategoriaRepository categoriaRepository, IRepository<Sucursal> sucursalRepository)
        : base(categoriaRepository)
    {
        _categoriaRepository = categoriaRepository;
        _sucursalRepository = sucursalRepository;
    }

    public async Task<IEnumerable<Categoria>> GetPorSucursalAsync(Guid? sucursalId, bool incluirInactivas, CancellationToken ct = default)
        => await _categoriaRepository.GetPorSucursalAsync(sucursalId, incluirInactivas, ct);

    public async Task<Categoria> CrearAsync(Categoria categoria, CancellationToken ct = default)
    {
        categoria.Nombre = NormalizarNombre(categoria.Nombre);

        if (!await _sucursalRepository.ExistsAsync(categoria.SucursalId, ct))
            throw new ReglaNegocioException("La sucursal indicada no es válida.");

        categoria.IsActive = true;
        return await CreateAsync(categoria, ct);
    }

    public async Task ActualizarAsync(Categoria categoria, CancellationToken ct = default)
    {
        categoria.Nombre = NormalizarNombre(categoria.Nombre);
        await UpdateAsync(categoria, ct);
    }

    private static string NormalizarNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre de la categoría es obligatorio.");

        return nombre.Trim();
    }
}
