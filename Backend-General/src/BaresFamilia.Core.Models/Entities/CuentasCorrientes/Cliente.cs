using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.CuentasCorrientes;

/// <summary>
/// Cliente del negocio con cuenta corriente habilitada.
/// Registra datos de contacto y límite de crédito autorizado.
/// </summary>
public class Cliente : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Monto máximo de crédito autorizado para este cliente.
    /// </summary>
    public decimal LimiteCredito { get; set; }

    /// <summary>
    /// Estado de sincronización con la Nube API.
    /// </summary>
    public SyncEstado SyncEstado { get; set; } = SyncEstado.Pendiente;

    // Navegación
    public CuentaCorriente? CuentaCorriente { get; set; }
}
