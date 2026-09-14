using System.Security.Cryptography;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de activación de dispositivos POS en la Nube.
/// </summary>
public class ActivacionDispositivoService : IActivacionDispositivoService
{
    /// <summary>Alfabeto sin caracteres ambiguos (0/O, 1/I/L): el código se dicta y se tipea a mano.</summary>
    private const string CaracteresCodigo = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int HorasDeVigenciaDelCodigo = 24;

    private readonly IDispositivoActivacionRepository _dispositivoRepository;
    private readonly IRepository<Sucursal> _sucursalRepository;

    public ActivacionDispositivoService(
        IDispositivoActivacionRepository dispositivoRepository,
        IRepository<Sucursal> sucursalRepository)
    {
        _dispositivoRepository = dispositivoRepository;
        _sucursalRepository = sucursalRepository;
    }

    public async Task<DispositivoActivacion> GenerarCodigoAsync(Guid sucursalId, string? nombreDispositivo, CancellationToken ct = default)
    {
        if (!await _sucursalRepository.ExistsAsync(sucursalId, ct))
            throw new ReglaNegocioException($"La sucursal con ID '{sucursalId}' no existe o está inactiva.");

        return await _dispositivoRepository.AddAsync(new DispositivoActivacion
        {
            SucursalId = sucursalId,
            CodigoActivacion = GenerarCodigoAlfanumerico(),
            NombreDispositivo = string.IsNullOrWhiteSpace(nombreDispositivo) ? "POS Sin Nombre" : nombreDispositivo.Trim(),
            Activado = false,
            ExpiraCodigo = DateTime.UtcNow.AddHours(HorasDeVigenciaDelCodigo)
        }, ct);
    }

    public async Task<DispositivoActivacion> ValidarCodigoCanjeableAsync(string codigoActivacion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigoActivacion))
            throw new ReglaNegocioException("El código de activación es obligatorio.");

        var dispositivo = await _dispositivoRepository.GetPorCodigoAsync(NormalizarCodigo(codigoActivacion), ct)
            ?? throw new RecursoNoEncontradoException("Código de activación no encontrado.");

        // Es de un solo uso: si ya se canjeó, el POS debe pedir uno nuevo al Backoffice.
        if (dispositivo.Activado)
            throw new ReglaNegocioException("Este código ya fue utilizado. Solicite uno nuevo.");

        if (dispositivo.ExpiraCodigo < DateTime.UtcNow)
            throw new ReglaNegocioException("El código de activación ha expirado. Solicite uno nuevo.");

        return dispositivo;
    }

    public async Task ConfirmarActivacionAsync(DispositivoActivacion dispositivo, string token, CancellationToken ct = default)
    {
        dispositivo.Activado = true;
        dispositivo.FechaActivacion = DateTime.UtcNow;
        dispositivo.TokenHash = TokenHasher.Compute(token);

        await _dispositivoRepository.UpdateAsync(dispositivo, ct);
    }

    public async Task<IEnumerable<DispositivoActivacion>> ListarVigentesAsync(CancellationToken ct = default)
        => await _dispositivoRepository.GetVigentesConSucursalAsync(ct);

    public async Task RevocarAsync(Guid id, CancellationToken ct = default)
    {
        if (!await _dispositivoRepository.ExistsAsync(id, ct))
            throw new RecursoNoEncontradoException("Dispositivo no encontrado o ya inactivo.");

        await _dispositivoRepository.DeleteAsync(id, ct);
    }

    public async Task<bool?> EstaActivoAsync(Guid id, CancellationToken ct = default)
    {
        var dispositivo = await _dispositivoRepository.GetPorIdIncluyendoInactivosAsync(id, ct);
        return dispositivo is null ? null : dispositivo.IsActive && dispositivo.Activado;
    }

    /// <summary>
    /// Genera un código con formato BAR-XXXX-XXXX.
    /// </summary>
    private static string GenerarCodigoAlfanumerico()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);

        var primerBloque = new string(bytes.Take(4).Select(b => CaracteresCodigo[b % CaracteresCodigo.Length]).ToArray());
        var segundoBloque = new string(bytes.Skip(4).Select(b => CaracteresCodigo[b % CaracteresCodigo.Length]).ToArray());

        return $"BAR-{primerBloque}-{segundoBloque}";
    }

    /// <summary>
    /// Los códigos se emiten en mayúsculas; el POS puede enviarlos con espacios o en minúscula.
    /// </summary>
    public static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpper();
}
