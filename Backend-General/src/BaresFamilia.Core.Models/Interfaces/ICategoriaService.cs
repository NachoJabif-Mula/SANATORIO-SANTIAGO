using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio específico para Categoria.
/// Flujo: CategoriaController → ICategoriaService → CategoriaService → ICategoriaRepository → CategoriaRepository.
/// </summary>
public interface ICategoriaService : IService<Categoria>
{
    /// <summary>
    /// Obtiene las categorías de una sucursal (o de todas si sucursalId es null),
    /// ordenadas por orden visual.
    /// </summary>
    Task<IEnumerable<Categoria>> GetPorSucursalAsync(Guid? sucursalId, bool incluirInactivas, CancellationToken ct = default);

    /// <summary>
    /// Da de alta una categoría validando que tenga nombre y que la sucursal exista.
    /// </summary>
    Task<Categoria> CrearAsync(Categoria categoria, CancellationToken ct = default);

    /// <summary>
    /// Guarda los cambios de una categoría existente validando que conserve un nombre.
    /// </summary>
    Task ActualizarAsync(Categoria categoria, CancellationToken ct = default);
}
