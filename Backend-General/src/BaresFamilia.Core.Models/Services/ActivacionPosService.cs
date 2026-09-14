using System.Text.Json;
using BaresFamilia.Core.Models.Contratos.Seguridad;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.Seguridad;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.Extensions.Logging;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de activación del POS de la sucursal.
///
/// El cuerpo completo de la respuesta de la Nube se guarda en TokenHash porque es
/// de donde el worker de sincronización relee el token M2M en cada ciclo.
/// </summary>
public class ActivacionPosService : IActivacionPosService
{
    private const int AniosDeVigenciaDeLaActivacion = 1;

    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    private readonly IActivacionNubeClient _nubeClient;
    private readonly IDispositivoActivacionRepository _dispositivoRepository;
    private readonly IRepository<Sucursal> _sucursalRepository;
    private readonly ILogger<ActivacionPosService> _logger;

    public ActivacionPosService(
        IActivacionNubeClient nubeClient,
        IDispositivoActivacionRepository dispositivoRepository,
        IRepository<Sucursal> sucursalRepository,
        ILogger<ActivacionPosService> logger)
    {
        _nubeClient = nubeClient;
        _dispositivoRepository = dispositivoRepository;
        _sucursalRepository = sucursalRepository;
        _logger = logger;
    }

    public async Task<EstadoActivacionPos?> GetEstadoAsync(CancellationToken ct = default)
    {
        var activacion = (await _dispositivoRepository.GetActivadasAsync(ct)).FirstOrDefault();
        if (activacion is null || string.IsNullOrWhiteSpace(activacion.TokenHash))
            return null;

        DatosActivacion? datos;
        try
        {
            datos = JsonSerializer.Deserialize<DatosActivacion>(activacion.TokenHash, OpcionesJson);
        }
        catch (JsonException ex)
        {
            // Una activación con payload corrupto equivale a no estar vinculado:
            // el POS volverá a pedir el código en vez de fallar el arranque.
            _logger.LogWarning(ex, "La activación local {DispositivoId} tiene un payload ilegible.", activacion.Id);
            return null;
        }

        if (datos is null || string.IsNullOrWhiteSpace(datos.Token))
            return null;

        return new EstadoActivacionPos(
            datos.SucursalId,
            datos.SucursalNombre,
            datos.DispositivoId,
            datos.NombreDispositivo,
            datos.ExpiresAt);
    }

    public async Task ActivarAsync(string codigoActivacion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigoActivacion))
            throw new ReglaNegocioException("El código de activación es obligatorio.");

        var codigo = ActivacionDispositivoService.NormalizarCodigo(codigoActivacion);
        var respuesta = await _nubeClient.CanjearCodigoAsync(codigo, ct);

        await AsegurarSucursalLocalAsync(respuesta.Datos, ct);
        await DarDeBajaActivacionesPreviasAsync(ct);

        await _dispositivoRepository.AddAsync(new DispositivoActivacion
        {
            Id = respuesta.Datos.DispositivoId,
            SucursalId = respuesta.Datos.SucursalId,
            CodigoActivacion = codigo,
            NombreDispositivo = respuesta.Datos.NombreDispositivo,
            Activado = true,
            FechaActivacion = DateTime.UtcNow,
            ExpiraCodigo = DateTime.UtcNow.AddYears(AniosDeVigenciaDeLaActivacion),
            TokenHash = respuesta.CuerpoCrudo
        }, ct);

        _logger.LogInformation(
            "POS activado contra la Nube. Sucursal {SucursalId}, dispositivo {DispositivoId}.",
            respuesta.Datos.SucursalId, respuesta.Datos.DispositivoId);
    }

    public async Task DesactivarAsync(CancellationToken ct = default)
        => await DarDeBajaActivacionesPreviasAsync(ct);

    /// <summary>
    /// La sucursal todavía no llegó por sincronización en la primera activación, así
    /// que se crea acá para no violar la clave foránea del dispositivo.
    /// </summary>
    private async Task AsegurarSucursalLocalAsync(DatosActivacion datos, CancellationToken ct)
    {
        if (await _sucursalRepository.ExistsAsync(datos.SucursalId, ct))
            return;

        await _sucursalRepository.AddAsync(new Sucursal
        {
            Id = datos.SucursalId,
            Nombre = datos.SucursalNombre,
            Direccion = "Local"
        }, ct);
    }

    /// <summary>
    /// Baja lógica en lugar de borrado: conserva el historial de qué dispositivo y
    /// sucursal estuvieron vinculados antes.
    /// </summary>
    private async Task DarDeBajaActivacionesPreviasAsync(CancellationToken ct)
    {
        var previas = (await _dispositivoRepository.GetActivadasAsync(ct)).ToList();
        if (previas.Count == 0)
            return;

        foreach (var previa in previas)
        {
            previa.Activado = false;
            previa.IsActive = false;
            previa.UpdatedAt = DateTime.UtcNow;
        }

        await _dispositivoRepository.GuardarCambiosAsync(previas, ct);
    }
}
