namespace BaresFamilia.Core.Models.Dtos.Sincronizacion;

// ═══════════════════════════════════════════════════════
// Espejos de subida (sucursal → Nube)
//
// Reflejan las tablas transaccionales que la sucursal envía para consolidar.
// Los comparten las dos puntas: la sucursal los serializa y la Nube los recibe,
// así un cambio de forma no puede quedar aplicado en un solo lado.
// ═══════════════════════════════════════════════════════

/// <summary>
/// Lote de datos transaccionales que una sucursal sube a la Nube.
/// El orden en que se procesan importa: cajas y turnos primero, para que pagos,
/// movimientos y cierres puedan enlazarse contra ellos sin violar claves foráneas.
/// </summary>
public class SyncPayloadDto
{
    public DateTime Timestamp { get; set; }
    public List<SyncComandaDto> Comandas { get; set; } = [];
    public List<SyncPagoDto> Pagos { get; set; } = [];
    public List<SyncMovimientoDto> Movimientos { get; set; } = [];
    public List<SyncCierreDiarioDto> CierresDiarios { get; set; } = [];
    public List<SyncClienteDto> Clientes { get; set; } = [];
    public List<SyncMovimientoCuentaCorrienteDto> MovimientosCuentaCorriente { get; set; } = [];
    public List<SyncTurnoCajaDto> TurnosCaja { get; set; } = [];
    public List<SyncCajaDto> Cajas { get; set; } = [];
}

/// <summary>
/// Caja real de la sucursal.
/// </summary>
public class SyncCajaDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string TipoCaja { get; set; } = string.Empty;
}

/// <summary>
/// Turno de caja real. Reemplaza al turno "stub" que la Nube fabricaba solo para
/// satisfacer la clave foránea de Pagos y Movimientos.
/// </summary>
public class SyncTurnoCajaDto
{
    public Guid Id { get; set; }
    public Guid CajaId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public DateTime FechaContable { get; set; }
    public string Turno { get; set; } = string.Empty;
    public decimal FondoInicial { get; set; }
    public decimal? DiferenciaArqueo { get; set; }
}

/// <summary>
/// Cliente dado de alta en el POS. Solo viaja el nombre: teléfono, email y límite
/// de crédito quedan en la sucursal; a la Nube le alcanza con el dato del reporte.
/// </summary>
public class SyncClienteDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SyncMovimientoCuentaCorrienteDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid? ComandaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SyncCierreDiarioDto
{
    public Guid Id { get; set; }
    public Guid CajaId { get; set; }
    public DateTime Fecha { get; set; }
    public Guid UsuarioCierreId { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal TotalNeto { get; set; }
    public string? ResumenJson { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SyncComandaDto
{
    public Guid Id { get; set; }
    public Guid TipoVentaId { get; set; }
    public Guid? MesaId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
    public DateTime FechaContable { get; set; }
    public string Turno { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<SyncComandaItemDto> Items { get; set; } = [];
}

public class SyncComandaItemDto
{
    public Guid Id { get; set; }
    public Guid ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public string? Notas { get; set; }
    public bool Cancelado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public Guid? AnuladoPorUsuarioId { get; set; }
    public DateTime? FechaAnulacion { get; set; }
}

public class SyncPagoDto
{
    public Guid Id { get; set; }
    public Guid ComandaId { get; set; }
    public Guid TurnoCajaId { get; set; }
    public Guid MetodoPagoId { get; set; }
    public decimal Monto { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SyncMovimientoDto
{
    public Guid Id { get; set; }
    public Guid TurnoCajaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Usuario que la sucursal descarga de la Nube para poder autenticar por PIN sin conexión.
/// </summary>
public record SyncUsuarioDto(
    Guid Id,
    Guid RolId,
    Guid SucursalId,
    string Nombre,
    string Email,
    string PinAcceso,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
