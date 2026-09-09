using System.Net.Sockets;
using System.Text;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.Extensions.Logging;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de impresión configurable.
/// Lee la configuración de impresoras y tipos de ticket desde la base de datos,
/// renderiza templates con placeholders y envía a las impresoras habilitadas
/// por tipo de ticket por sucursal.
/// </summary>
public class ImpresoraService : IImpresoraService
{
    private readonly IRepository<Impresora> _impresoraRepo;
    private readonly IRepository<TipoTicket> _tipoTicketRepo;
    private readonly IRepository<ImpresoraTicketTipo> _asignacionRepo;
    private readonly ILogger<ImpresoraService> _logger;

    public ImpresoraService(
        IRepository<Impresora> impresoraRepo,
        IRepository<TipoTicket> tipoTicketRepo,
        IRepository<ImpresoraTicketTipo> asignacionRepo,
        ILogger<ImpresoraService> logger)
    {
        _impresoraRepo = impresoraRepo;
        _tipoTicketRepo = tipoTicketRepo;
        _asignacionRepo = asignacionRepo;
        _logger = logger;
    }

    /// <summary>
    /// Imprime un ticket del tipo especificado en todas las impresoras
    /// de la sucursal que tengan ese tipo habilitado.
    /// </summary>
    public async Task<List<ResultadoImpresion>> ImprimirTicketAsync(
        Guid sucursalId,
        string tipoTicketCodigo,
        Comanda comanda,
        Dictionary<string, string>? datosExtra = null,
        CancellationToken ct = default)
    {
        var resultados = new List<ResultadoImpresion>();

        try
        {
            // 1. Buscar el tipo de ticket por código
            var tiposTicket = await _tipoTicketRepo.GetAllAsync(ct);
            var tipoTicket = tiposTicket.FirstOrDefault(t => t.Codigo == tipoTicketCodigo && t.IsActive);

            if (tipoTicket is null)
            {
                _logger.LogWarning("Tipo de ticket '{Codigo}' no encontrado o inactivo.", tipoTicketCodigo);
                resultados.Add(new ResultadoImpresion
                {
                    Exitoso = false,
                    Mensaje = $"Tipo de ticket '{tipoTicketCodigo}' no configurado."
                });
                return resultados;
            }

            // 2. Buscar las asignaciones de este tipo de ticket
            var asignaciones = await _asignacionRepo.GetAllAsync(ct);
            var asignacionesTipo = asignaciones
                .Where(a => a.TipoTicketId == tipoTicket.Id && a.IsActive)
                .ToList();

            if (asignacionesTipo.Count == 0)
            {
                _logger.LogInformation(
                    "No hay impresoras asignadas al tipo de ticket '{Codigo}'. Omitiendo impresión.",
                    tipoTicketCodigo);
                resultados.Add(new ResultadoImpresion
                {
                    Exitoso = true,
                    Mensaje = $"No hay impresoras configuradas para '{tipoTicketCodigo}'."
                });
                return resultados;
            }

            // 3. Buscar las impresoras de la sucursal
            var impresoras = await _impresoraRepo.GetAllAsync(ct);
            var impresorasSucursal = impresoras
                .Where(i => i.SucursalId == sucursalId && i.IsActive)
                .ToList();

            // 4. Filtrar solo las que tienen este tipo habilitado
            var impresorasIds = asignacionesTipo.Select(a => a.ImpresoraId).ToHashSet();
            var impresorasDestino = impresorasSucursal
                .Where(i => impresorasIds.Contains(i.Id))
                .ToList();

            if (impresorasDestino.Count == 0)
            {
                _logger.LogInformation(
                    "No hay impresoras activas en la sucursal {SucursalId} para el tipo '{Codigo}'.",
                    sucursalId, tipoTicketCodigo);
                resultados.Add(new ResultadoImpresion
                {
                    Exitoso = true,
                    Mensaje = $"No hay impresoras activas en esta sucursal para '{tipoTicketCodigo}'."
                });
                return resultados;
            }

            // 5. Renderizar el template
            var contenidoTicket = RenderizarTemplate(tipoTicket.TemplateContenido, comanda, datosExtra);

            // 6. Enviar a cada impresora destino
            foreach (var impresora in impresorasDestino)
            {
                var resultado = await EnviarAImpresoraAsync(impresora, contenidoTicket, ct);
                resultados.Add(resultado);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico al imprimir ticket '{Codigo}' para sucursal {SucursalId}",
                tipoTicketCodigo, sucursalId);
            resultados.Add(new ResultadoImpresion
            {
                Exitoso = false,
                Mensaje = $"Error de impresión: {ex.Message}"
            });
        }

        return resultados;
    }

