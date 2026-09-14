using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio específico para MetodoPago.
/// Flujo: MetodoPagoController → IMetodoPagoService → MetodoPagoService → IMetodoPagoRepository → MetodoPagoRepository.
/// </summary>
public interface IMetodoPagoService : IService<MetodoPago>
{
    /// <summary>
    /// Obtiene los métodos de pago ordenados por nombre, sembrando los valores por
    /// defecto la primera vez que se consulta el catálogo.
    /// </summary>
    Task<IEnumerable<MetodoPago>> GetOrdenadosPorNombreAsync(bool incluirInactivos, CancellationToken ct = default);

    /// <summary>
    /// Da de alta un método de pago validando nombre obligatorio y unicidad.
    /// </summary>
    Task<MetodoPago> CrearAsync(MetodoPago metodoPago, CancellationToken ct = default);

    /// <summary>
    /// Guarda los cambios de un método de pago existente validando nombre obligatorio
    /// y que no colisione con el de otro método activo.
    /// </summary>
    Task ActualizarAsync(MetodoPago metodoPago, CancellationToken ct = default);
}
