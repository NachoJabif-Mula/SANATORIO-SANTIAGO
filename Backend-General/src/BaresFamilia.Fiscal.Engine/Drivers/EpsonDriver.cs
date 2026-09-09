using System.Runtime.InteropServices;
using System.Text;

namespace BaresFamilia.Fiscal.Engine.Drivers;

/// <summary>
/// Driver nativo propio para controladores fiscales Epson (TM-T900FA, TM-T88).
/// Invoca la DLL oficial del fabricante Epson ("EpsonFiscalInterface.dll") mediante P/Invoke.
/// Implementa thread-locking global estático debido a la falta de reentrancia de la DLL nativa.
/// Escrito 100% desde cero para el proyecto Bares Familia.
/// </summary>
public class EpsonDriver : IFiscalDriver
{
    public string NombreDriver => "Epson";

    /// <summary>
    /// Lock global estático. La DLL oficial de Epson no es thread-safe ni reentrante.
    /// Todos los comandos nativos deben ejecutarse dentro de este lock.
    /// </summary>
    private static readonly object _nativeLock = new();

    #region P/Invoke Native DLL Declarations (EpsonFiscalInterface.dll)

    private const string DLL_NAME = "EpsonFiscalInterface.dll";

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int ConfigurarVelocidad(int velocidad);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int ConfigurarPuerto(string puerto);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int Conectar();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int Desconectar();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int ConsultarEstado(StringBuilder estadoImpresora, StringBuilder estadoFiscal);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int CargarDatosCliente(
        string razonSocial,
        string cuit,
        int tipoDocumento,
        int responsabilidadIva,
        string domicilio);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int AbrirComprobante(int tipoComprobante);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int ImprimirItem(
        string descripcion,
        string cantidad,
        string precioUnitario,
        int alicuotaIva,
        int tipoImpuestoInterno,
        string valorImpuestoInterno);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int CargarAjuste(
        int tipoAjuste,
        string descripcion,
        string monto,
        int alicuotaIva);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int ImprimirSubTotal();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int CargarPago(int codigoPago, string monto, string descripcion);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int CerrarComprobante(StringBuilder numeroComprobante, StringBuilder cae, StringBuilder vtoCae);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int ImprimirCierreZ(StringBuilder numeroZ);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int ImprimirCierreX();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int Cancelar();

    #endregion