    // ═══════════════════════════════════════════════════════
    // RENDERIZADO DE TEMPLATE
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Reemplaza los placeholders del template con los datos de la comanda.
    /// </summary>
    private static string RenderizarTemplate(
        string template,
        Comanda comanda,
        Dictionary<string, string>? datosExtra)
    {
        // Construir la lista de ítems como texto
        var itemsBuilder = new StringBuilder();
        if (comanda.Items?.Any() == true)
        {
            foreach (var item in comanda.Items)
            {
                itemsBuilder.AppendLine($"  {item.Cantidad}x {item.Producto?.Nombre ?? "Producto"}");
                if (!string.IsNullOrWhiteSpace(item.Notas))
                {
                    itemsBuilder.AppendLine($"     → {item.Notas}");
                }
            }
        }

        var totalItems = comanda.Items?.Sum(i => i.Cantidad) ?? 0;

        // Reemplazar placeholders
        var resultado = template
            .Replace("{{NEGOCIO}}", "BARES FAMILIA")
            .Replace("{{SUCURSAL}}", comanda.Mesa?.Sucursal?.Nombre ?? "—")
            .Replace("{{FECHA}}", DateTime.Now.ToString("dd/MM/yyyy"))
            .Replace("{{HORA}}", DateTime.Now.ToString("HH:mm:ss"))
            .Replace("{{COMANDA_ID}}", comanda.Id.ToString()[..8].ToUpper())
            .Replace("{{MESA}}", comanda.Mesa?.Etiqueta ?? "BARRA/MOSTRADOR")
            .Replace("{{MOZO}}", comanda.Usuario?.Nombre ?? "—")
            .Replace("{{ITEMS}}", itemsBuilder.ToString().TrimEnd())
            .Replace("{{TOTAL_ITEMS}}", totalItems.ToString())
            .Replace("{{SUBTOTAL}}", comanda.Subtotal.ToString("N2"))
            .Replace("{{DESCUENTO}}", comanda.Descuento.ToString("N2"))
            .Replace("{{TOTAL}}", comanda.Total.ToString("N2"));

        // Reemplazar datos extra (CAE, comprobante, etc.)
        if (datosExtra is not null)
        {
            foreach (var (key, value) in datosExtra)
            {
                resultado = resultado.Replace($"{{{{{key}}}}}", value);
            }
        }

        return resultado;
    }

    // ═══════════════════════════════════════════════════════
    // ENVÍO A IMPRESORA
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Envía el contenido del ticket a una impresora según su tipo de conexión.
    /// </summary>
    private async Task<ResultadoImpresion> EnviarAImpresoraAsync(
        Impresora impresora, string contenido, CancellationToken ct)
    {
        var resultado = new ResultadoImpresion();
        var identificador = impresora.TipoConexion == TipoConexionImpresora.Red
            ? $"TCP:{impresora.Direccion}:{impresora.Puerto}"
            : $"USB:{impresora.Direccion}";

        try
        {
            if (impresora.TipoConexion == TipoConexionImpresora.USB)
            {
                await EnviarPorUsbAsync(impresora.Direccion, contenido, ct);
            }
            else
            {
                await EnviarPorRedAsync(impresora.Direccion, impresora.Puerto, contenido, ct);
            }

            resultado.Exitoso = true;
            resultado.Mensaje = $"Ticket impreso correctamente en '{impresora.Nombre}'.";
            resultado.ImpresoraUtilizada = identificador;
            resultado.TicketContenido = contenido;

            _logger.LogInformation(
                "✓ Ticket impreso en '{Nombre}' ({Identificador})",
                impresora.Nombre, identificador);
        }
        catch (Exception ex)
        {
            resultado.Exitoso = false;
            resultado.Mensaje = $"Error al imprimir en '{impresora.Nombre}': {ex.Message}";
            resultado.ImpresoraUtilizada = identificador;

            _logger.LogError(ex,
                "✗ Fallo al imprimir en '{Nombre}' ({Identificador})",
                impresora.Nombre, identificador);
        }

        return resultado;
    }

    /// <summary>
    /// Envía el ticket a una impresora conectada por Red TCP/IP.
    /// </summary>
    private async Task EnviarPorRedAsync(string ip, int puerto, string contenido, CancellationToken ct)
    {
        _logger.LogInformation("Enviando ticket a impresora de red {Ip}:{Puerto}...", ip, puerto);

        using var client = new TcpClient();
        await client.ConnectAsync(ip, puerto, ct);

        var stream = client.GetStream();
        var bytes = Encoding.GetEncoding("ibm850").GetBytes(contenido);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);

        _logger.LogInformation("Ticket enviado por Red exitosamente. {Bytes} bytes.", bytes.Length);
    }

    /// <summary>
    /// Envía el ticket a una impresora USB usando el spooler de Windows.
    /// </summary>
    private async Task EnviarPorUsbAsync(string nombreImpresora, string contenido, CancellationToken ct)
    {
        _logger.LogInformation("Enviando ticket a impresora USB '{Printer}'...", nombreImpresora);

        var tempFile = Path.GetTempFileName();
        try
        {
            var bytes = Encoding.GetEncoding("ibm850").GetBytes(contenido);
            await File.WriteAllBytesAsync(tempFile, bytes, ct);

            var printerPath = nombreImpresora.StartsWith(@"\\")
                ? nombreImpresora
                : $@"\\.\{nombreImpresora}";

            using var printerStream = new FileStream(printerPath, FileMode.Open, FileAccess.Write);
            await printerStream.WriteAsync(bytes, ct);

            _logger.LogInformation("Ticket enviado por USB exitosamente. {Bytes} bytes.", bytes.Length);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
