using BaresFamilia.Core.Models.Contratos.Cajas;
using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio del ciclo de caja de la sucursal: apertura y cierre de
/// turnos, egresos, arqueo y cierre diario consolidado.
/// Flujo: CajaController → ICajaService → CajaService → I*Repository → *Repository.
/// </summary>
public interface ICajaService
{
    /// <summary>
    /// Estado contable actual de la caja de la sucursal. Si no hay caja configurada
    /// devuelve el estado "SinCaja" con la fecha de hoy.
    /// </summary>
    Task<EstadoCaja> GetEstadoAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene el turno abierto, o null si no hay ninguno.
    /// </summary>
    Task<TurnoActivoResponse?> GetTurnoActivoAsync(CancellationToken ct = default);

    /// <summary>
    /// Abre un turno de caja. Crea la caja principal si la sucursal todavía no tiene
    /// una, y rechaza la apertura si ya hay otro turno abierto.
    /// </summary>
    Task<TurnoActivoResponse> AbrirTurnoAsync(Guid usuarioId, decimal fondoInicial, CancellationToken ct = default);

    /// <summary>
    /// Registra un egreso de efectivo en un turno abierto.
    /// </summary>
    Task<MovimientoCaja> RegistrarEgresoAsync(Guid turnoCajaId, decimal monto, string concepto, string? referenciaComprobante, CancellationToken ct = default);

    /// <summary>
    /// Calcula el arqueo de un turno sin cerrarlo.
    /// </summary>
    Task<ResultadoCierreTurno> GetResumenTurnoAsync(Guid turnoId, CancellationToken ct = default);

    /// <summary>
    /// Cierra un turno con arqueo a ciegas: el encargado declara el efectivo en mano
    /// y el sistema calcula la diferencia. Si quedan comandas abiertas, o se transfieren
    /// al turno siguiente o se rechaza el cierre. Al cerrar el turno PM se dispara el
    /// cierre diario automático.
    /// </summary>
    Task<ResultadoCierreTurno> CerrarTurnoAsync(Guid turnoCajaId, decimal montoDeclarado, string? observaciones, bool transferirMesasAbiertas, CancellationToken ct = default);

    /// <summary>
    /// Genera el cierre diario consolidando todos los turnos de la fecha contable en curso.
    /// </summary>
    Task<ResultadoCierreDiario> GenerarCierreDiarioAsync(Guid usuarioId, string? observaciones, CancellationToken ct = default);

    /// <summary>
    /// Resumen del día contable en curso. Devuelve un resumen vacío si no hay caja configurada.
    /// </summary>
    Task<ResumenDia> GetResumenDiaAsync(CancellationToken ct = default);
}
