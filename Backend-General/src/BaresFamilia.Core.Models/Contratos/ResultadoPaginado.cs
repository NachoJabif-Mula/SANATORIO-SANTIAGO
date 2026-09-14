namespace BaresFamilia.Core.Models.Contratos;

/// <summary>
/// Página de resultados de una consulta, junto con el total de registros que
/// cumplen el filtro (no solo los de esta página).
/// </summary>
public record ResultadoPaginado<T>(int Total, int Pagina, int TamanoPagina, IReadOnlyList<T> Items);
