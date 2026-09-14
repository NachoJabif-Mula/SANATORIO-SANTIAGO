using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Repositorio específico para MetodoPago.
/// </summary>
public interface IMetodoPagoRepository : IRepository<MetodoPago>
{
    /// <summary>
    /// Obtiene los métodos de pago ordenados por nombre. Si incluirInactivos es true
    /// agrega los dados de baja (usado por la sincronización de la sucursal).
    /// </summary>
    Task<IEnumerable<MetodoPago>> GetOrdenadosPorNombreAsync(bool incluirInactivos, CancellationToken ct = default);

    /// <summary>
    /// Indica si existe al menos un método de pago cargado (activo o inactivo).
    /// </summary>
    Task<bool> ExisteAlgunoAsync(CancellationToken ct = default);

    /// <summary>
    /// Indica si ya hay un método de pago activo con ese nombre, opcionalmente
    /// excluyendo un id (para permitir que una edición conserve su propio nombre).
    /// </summary>
    Task<bool> ExisteNombreAsync(string nombre, Guid? idExcluido = null, CancellationToken ct = default);
}
