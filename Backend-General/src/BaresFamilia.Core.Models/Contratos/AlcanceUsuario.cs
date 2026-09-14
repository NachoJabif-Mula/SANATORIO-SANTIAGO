namespace BaresFamilia.Core.Models.Contratos;

/// <summary>
/// Alcance del usuario autenticado, tal como lo declaran sus claims.
/// Un usuario global opera sobre todas las sucursales; el resto queda acotado
/// a la propia, sin importar qué sucursal pida por parámetro.
/// </summary>
public record AlcanceUsuario(bool EsGlobal, Guid? SucursalId)
{
    /// <summary>
    /// Resuelve la sucursal efectiva de una consulta: un usuario global recibe la
    /// que pidió (o null para ver todas), y el resto siempre la propia.
    /// </summary>
    public Guid? ResolverSucursalConsultada(Guid? sucursalSolicitada)
        => EsGlobal ? sucursalSolicitada : SucursalId;

    /// <summary>
    /// Indica si puede operar sobre datos de la sucursal indicada.
    /// </summary>
    public bool PuedeOperarEn(Guid sucursalId)
        => EsGlobal || SucursalId == sucursalId;
}
