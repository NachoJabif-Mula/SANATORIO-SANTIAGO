namespace BaresFamilia.Fiscal.Engine;

/// <summary>
/// Configuración de comunicación con el controlador fiscal físico.
/// </summary>
public class FiscalDeviceConfig
{
    /// <summary>
    /// Marca o driver: "Epson", "Hasar", "Moretti".
    /// </summary>
    public string Driver { get; set; } = "Epson";

    /// <summary>
    /// Puerto de conexión para Epson/Moretti (ej: "COM3", "USB", "USB001").
    /// </summary>
    public string Puerto { get; set; } = "USB";

    /// <summary>
    /// Dirección IP para Hasar 2G de red (ej: "192.168.1.100").
    /// </summary>
    public string Ip { get; set; } = "192.168.1.100";

    /// <summary>
    /// Puerto HTTP para Hasar 2G (por defecto 80 o 5000).
    /// </summary>
    public int PuertoTcp { get; set; } = 80;

    /// <summary>
    /// Velocidad del puerto serie (BaudRate: 9600, 115200).
    /// </summary>
    public int Velocidad { get; set; } = 9600;

    /// <summary>
    /// Timeout en segundos para comandos fiscales (por defecto 30s).
    /// </summary>
    public int TimeoutSegundos { get; set; } = 30;
}

/// <summary>
/// Datos del cliente comprador para facturas A/B/C.
/// </summary>
public class FiscalClienteDto
{
    public string Nombre { get; set; } = "Consumidor Final";
    public string Cuit { get; set; } = string.Empty;
    public int CondicionIva { get; set; } = FiscalConstants.COND_CONSUMIDOR_FINAL;
    public string Domicilio { get; set; } = string.Empty;
}

/// <summary>
/// Item o producto de la factura.
/// </summary>
public class FiscalItemDto
{
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; } = 1m;
    public decimal PrecioUnitarioConIva { get; set; }
    public int AlicuotaIvaCode { get; set; } = FiscalConstants.IVA_21;
    public decimal DescuentoPorcentaje { get; set; } = 0m;
}

/// <summary>
/// Forma de pago aplicada al comprobante.
/// </summary>
public class FiscalPagoDto
{
    public int FormaPagoCode { get; set; } = FiscalConstants.PAGO_EFECTIVO;
    public decimal Monto { get; set; }
    public string Descripcion { get; set; } = "Efectivo";
}

/// <summary>
/// Solicitud completa de emisión de comprobante fiscal.
/// </summary>
public class FiscalFacturaRequest
{
    public int TipoComprobante { get; set; } = FiscalConstants.COMP_FACTURA_B;
    public FiscalClienteDto Cliente { get; set; } = new();
    public List<FiscalItemDto> Items { get; set; } = new();
    public List<FiscalPagoDto> Pagos { get; set; } = new();
    public decimal DescuentoGeneralMonto { get; set; } = 0m;
    public string NumeroComprobanteAsociado { get; set; } = string.Empty; // Para Notas de Crédito
}

/// <summary>
/// Resultado de una operación fiscal.
/// </summary>
public class FiscalResult
{
    public bool Success { get; set; }
    public string NumeroComprobante { get; set; } = string.Empty;
    public string Cae { get; set; } = string.Empty;
    public string VencimientoCae { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string RawResponseJson { get; set; } = string.Empty;

    public static FiscalResult Ok(string numComprobante, string cae = "", string vtoCae = "") => new()
    {
        Success = true,
        NumeroComprobante = numComprobante,
        Cae = cae,
        VencimientoCae = vtoCae
    };

    public static FiscalResult Error(string message, int code = -1) => new()
    {
        Success = false,
        ErrorCode = code,
        ErrorMessage = message
    };
}
