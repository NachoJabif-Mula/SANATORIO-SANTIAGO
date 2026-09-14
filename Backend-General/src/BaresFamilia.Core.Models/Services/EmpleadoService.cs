using BaresFamilia.Core.Models.Contratos.Seguridad;
using BaresFamilia.Core.Models.Contratos;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para la gestión de empleados.
///
/// Reglas que concentra: el PIN identifica al empleado dentro de su sucursal (debe
/// ser único ahí), solo un usuario global puede asignar roles globales u operar
/// sobre otras sucursales, y el administrador del sistema no se puede eliminar.
/// </summary>
public class EmpleadoService : IEmpleadoService
{
    /// <summary>Cuenta semilla del sistema: sin ella nadie podría volver a entrar al Backoffice.</summary>
    private const string EmailAdministrador = "admin@baresfamilia.com";

    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRepository<Rol> _rolRepository;
    private readonly IRepository<Sucursal> _sucursalRepository;

    public EmpleadoService(
        IUsuarioRepository usuarioRepository,
        IRepository<Rol> rolRepository,
        IRepository<Sucursal> sucursalRepository)
    {
        _usuarioRepository = usuarioRepository;
        _rolRepository = rolRepository;
        _sucursalRepository = sucursalRepository;
    }

    public async Task<IEnumerable<Usuario>> GetAsync(Guid? sucursalSolicitada, AlcanceUsuario alcance, CancellationToken ct = default)
        => await _usuarioRepository.GetActivosConRolYSucursalAsync(alcance.ResolverSucursalConsultada(sucursalSolicitada), ct);

    public async Task<IEnumerable<Usuario>> GetActivosDeSucursalAsync(Guid sucursalId, CancellationToken ct = default)
        => await _usuarioRepository.GetActivosDeSucursalAsync(sucursalId, ct);

    public async Task<Usuario> CrearAsync(DatosEmpleado datos, AlcanceUsuario alcance, CancellationToken ct = default)
    {
        var sucursalId = ResolverSucursalDestino(datos.SucursalId, alcance);
        var (nombre, pin) = ValidarDatosObligatorios(datos);

        await ValidarPinDisponibleAsync(sucursalId, pin, idExcluido: null, ct);

        var rol = await ObtenerRolValidoAsync(datos.RolId, alcance, ct);
        var sucursal = await ObtenerSucursalValidaAsync(sucursalId, ct);

        var usuario = await _usuarioRepository.AddAsync(new Usuario
        {
            Nombre = nombre,
            Email = NormalizarEmail(datos.Email),
            PinAcceso = pin,
            RolId = datos.RolId,
            SucursalId = sucursalId,
            PasswordHash = string.IsNullOrWhiteSpace(datos.Password)
                ? string.Empty
                : BCrypt.Net.BCrypt.HashPassword(datos.Password),
            IsActive = true
        }, ct);

        // El llamador necesita los nombres para responder sin volver a consultar.
        usuario.Rol = rol;
        usuario.Sucursal = sucursal;
        return usuario;
    }

    public async Task ActualizarAsync(Guid id, DatosEmpleado datos, AlcanceUsuario alcance, CancellationToken ct = default)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id, ct)
            ?? throw new RecursoNoEncontradoException("Empleado no encontrado.");

        // Un usuario no-global solo edita empleados de su propia sucursal y no puede
        // reasignarlos a otra.
        var sucursalId = datos.SucursalId;
        if (!alcance.EsGlobal)
        {
            if (!alcance.PuedeOperarEn(usuario.SucursalId) || alcance.SucursalId is null)
                throw new AccesoDenegadoException();

            sucursalId = alcance.SucursalId.Value;
        }

        var (nombre, pin) = ValidarDatosObligatorios(datos);

        await ValidarPinDisponibleAsync(sucursalId, pin, idExcluido: id, ct);
        await ObtenerRolValidoAsync(datos.RolId, alcance, ct);
        await ObtenerSucursalValidaAsync(sucursalId, ct);

        usuario.Nombre = nombre;
        usuario.Email = NormalizarEmail(datos.Email);
        usuario.PinAcceso = pin;
        usuario.RolId = datos.RolId;
        usuario.SucursalId = sucursalId;

        if (!string.IsNullOrWhiteSpace(datos.Password))
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(datos.Password);

        await _usuarioRepository.UpdateAsync(usuario, ct);
    }

    public async Task DesactivarAsync(Guid id, AlcanceUsuario alcance, CancellationToken ct = default)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id, ct)
            ?? throw new RecursoNoEncontradoException("Empleado no encontrado.");

        if (!alcance.PuedeOperarEn(usuario.SucursalId))
            throw new AccesoDenegadoException();

        if (usuario.Email == EmailAdministrador)
            throw new ReglaNegocioException("No se puede eliminar al usuario administrador del sistema.");

        await _usuarioRepository.DeleteAsync(id, ct);
    }

    /// <summary>
    /// Un usuario no-global siempre trabaja sobre su propia sucursal, sin importar
    /// qué sucursal haya enviado en el request.
    /// </summary>
    private static Guid ResolverSucursalDestino(Guid sucursalSolicitada, AlcanceUsuario alcance)
    {
        if (alcance.EsGlobal)
            return sucursalSolicitada;

        return alcance.SucursalId ?? throw new AccesoDenegadoException();
    }

    private static (string Nombre, string Pin) ValidarDatosObligatorios(DatosEmpleado datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Nombre))
            throw new ReglaNegocioException("El nombre es obligatorio.");

        if (string.IsNullOrWhiteSpace(datos.PinAcceso))
            throw new ReglaNegocioException("El PIN de acceso es obligatorio.");

        return (datos.Nombre.Trim(), datos.PinAcceso.Trim());
    }

    private async Task ValidarPinDisponibleAsync(Guid sucursalId, string pin, Guid? idExcluido, CancellationToken ct)
    {
        if (await _usuarioRepository.ExistePinEnSucursalAsync(sucursalId, pin, idExcluido, ct))
            throw new ReglaNegocioException("El PIN ingresado ya está asignado a otro empleado en esta sucursal.");
    }

    private async Task<Rol> ObtenerRolValidoAsync(Guid rolId, AlcanceUsuario alcance, CancellationToken ct)
    {
        var rol = await _rolRepository.GetByIdAsync(rolId, ct)
            ?? throw new ReglaNegocioException("El rol seleccionado no es válido.");

        // Solo un usuario global puede otorgar alcance global a otro.
        if (rol.EsGlobal && !alcance.EsGlobal)
            throw new AccesoDenegadoException();

        return rol;
    }

    private async Task<Sucursal> ObtenerSucursalValidaAsync(Guid sucursalId, CancellationToken ct)
        => await _sucursalRepository.GetByIdAsync(sucursalId, ct)
            ?? throw new ReglaNegocioException("La sucursal seleccionada no es válida.");

    private static string NormalizarEmail(string? email)
        => email?.Trim().ToLower() ?? string.Empty;
}
