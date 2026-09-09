using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de impresión configurable.
/// Lee la configuración de impresoras y tipos de ticket de la base de datos,
/// renderiza templates con placeholders y envía a las impresoras habilitadas.
/// </summary>
public interface IImpresoraService
{
    /// <summary>
    /// Imprime un ticket del tipo especificado en todas las impresoras
    /// de la sucursal que tengan ese tipo habilitado.
    /// </summary>
    /// <param name="sucursalId">Sucursal donde buscar impresoras configuradas.</param>
    /// <param name="tipoTicketCodigo">Código del tipo de ticket ("Comanda", "FacturaA", "FacturaB").</param>
    /// <param name="comanda">Comanda con Items, Productos, Usuario y Mesa cargados.</param>
    /// <param name="datosExtra">Datos adicionales para el template (CAE, comprobante, etc.).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Lista de resultados, uno por cada impresora a la que se intentó enviar.</returns>
    Task<List<ResultadoImpresion>> ImprimirTicketAsync(
        Guid sucursalId,
        string tipoTicketCodigo,
        Comanda comanda,
        Dictionary<string, string>? datosExtra = null,
        CancellationToken ct = default);
}

/// <summary>
/// Resultado de una operación de impresión.
/// </summary>
public class ResultadoImpresion
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public string? ImpresoraUtilizada { get; set; }
    public string? TicketContenido { get; set; }
}
