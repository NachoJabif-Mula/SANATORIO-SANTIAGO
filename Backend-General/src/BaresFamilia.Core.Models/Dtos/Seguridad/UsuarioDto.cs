namespace BaresFamilia.Core.Models.Dtos.Seguridad;

/// <summary>
/// Perfil del usuario autenticado. EsGlobal indica si opera sobre todas las
/// sucursales o solo sobre la propia.
/// </summary>
public record UsuarioDto(Guid Id, string Nombre, string Email, string Rol, string Sucursal, Guid SucursalId, bool EsGlobal);

public record EmpleadoDto(
    Guid Id,
    string Nombre,
    string Email,
    string PinAcceso,
    Guid RolId,
    string RolNombre,
    Guid SucursalId,
    string SucursalNombre,
    bool IsActive
);
