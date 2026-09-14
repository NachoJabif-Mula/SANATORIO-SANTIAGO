using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio específico para TipoVenta.
/// Flujo: TipoVentaController → ITipoVentaService → TipoVentaService → ITipoVentaRepository → TipoVentaRepository.
/// </summary>
public interface ITipoVentaService : IService<TipoVenta>
{
    /// <summary>
    /// Obtiene los tipos de venta ordenados por nombre, sembrando los valores por
    /// defecto (Salón, Delivery, Mostrador) la primera vez que se consulta el catálogo.
    /// </summary>
    Task<IEnumerable<TipoVenta>> GetOrdenadosPorNombreAsync(bool incluirInactivos, CancellationToken ct = default);

    /// <summary>
    /// Da de alta un tipo de venta validando que tenga nombre.
    /// </summary>
    Task<TipoVenta> CrearAsync(TipoVenta tipoVenta, CancellationToken ct = default);

    /// <summary>
    /// Guarda los cambios de un tipo de venta existente validando que conserve un nombre.
    /// </summary>
    Task ActualizarAsync(TipoVenta tipoVenta, CancellationToken ct = default);
}
