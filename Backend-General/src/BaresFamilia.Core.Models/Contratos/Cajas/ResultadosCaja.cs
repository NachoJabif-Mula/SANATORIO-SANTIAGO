namespace BaresFamilia.Core.Models.Contratos.Cajas;

/// <summary>
/// Estado contable actual de una caja: qué fecha y turno corresponde operar
/// y si hay un turno abierto en este momento.
/// </summary>
public record EstadoCaja(DateTime FechaContable, string Turno, string Estado);

/// <summary>
/// Datos del turno de caja abierto en este momento.
/// </summary>
public class TurnoActivoResponse
{
    public Guid TurnoId { get; set; }
    public Guid CajaId { get; set; }
    public string CajaNombre { get; set; } = string.Empty;
    public Guid UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime FechaApertura { get; set; }
    public decimal FondoInicial { get; set; }
    public DateTime FechaContable { get; set; }
    public string Turno { get; set; } = string.Empty;
}

/// <summary>
/// Total recaudado con un método de pago dentro de un turno o de un día.
/// </summary>
public class DesglosePorMetodo
{
    public Guid MetodoPagoId { get; set; }
    public string MetodoPagoNombre { get; set; } = string.Empty;
    public int CantidadOperaciones { get; set; }
    public decimal Total { get; set; }
}

/// <summary>
/// Resultado del arqueo de un turno: lo calculado por el sistema frente a lo
/// declarado por el encargado.
/// </summary>
public class ResultadoCierreTurno
{
    public Guid TurnoCajaId { get; set; }
    public DateTime FechaCierre { get; set; }
    public decimal FondoInicial { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal MontoEsperadoEfectivo { get; set; }
    public decimal MontoDeclarado { get; set; }
    public decimal DiferenciaArqueo { get; set; }
    public List<DesglosePorMetodo> DesglosePorMetodo { get; set; } = [];
    public string? Observaciones { get; set; }
    public bool EsUltimoTurnoDia { get; set; }
    public int TurnosAbiertosDia { get; set; }
}

/// <summary>
/// Resumen de un turno dentro del detalle del cierre diario.
/// </summary>
public class DetalleTurno
{
    public Guid TurnoId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal FondoInicial { get; set; }
    public decimal DiferenciaArqueo { get; set; }
}

/// <summary>
/// Resultado del cierre diario consolidado de todos los turnos de una fecha contable.
/// </summary>
public class ResultadoCierreDiario
{
    public Guid CierreId { get; set; }
    public DateTime Fecha { get; set; }
    public string CajaNombre { get; set; } = string.Empty;
    public int TotalTurnos { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal TotalNeto { get; set; }
    public List<DesglosePorMetodo> DesglosePorMetodo { get; set; } = [];
    public List<DetalleTurno> DetalleTurnos { get; set; } = [];
    public string? Observaciones { get; set; }
}

/// <summary>
/// Panorama del día contable en curso para el POS.
/// </summary>
public class ResumenDia
{
    public DateTime Fecha { get; set; } = DateTime.UtcNow.Date;
    public int TotalTurnos { get; set; }
    public int TurnosAbiertos { get; set; }
    public int TurnosCerrados { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalEgresos { get; set; }
    public Guid? TurnoActivoId { get; set; }
    public bool CierreDiarioRealizado { get; set; }
}
