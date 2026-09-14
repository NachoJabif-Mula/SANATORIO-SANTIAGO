namespace BaresFamilia.Core.Models.Dtos.Cajas;

/// <summary>
/// Cierre diario tal como lo lista el Backoffice.
/// </summary>
public class CierreDiarioDto
{
    public Guid Id { get; set; }
    public Guid CajaId { get; set; }
    public string CajaNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public Guid UsuarioCierreId { get; set; }
    public string UsuarioCierreNombre { get; set; } = string.Empty;
    public decimal TotalVentas { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal TotalNeto { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Detalle de un cierre diario: agrega el resumen serializado de turnos y
/// desglose por método de pago que se calculó al cerrarlo.
/// </summary>
public class CierreDiarioDetalleDto : CierreDiarioDto
{
    public string? ResumenJson { get; set; }
}
