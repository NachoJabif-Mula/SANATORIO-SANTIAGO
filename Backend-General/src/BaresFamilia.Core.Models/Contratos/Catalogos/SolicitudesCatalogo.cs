using BaresFamilia.Core.Models.Enums;

namespace BaresFamilia.Core.Models.Contratos.Catalogos;

/// <summary>
/// Alta de sucursal. Los campos fiscales son opcionales: una sucursal puede
/// operar y cargarlos después, cuando se habilita la facturación electrónica.
/// </summary>
public record CreateSucursalRequest(
    string Nombre,
    string Direccion,
    string? Cuit = null,
    string? RazonSocial = null,
    string? DomicilioFiscal = null,
    int? CondicionIva = null,
    int? PuntoDeVenta = 1,
    string? NumeroIIBB = null,
    DateTime? FechaInicioActividades = null,
    bool? ImpresionSimulada = null);

public record UpdateSucursalRequest(
    string Nombre,
    string Direccion,
    string? Cuit = null,
    string? RazonSocial = null,
    string? DomicilioFiscal = null,
    int? CondicionIva = null,
    int? PuntoDeVenta = null,
    string? NumeroIIBB = null,
    DateTime? FechaInicioActividades = null,
    bool? ImpresionSimulada = null);

public record CreateCategoriaRequest(Guid SucursalId, string Nombre, int OrdenVisual);

/// <summary>
/// Edición de categoría. La sucursal dueña no se puede reasignar: si hay que mover
/// el catálogo de sucursal, se recrea.
/// </summary>
public record UpdateCategoriaRequest(string Nombre, int OrdenVisual);

public record CreateProductoRequest(
    Guid SucursalId,
    Guid CategoriaId,
    string Nombre,
    string? ColorUi,
    bool RequiereCocina,
    AlicuotaIva AlicuotaIva = AlicuotaIva.Iva21
);

public record UpdateProductoRequest(
    Guid CategoriaId,
    string Nombre,
    string? ColorUi,
    bool RequiereCocina,
    AlicuotaIva AlicuotaIva = AlicuotaIva.Iva21
);

/// <summary>
/// Entrada de la grilla de precios que se guarda en bloque. La sucursal no viaja:
/// siempre es la del producto.
/// </summary>
public record UpdateProductoPrecioRequest(
    Guid TipoVentaId,
    decimal PrecioVenta
);

public record CreateTipoVentaRequest(string Nombre, bool AplicaRecargo);

public record UpdateTipoVentaRequest(string Nombre, bool AplicaRecargo);

public record SaveMetodoPagoRequest(string Nombre, decimal ComisionPorcentaje, bool RequiereFacturaAfip);

/// <summary>
/// Configuración del salón que el POS renderiza (coordenadas de mesas, colores,
/// zonas), guardada como JSON para no acoplar el backend al diseño de la pantalla.
/// </summary>
public record SaveConfiguracionPosRequest(
    Guid SucursalId,
    string? Nombre,
    string ConfiguracionJson
);

public record CrearImpresoraRequest(
    Guid SucursalId,
    string Nombre,
    TipoDispositivoImpresora TipoDispositivo = TipoDispositivoImpresora.Comandera,
    TipoConexionImpresora TipoConexion = TipoConexionImpresora.Red,
    string Direccion = "",
    int Puerto = 9100,
    int Velocidad = 9600);

public record ActualizarImpresoraRequest(
    string Nombre,
    TipoDispositivoImpresora TipoDispositivo = TipoDispositivoImpresora.Comandera,
    TipoConexionImpresora TipoConexion = TipoConexionImpresora.Red,
    string Direccion = "",
    int Puerto = 9100,
    int Velocidad = 9600);

public record ActualizarTipoTicketRequest(string Nombre, string TemplateContenido);

/// <summary>
/// Precio a aplicar a un producto para un tipo de venta determinado.
/// </summary>
public record PrecioPorTipoVenta(Guid TipoVentaId, decimal PrecioVenta);
