namespace BaresFamilia.Core.Models.Enums;

/// <summary>
/// Estado de un trabajo de impresión enviado a una impresora de la sucursal.
/// </summary>
public enum EstadoPrintJob
{
    Pendiente = 0,
    Enviado = 1,
    Impreso = 2,
    Fallo = 3
}
