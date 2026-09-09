using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Servicio de negocio específico para Comanda.
/// Incluye lógica de cobro con integración AFIP simulada
/// e impresión de ticket de cocina.
/// </summary>
public interface IComandaService : IService<Comanda>
{
    /// <summary>
    /// Obtiene una comanda con todos sus detalles (ítems, pagos, método de pago).
    /// </summary>
    Task<Comanda?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las comandas abiertas de una mesa.
    /// </summary>
    Task<IEnumerable<Comanda>> GetAbierdasPorMesaAsync(Guid mesaId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las cuentas corrientes abiertas (comandas exentas de turno) de un cliente específico.
    /// </summary>
    Task<IEnumerable<Comanda>> GetAbiertasPorClienteAsync(Guid clienteId, CancellationToken ct = default);

    /// <summary>
    /// Procesar el cobro de una comanda registrando múltiples pagos, gestiona factura AFIP si corresponde,
    /// y marca la comanda como cobrada.
    /// </summary>
    Task<ResultadoCobro> CobrarComandaAsync(Guid comandaId, Guid turnoCajaId, List<PagoItemDto> pagos, CancellationToken ct = default);

    /// <summary>
    /// Envía la comanda a preparación e imprime el ticket en la Impresora 2 (Cocina/Producción).
    /// </summary>
    Task<ResultadoImpresion> EnviarAPreparacionAsync(Guid comandaId, CancellationToken ct = default);

    /// <summary>
    /// Actualiza los totales e ítems de una comanda abierta, y re-imprime el ticket de cocina.
    /// </summary>
    Task<Comanda> UpdateComandaAsync(Guid id, Comanda updatedEntity, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las comandas con sus ítems, pagos y navegaciones cargadas.
    /// </summary>
    Task<IEnumerable<Comanda>> GetAllWithDetailsAsync(CancellationToken ct = default);

    /// <summary>
    /// Imprime un ticket no válido como factura (ticket de cortesía o pre-cuenta).
    /// </summary>
    Task<ResultadoImpresion> ImprimirTicketNoFiscalAsync(Guid comandaId, CancellationToken ct = default);

    /// <summary>
    /// Anula una comanda completa (solo si está Abierta) y libera la mesa correspondiente.
    /// Requiere un motivo obligatorio y valida que el usuario tenga permiso de anulación
    /// (rol cajero/administrador o permiso "pos.anular").
    /// </summary>
    Task AnularComandaAsync(Guid comandaId, Guid usuarioId, string motivo, CancellationToken ct = default);

    /// <summary>
    /// Anula un ítem individual ya comandado (solo si la comanda sigue Abierta) y
    /// recalcula los totales de la comanda excluyendo el ítem anulado.
    /// Requiere un motivo obligatorio y valida el mismo permiso que AnularComandaAsync.
    /// </summary>
    Task AnularItemComandaAsync(Guid comandaId, Guid comandaItemId, Guid usuarioId, string motivo, CancellationToken ct = default);
}

/// <summary>
/// DTO para registrar un pago parcial dentro del cobro.
/// ClienteId es requerido solo cuando el método es "Cuenta Corriente".
/// </summary>
public record PagoItemDto(Guid MetodoPagoId, decimal Monto, Guid? ClienteId = null);

/// <summary>
/// Resultado del proceso de cobro con información de facturación AFIP.
/// </summary>
public class ResultadoCobro
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public Guid? PagoId { get; set; }

    // Datos AFIP (cuando aplica)
    public bool FacturaAfipEmitida { get; set; }
    public string? CaeNumero { get; set; }
    public string? CaeVencimiento { get; set; }
    public string? ComprobanteNumero { get; set; }
    public string? OrdenImpresionUsb { get; set; }
}
