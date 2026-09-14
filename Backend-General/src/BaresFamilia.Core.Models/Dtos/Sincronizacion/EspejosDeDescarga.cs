using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Dtos.Sincronizacion;

// ═══════════════════════════════════════════════════════
// Espejos de descarga (Nube → sucursal)
//
// Reflejan el catálogo maestro que la sucursal replica localmente para poder
// operar sin conexión. A diferencia de los de subida viajan con IsActive y las
// fechas de auditoría: la sucursal necesita reflejar también las bajas lógicas.
// ═══════════════════════════════════════════════════════

public class SyncSucursalDto
{
    public Guid Id { get; set; }
    public bool ImpresionSimulada { get; set; }
}

public class SyncRolDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public List<string> Permisos { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncCategoriaDto
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int OrdenVisual { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncProductoDto
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public Guid CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ColorUi { get; set; } = string.Empty;
    public bool RequiereCocina { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncProductoPrecioDto
{
    public Guid Id { get; set; }
    public Guid ProductoId { get; set; }
    public Guid SucursalId { get; set; }
    public Guid TipoVentaId { get; set; }
    public decimal PrecioVenta { get; set; }
    public bool IsActive { get; set; }
}

public class SyncTipoVentaDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool AplicaRecargo { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncMetodoPagoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal ComisionPorcentaje { get; set; }
    public bool RequiereFacturaAfip { get; set; }
    public bool EsCuentaCorriente { get; set; }
    public bool IsActive { get; set; }
}

public class SyncMesaDto
{
    public Guid Id { get; set; }
    public string Etiqueta { get; set; } = string.Empty;
    public int Capacidad { get; set; }
    public double PosX { get; set; }
    public double PosY { get; set; }
    public FormaMesa Forma { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncConfigPosDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ConfiguracionJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncTipoTicketDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string TemplateContenido { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Cliente completo que baja de la Nube, con sus datos de contacto y el saldo de
/// cuenta corriente ya consolidado.
///
/// No confundir con <see cref="SyncClienteDto"/>, que es el contrato de subida y
/// solo lleva el nombre: los datos de contacto no viajan hacia la Nube.
/// </summary>
public class SyncClienteDescargaDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public decimal LimiteCredito { get; set; }
    public decimal SaldoActual { get; set; }
    public bool IsActive { get; set; }
}
