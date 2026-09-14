namespace BaresFamilia.Core.Models.Contratos.Inventario;

/// <summary>
/// Alta de insumo. StockMinimo es el umbral que dispara el aviso de reposición.
/// </summary>
public record CreateInsumoRequest(
    string Nombre,
    string UnidadMedida,
    decimal StockMinimo
);

public record UpdateInsumoRequest(
    string Nombre,
    string? UnidadMedida,
    decimal StockMinimo
);

/// <summary>
/// Vínculo entre un producto y el insumo que consume, con la cantidad que gasta
/// cada unidad vendida.
/// </summary>
public record CreateRecetaRequest(
    Guid ProductoId,
    Guid InsumoId,
    decimal CantidadNecesaria
);

public record UpdateRecetaRequest(
    decimal CantidadNecesaria
);