    public Task<FiscalResult> ConsultarEstadoAsync(FiscalDeviceConfig config, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            lock (_nativeLock)
            {
                try {
                    ConfigurarVelocidad(config.Velocidad);
                    ConfigurarPuerto(config.Puerto);
                    int resConnect = Conectar();
                    if (resConnect != 0)
                    {
                        return FiscalResult.Error($"Error de conexión con Epson en {config.Puerto} (código {resConnect})", resConnect);
                    }

                    var sbPrinter = new StringBuilder(256);
                    var sbFiscal = new StringBuilder(256);
                    int resStatus = ConsultarEstado(sbPrinter, sbFiscal);
                    Desconectar();

                    if (resStatus == 0)
                    {
                        return FiscalResult.Ok("OK", sbPrinter.ToString(), sbFiscal.ToString());
                    }
                    return FiscalResult.Error($"Error al consultar estado Epson (código {resStatus})", resStatus);
                }
                catch (DllNotFoundException)
                {
                    return FiscalResult.Error($"DLL oficial Epson '{DLL_NAME}' no encontrada. Verifica que esté en la carpeta del ejecutable.");
                }
                catch (Exception ex)
                {
                    return FiscalResult.Error($"Error nativo Epson: {ex.Message}");
                }
                finally
                {
                    try { Desconectar(); } catch { }
                }
            }
        }, ct);
    }

    public Task<FiscalResult> ImprimirFacturaAsync(FiscalDeviceConfig config, FiscalFacturaRequest request, CancellationToken ct = default)
    {
        return EjecutarOperacionComprobanteAsync(config, request, esNotaCredito: false, ct);
    }

    public Task<FiscalResult> ImprimirNotaCreditoAsync(FiscalDeviceConfig config, FiscalFacturaRequest request, CancellationToken ct = default)
    {
        return EjecutarOperacionComprobanteAsync(config, request, esNotaCredito: true, ct);
    }

    private Task<FiscalResult> EjecutarOperacionComprobanteAsync(FiscalDeviceConfig config, FiscalFacturaRequest request, bool esNotaCredito, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            lock (_nativeLock)
            {
                try
                {
                    ConfigurarVelocidad(config.Velocidad);
                    ConfigurarPuerto(config.Puerto);

                    int err = Conectar();
                    if (err != 0) return FiscalResult.Error($"Falló conexión a puerto Epson {config.Puerto} (code {err})", err);

                    // 1. Cargar Cliente
                    var cliente = request.Cliente;
                    int tipoDoc = string.IsNullOrWhiteSpace(cliente.Cuit) ? 0 : 3; // 3 = CUIT
                    err = CargarDatosCliente(
                        cliente.Nombre,
                        cliente.Cuit.Replace("-", ""),
                        tipoDoc,
                        cliente.CondicionIva,
                        cliente.Domicilio
                    );

                    // 2. Abrir Comprobante
                    err = AbrirComprobante(request.TipoComprobante);
                    if (err != 0)
                    {
                        Cancelar();
                        return FiscalResult.Error($"Error al abrir comprobante tipo {request.TipoComprobante} (code {err})", err);
                    }

                    // 3. Imprimir Items
                    foreach (var item in request.Items)
                    {
                        err = ImprimirItem(
                            item.Descripcion,
                            item.Cantidad.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            item.PrecioUnitarioConIva.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                            item.AlicuotaIvaCode,
                            0, ""
                        );
                        if (err != 0)
                        {
                            Cancelar();
                            return FiscalResult.Error($"Error al imprimir ítem '{item.Descripcion}' (code {err})", err);
                        }
                    }

                    // 4. Descuento general si aplica
                    if (request.DescuentoGeneralMonto > 0)
                    {
                        ImprimirSubTotal();
                        CargarAjuste(0, "Descuento General", request.DescuentoGeneralMonto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), FiscalConstants.IVA_21);
                    }

                    // 5. Cargar Pagos
                    foreach (var pago in request.Pagos)
                    {
                        CargarPago(pago.FormaPagoCode, pago.Monto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), pago.Descripcion);
                    }

                    // 6. Cerrar Comprobante
                    var sbNum = new StringBuilder(64);
                    var sbCae = new StringBuilder(64);
                    var sbVto = new StringBuilder(64);
                    err = CerrarComprobante(sbNum, sbCae, sbVto);

                    if (err == 0)
                    {
                        return FiscalResult.Ok(sbNum.ToString(), sbCae.ToString(), sbVto.ToString());
                    }

                    Cancelar();
                    return FiscalResult.Error($"Error al cerrar comprobante Epson (code {err})", err);
                }
                catch (DllNotFoundException)
                {
                    return FiscalResult.Error($"DLL oficial Epson '{DLL_NAME}' no encontrada. Requerida para impresoras Epson físicas.");
                }
                catch (Exception ex)
                {
                    return FiscalResult.Error($"Excepción nativa Epson: {ex.Message}");
                }
                finally
                {
                    try { Desconectar(); } catch { }
                }
            }
        }, ct);
    }

    public Task<FiscalResult> EjecutarCierreZAsync(FiscalDeviceConfig config, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            lock (_nativeLock)
            {
                try
                {
                    ConfigurarVelocidad(config.Velocidad);
                    ConfigurarPuerto(config.Puerto);
                    int err = Conectar();
                    if (err != 0) return FiscalResult.Error($"No se pudo conectar a la impresora Epson (code {err})", err);

                    var sbZ = new StringBuilder(64);
                    err = ImprimirCierreZ(sbZ);
                    Desconectar();

                    if (err == 0) return FiscalResult.Ok(sbZ.ToString());
                    return FiscalResult.Error($"Error al realizar Cierre Z en Epson (code {err})", err);
                }
                catch (Exception ex)
                {
                    return FiscalResult.Error($"Error en Cierre Z Epson: {ex.Message}");
                }
                finally
                {
                    try { Desconectar(); } catch { }
                }
            }
        }, ct);
    }

    public Task<FiscalResult> EjecutarCierreXAsync(FiscalDeviceConfig config, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            lock (_nativeLock)
            {
                try
                {
                    ConfigurarVelocidad(config.Velocidad);
                    ConfigurarPuerto(config.Puerto);
                    int err = Conectar();
                    if (err != 0) return FiscalResult.Error($"No se pudo conectar a la impresora Epson (code {err})", err);

                    err = ImprimirCierreX();
                    Desconectar();

                    if (err == 0) return FiscalResult.Ok("CierreX_OK");
                    return FiscalResult.Error($"Error al realizar Cierre X en Epson (code {err})", err);
                }
                catch (Exception ex)
                {
                    return FiscalResult.Error($"Error en Cierre X Epson: {ex.Message}");
                }
                finally
                {
                    try { Desconectar(); } catch { }
                }
            }
        }, ct);
    }

    public Task<FiscalResult> CancelarComprobanteAsync(FiscalDeviceConfig config, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            lock (_nativeLock)
            {
                try
                {
                    ConfigurarVelocidad(config.Velocidad);
                    ConfigurarPuerto(config.Puerto);
                    Conectar();
                    int err = Cancelar();
                    Desconectar();
                    return FiscalResult.Ok("Cancelado");
                }
                catch (Exception ex)
                {
                    return FiscalResult.Error($"Error al cancelar comprobante en Epson: {ex.Message}");
                }
                finally
                {
                    try { Desconectar(); } catch { }
                }
            }
        }, ct);
    }
}
