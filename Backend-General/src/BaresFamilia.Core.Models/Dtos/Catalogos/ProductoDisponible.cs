namespace BaresFamilia.Core.Models.Dtos.Catalogos;

/// <summary>
/// Producto tal como lo necesita la grilla del punto de venta: identidad, categoría
/// y precio de venta ya resuelto. Precio queda en 0 si el producto todavía no tiene
/// ninguno configurado.
/// </summary>
public record ProductoDisponible(Guid Id, string Nombre, Guid CategoriaId, decimal Precio);
