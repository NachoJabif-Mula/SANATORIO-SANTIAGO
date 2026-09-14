using BaresFamilia.Core.Models.Contratos;
using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de reportería de ventas del Backoffice.
/// Flujo: ComandaController / ReportesController → IReporteVentasService → ReporteVentasService → IReporteVentasRepository → ReporteVentasRepository.
/// </summary>
public interface IReporteVentasService
{
    /// <summary>
    /// Obtiene todas las comandas consolidadas con su detalle cargado.
    /// </summary>
    Task<IEnumerable<Comanda>> GetComandasConDetallesAsync(CancellationToken ct = default);

    /// <summary>
    /// Reporte paginado de ítems anulados. La paginación se normaliza acá: página
    /// mínima 1 y tamaño entre 1 y 200, con 50 por defecto.
    /// </summary>
    Task<ResultadoPaginado<ComandaItem>> GetItemsAnuladosAsync(
        Guid? sucursalId, DateTime? desde, DateTime? hasta, int pagina, int tamanoPagina, CancellationToken ct = default);

    /// <summary>
    /// Reporte paginado de comandas anuladas por completo.
    /// </summary>
    Task<ResultadoPaginado<Comanda>> GetComandasAnuladasAsync(
        Guid? sucursalId, DateTime? desde, DateTime? hasta, int pagina, int tamanoPagina, CancellationToken ct = default);
}
