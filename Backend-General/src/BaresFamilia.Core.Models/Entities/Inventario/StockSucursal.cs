using BaresFamilia.Core.Models.Entities.Catalogo;

namespace BaresFamilia.Core.Models.Entities.Inventario;

/// <summary>
/// Stock actual de un insumo en una sucursal específica.
/// Permite controlar el inventario de forma descentralizada por punto de venta.
/// </summary>
public class StockSucursal : BaseEntity
{
    public Guid SucursalId { get; set; }
    public Guid InsumoId { get; set; }

    /// <summary>
    /// Cantidad actual disponible del insumo en la sucursal.
    /// </summary>
    public decimal CantidadActual { get; set; }

    // Navegación
    public Sucursal Sucursal { get; set; } = null!;
    public Insumo Insumo { get; set; } = null!;
}
