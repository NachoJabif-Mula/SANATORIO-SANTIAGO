using BaresFamilia.Core.Models.Dtos.Seguridad;

namespace BaresFamilia.Core.Models.Contratos.Seguridad;

/// <summary>
/// Datos de alta o edición de un empleado. Password es opcional: en la edición,
/// dejarlo vacío conserva la contraseña actual.
/// </summary>
public record DatosEmpleado(
    string Nombre,
    string? Email,
    string PinAcceso,
    Guid RolId,
    Guid SucursalId,
    string? Password);

/// <summary>
/// Credenciales del Backoffice. RememberMe alarga la vigencia del token y hace que
/// la sesión se persista, para poder revocarla antes de que expire.
/// </summary>
public record LoginRequest(string Email, string Password, bool RememberMe = false);

public record LoginResponse(string Token, UsuarioDto Usuario);

/// <summary>
/// PIN con el que un empleado se identifica en la terminal.
/// </summary>
public class LoginPinRequest
{
    public string Pin { get; set; } = string.Empty;
}

/// <summary>
/// Alta de empleado. Password es opcional: un empleado que solo opera el POS
/// entra con PIN y no necesita contraseña de Backoffice.
/// </summary>
public record CreateEmpleadoRequest(
    string Nombre,
    string? Email,
    string PinAcceso,
    Guid RolId,
    Guid SucursalId,
    string? Password
);

/// <summary>
/// Edición de empleado. Dejar Password vacío conserva la contraseña actual.
/// </summary>
public record UpdateEmpleadoRequest(
    string Nombre,
    string? Email,
    string PinAcceso,
    Guid RolId,
    Guid SucursalId,
    string? Password
);

public record CreateRolRequest(string Nombre, List<string> Permisos, bool EsGlobal = false);

public record UpdateRolRequest(string Nombre, List<string> Permisos, bool EsGlobal = false);

public record GenerarCodigoRequest(
    Guid SucursalId,
    string? NombreDispositivo
);

public record GenerarCodigoResponse(
    Guid DispositivoId,
    string CodigoActivacion,
    Guid SucursalId,
    string NombreDispositivo,
    DateTime ExpiraEn
);

/// <summary>
/// Canje del código de activación por un token M2M, hecho desde la Nube.
/// </summary>
public record ActivarRequest(
    string CodigoActivacion
);

public record ActivarResponse(
    string Token,
    string TokenType,
    DateTime ExpiresAt,
    Guid SucursalId,
    string SucursalNombre,
    Guid DispositivoId,
    string NombreDispositivo
);

public record DispositivoDetalleResponse(
    Guid Id,
    string Sucursal,
    string Codigo,
    string Descripcion,
    bool IsActivado,
    DateTime? ActivadoEn,
    DateTime ExpiraEn
);

/// <summary>
/// Código que el operador tipea en la terminal para vincularla con la Nube.
/// </summary>
public class ActivarLocalRequest
{
    public string CodigoActivacion { get; set; } = string.Empty;
}
