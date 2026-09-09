using BaresFamilia.Core.Models.Entities.Transaccional;

namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Método de pago aceptado con comisión y configuración fiscal (AFIP).
/// </summary>
public class MetodoPago : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Porcentaje de comisión del procesador de pago (ej: 3.5 para 3.5%).
    /// </summary>
    public decimal ComisionPorcentaje { get; set; }

    /// <summary>
    /// Indica si este método de pago requiere emitir factura electrónica AFIP.
    /// </summary>
    public bool RequiereFacturaAfip { get; set; }

    /// <summary>
    /// Indica si este método representa "Cuenta Corriente" (crédito de cliente).
    /// Solo se permite un método con este flag activo.
    /// </summary>
    public bool EsCuentaCorriente { get; set; }

    // Navegación
    public ICollection<Pago> Pagos { get; set; } = [];
}
