using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Entities.Catalogo;

/// <summary>
/// Mesa física del local con posición en el plano del salón.
/// </summary>
public class Mesa : BaseEntity
{
    public Guid SucursalId { get; set; }
    public string Etiqueta { get; set; } = string.Empty;
    public int Capacidad { get; set; }

    /// <summary>
    /// Posición X en el plano visual del salón (píxeles o porcentaje).
    /// </summary>
    public double PosX { get; set; }

    /// <summary>
    /// Posición Y en el plano visual del salón (píxeles o porcentaje).
    /// </summary>
    public double PosY { get; set; }

    /// <summary>
    /// Forma geométrica de la mesa para renderizar en el plano.
    /// </summary>
    public FormaMesa Forma { get; set; } = FormaMesa.Cuadrada;

    /// <summary>
    /// Indicador de si la mesa se encuentra ocupada (tiene comanda abierta activa).
    /// </summary>
    public bool Ocupada { get; set; } = false;

    // Navegación
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<Comanda> Comandas { get; set; } = [];
}
