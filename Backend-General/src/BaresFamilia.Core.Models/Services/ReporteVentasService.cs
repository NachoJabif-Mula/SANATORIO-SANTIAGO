using BaresFamilia.Core.Models.Contratos;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de reportería de ventas del Backoffice.
///
/// La anulación (de ítem o de comanda completa) vive como estado directamente en
/// ComandaItem (Cancelado, MotivoAnulacion, AnuladoPorUsuarioId, FechaAnulacion):
/// no hay una tabla de auditoría separada, así estos reportes comparten la misma
/// fuente de verdad que el resto del sistema.
/// </summary>
public class ReporteVentasService : IReporteVentasService
{
    private const int TamanoPaginaPorDefecto = 50;
    private const int TamanoPaginaMaximo = 200;

    private readonly IReporteVentasRepository _reporteVentasRepository;

    public ReporteVentasService(IReporteVentasRepository reporteVentasRepository)
    {
        _reporteVentasRepository = reporteVentasRepository;
    }

    public async Task<IEnumerable<Comanda>> GetComandasConDetallesAsync(CancellationToken ct = default)
        => await _reporteVentasRepository.GetComandasConDetallesAsync(ct);

    public async Task<ResultadoPaginado<ComandaItem>> GetItemsAnuladosAsync(
        Guid? sucursalId, DateTime? desde, DateTime? hasta, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        var (paginaValida, tamanoValido) = NormalizarPaginacion(pagina, tamanoPagina);
        return await _reporteVentasRepository.GetItemsAnuladosAsync(sucursalId, desde, hasta, paginaValida, tamanoValido, ct);
    }

    public async Task<ResultadoPaginado<Comanda>> GetComandasAnuladasAsync(
        Guid? sucursalId, DateTime? desde, DateTime? hasta, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        var (paginaValida, tamanoValido) = NormalizarPaginacion(pagina, tamanoPagina);
        return await _reporteVentasRepository.GetComandasAnuladasAsync(sucursalId, desde, hasta, paginaValida, tamanoValido, ct);
    }

    /// <summary>
    /// Acota los parámetros de paginación para que un cliente no pueda pedir páginas
    /// inválidas ni traer la tabla entera de una sola vez.
    /// </summary>
    private static (int Pagina, int TamanoPagina) NormalizarPaginacion(int pagina, int tamanoPagina)
    {
        if (pagina < 1)
            pagina = 1;

        if (tamanoPagina < 1 || tamanoPagina > TamanoPaginaMaximo)
            tamanoPagina = TamanoPaginaPorDefecto;

        return (pagina, tamanoPagina);
    }
}
