using System.Collections.Generic;
using System.Linq;
using BaresFamilia.Core.Models.Contratos.Comandas;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Fiscal;
using BaresFamilia.Core.Models.Entities.Inventario;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Interfaces;
using BaresFamilia.Fiscal.Engine;
using Microsoft.Extensions.Logging;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio para Comanda.
/// Flujo: ComandaController → IComandaService → ComandaService → IComandaRepository → ComandaRepository.
/// Incluye integración simulada con AFIP WSFEv1 para facturación electrónica
/// e impresión configurable de tickets en impresoras de Caja y Cocina.
/// </summary>
public class ComandaService : GenericService<Comanda>, IComandaService
{
    private readonly IComandaRepository _comandaRepository;
    private readonly IRepository<Pago> _pagoRepository;
    private readonly IRepository<MetodoPago> _metodoPagoRepository;
    private readonly IImpresoraService _impresoraService;
    private readonly IRepository<Producto> _productoRepository;
    private readonly IRepository<Mesa> _mesaRepository;
    private readonly IRepository<TurnoCaja> _turnoCajaRepository;
    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IRepository<CuentaCorriente> _cuentaCorrienteRepository;
    private readonly IRepository<MovimientoCuentaCorriente> _movimientoCCRepository;
    private readonly IRepository<Rol> _rolRepository;
    private readonly IRepository<ComandaItem> _comandaItemRepository;
    private readonly IRepository<Receta> _recetaRepository;
    private readonly IRepository<StockSucursal> _stockSucursalRepository;
    private readonly IFacturacionElectronicaService _facturacionElectronicaService;
    private readonly ILogger<ComandaService> _logger;

    public ComandaService(
        IComandaRepository repository,
        IRepository<Pago> pagoRepository,
        IRepository<MetodoPago> metodoPagoRepository,
        IImpresoraService impresoraService,
        IRepository<Producto> productoRepository,
        IRepository<Mesa> mesaRepository,
        IRepository<TurnoCaja> turnoCajaRepository,
        IRepository<Usuario> usuarioRepository,
        IClienteRepository clienteRepository,
        IRepository<CuentaCorriente> cuentaCorrienteRepository,
        IRepository<MovimientoCuentaCorriente> movimientoCCRepository,
        IRepository<Rol> rolRepository,
        IRepository<ComandaItem> comandaItemRepository,
        IRepository<Receta> recetaRepository,
        IRepository<StockSucursal> stockSucursalRepository,
        IFacturacionElectronicaService facturacionElectronicaService,
        ILogger<ComandaService> logger) : base(repository)
    {
        _comandaRepository = repository;
        _pagoRepository = pagoRepository;
        _metodoPagoRepository = metodoPagoRepository;
        _impresoraService = impresoraService;
        _productoRepository = productoRepository;
        _mesaRepository = mesaRepository;
        _turnoCajaRepository = turnoCajaRepository;
        _usuarioRepository = usuarioRepository;
        _clienteRepository = clienteRepository;
        _cuentaCorrienteRepository = cuentaCorrienteRepository;
        _movimientoCCRepository = movimientoCCRepository;
        _rolRepository = rolRepository;
        _comandaItemRepository = comandaItemRepository;
        _recetaRepository = recetaRepository;
        _stockSucursalRepository = stockSucursalRepository;
        _facturacionElectronicaService = facturacionElectronicaService;
        _logger = logger;
    }

    /// <summary>
    /// Crea una comanda y, si contiene ítems que requieren cocina,
    /// envía automáticamente el ticket de tipo "Comanda" a las impresoras habilitadas.
    /// </summary>
    public override async Task<Comanda> CreateAsync(Comanda entity, CancellationToken ct = default)
    {
        if (entity.ClienteId.HasValue)
        {
            // Cuenta corriente abierta: queda exenta de turno/fecha contable hasta que se
            // cierre (cobre); recién ahí se le asigna el turno/caja con el que se liquida.
            entity.FechaContable = null;
            entity.Turno = null;
        }
        else
        {
            // Obtener el turno de caja activo
            var turnosActivos = await _turnoCajaRepository.FindAsync(t => t.FechaCierre == null && t.IsActive, ct);
            var activeTurno = turnosActivos.OrderByDescending(t => t.FechaApertura).FirstOrDefault();
            if (activeTurno != null)
            {
                entity.FechaContable = DateTime.SpecifyKind(activeTurno.FechaContable, DateTimeKind.Utc);
                entity.Turno = activeTurno.Turno;
            }
            else
            {
                entity.FechaContable = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                entity.Turno = "AM";
            }
        }

        var created = await base.CreateAsync(entity, ct);

        // Actualizar el estado de ocupado de la mesa (si aplica) y resolver la sucursal
        // para el descuento de stock (una cuenta corriente no tiene mesa: se resuelve por el usuario).
        Mesa? mesa = null;
        if (created.MesaId.HasValue)
        {
            mesa = await _mesaRepository.GetByIdAsync(created.MesaId.Value, ct);
            if (mesa != null)
            {
                mesa.Ocupada = true;
                mesa.UpdatedAt = DateTime.UtcNow;
                await _mesaRepository.UpdateAsync(mesa, ct);
            }
        }

        // Descontar stock de los ítems recién comandados (solo afecta productos con receta cargada)
        var sucursalId = mesa?.SucursalId;
        if (sucursalId is null)
        {
            var usuarioComanda = await _usuarioRepository.GetByIdAsync(created.UsuarioId, ct);
            sucursalId = usuarioComanda?.SucursalId;
        }
        if (sucursalId.HasValue)
        {
            foreach (var item in created.Items)
            {
                await AjustarStockPorProductoAsync(item.ProductoId, item.Cantidad, sucursalId.Value, restituir: false, ct);
            }
        }

        // Intentar enviar ticket de comanda a las impresoras configuradas
        var resultadosImpresion = await EnviarTicketComandaAsync(created.Id, ct);
        created.UltimaImpresion = ConsolidarResultadoImpresion(resultadosImpresion);

        return created;
    }

    /// <summary>
    /// Descuenta (o restituye) stock de los insumos usados en la receta de un producto,
    /// proporcional a la cantidad vendida/devuelta. No hace nada si el producto no tiene
    /// receta cargada (no todos los productos requieren control de stock).
    /// </summary>
    private async Task AjustarStockPorProductoAsync(Guid productoId, int cantidad, Guid sucursalId, bool restituir, CancellationToken ct)
    {
        if (cantidad <= 0) return;

        var recetas = await _recetaRepository.FindAsync(r => r.ProductoId == productoId && r.IsActive, ct);
        foreach (var receta in recetas)
        {
            var cantidadAjuste = receta.CantidadNecesaria * cantidad;
            var stocks = await _stockSucursalRepository.FindAsync(s => s.SucursalId == sucursalId && s.InsumoId == receta.InsumoId, ct);
            var stock = stocks.FirstOrDefault();

            if (stock == null)
            {
                if (restituir) continue; // No hay registro de stock local; nada que restituir.
                stock = new StockSucursal
                {
                    SucursalId = sucursalId,
                    InsumoId = receta.InsumoId,
                    CantidadActual = 0
                };
                await _stockSucursalRepository.AddAsync(stock, ct);
            }

            stock.CantidadActual += restituir ? cantidadAjuste : -cantidadAjuste;
            stock.UpdatedAt = DateTime.UtcNow;
            await _stockSucursalRepository.UpdateAsync(stock, ct);

            _logger.LogInformation(
                "Stock ajustado por venta: Insumo {InsumoId} en Sucursal {SucursalId} {Signo}{Cantidad}. Nuevo stock: {Nuevo}",
                receta.InsumoId, sucursalId, restituir ? "+" : "-", cantidadAjuste, stock.CantidadActual);
        }
    }

    public async Task<Comanda?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _comandaRepository.GetWithDetailsAsync(id, ct);

    public async Task<IEnumerable<Comanda>> GetAbierdasPorMesaAsync(Guid mesaId, CancellationToken ct = default)
        => await _comandaRepository.GetAbierdasPorMesaAsync(mesaId, ct);

    public async Task<IEnumerable<Comanda>> GetAbiertasPorClienteAsync(Guid clienteId, CancellationToken ct = default)
        => await _comandaRepository.GetAbiertasPorClienteAsync(clienteId, ct);

    /// <summary>
    /// Envía una comanda a preparación: carga los detalles y envía el ticket
    /// de tipo "Comanda" a las impresoras habilitadas de la sucursal.
    /// </summary>
    public async Task<ResultadoImpresion> EnviarAPreparacionAsync(Guid comandaId, CancellationToken ct = default)
    {
        var resultados = await EnviarTicketComandaAsync(comandaId, ct);
        return ConsolidarResultadoImpresion(resultados);
    }

    /// <summary>
    /// Reduce la lista de resultados por-impresora (una comanda puede imprimirse en varias)
    /// a un único resumen: éxito global, impresoras usadas, y el primer ticket renderizado
    /// (para mostrarlo en pantalla en modo simulador).
    /// </summary>
    private static ResultadoImpresion ConsolidarResultadoImpresion(List<ResultadoImpresion> resultados)
    {
        if (resultados.Count == 0)
            return new ResultadoImpresion { Exitoso = true, Mensaje = "No hay impresoras configuradas." };

        var exitosos = resultados.Count(r => r.Exitoso);
        var fallidos = resultados.Count - exitosos;

        return new ResultadoImpresion
        {
            Exitoso = fallidos == 0,
            Mensaje = $"Impresión: {exitosos} exitosa(s), {fallidos} fallida(s).",
            ImpresoraUtilizada = string.Join(", ", resultados.Where(r => r.ImpresoraUtilizada != null).Select(r => r.ImpresoraUtilizada)),
            TicketContenido = resultados.FirstOrDefault(r => r.TicketContenido != null)?.TicketContenido,
            Simulado = resultados.Any(r => r.Simulado)
        };
    }

    /// <summary>
    /// Procesa el cobro de una comanda:
    /// 1. Valida que la comanda existe y está abierta
    /// 2. Registra el pago
    /// 3. Si el método de pago requiere factura AFIP → simula WSFEv1
    /// 4. Marca la comanda como cobrada
    /// 5. Imprime factura en las impresoras configuradas
    /// </summary>
    public async Task<ResultadoCobro> CobrarComandaAsync(
        Guid comandaId, Guid turnoCajaId, List<PagoItemDto> pagos, CancellationToken ct = default)
    {
        // 1. Obtener comanda con detalles
        var comanda = await _comandaRepository.GetWithDetailsAsync(comandaId, ct);

        if (comanda is null)
            return new ResultadoCobro { Exitoso = false, Mensaje = "Comanda no encontrada." };

        if (comanda.Estado != ComandaEstado.Abierta)
            return new ResultadoCobro { Exitoso = false, Mensaje = $"La comanda no está abierta. Estado actual: {comanda.Estado}." };

        if (pagos == null || pagos.Count == 0)
            return new ResultadoCobro { Exitoso = false, Mensaje = "No se especificaron pagos para registrar." };

        decimal totalPagado = pagos.Sum(p => p.Monto);
        if (totalPagado < comanda.Total)
            return new ResultadoCobro { Exitoso = false, Mensaje = $"El monto total pagado ({totalPagado:N2}) es menor al total de la comanda ({comanda.Total:N2})." };

        // 2. Crear los registros de pago
        var creados = new List<Pago>();
        foreach (var pagoItem in pagos)
        {
            var mp = await _metodoPagoRepository.GetByIdAsync(pagoItem.MetodoPagoId, ct);
            if (mp is null)
                return new ResultadoCobro { Exitoso = false, Mensaje = $"Método de pago no encontrado." };

            // Manejo especial para Cuenta Corriente
            if (mp.EsCuentaCorriente)
            {
                if (pagoItem.ClienteId is null)
                    return new ResultadoCobro { Exitoso = false, Mensaje = "Debe seleccionar un cliente para cargar el consumo a Cuenta Corriente." };

                var cliente = await _clienteRepository.GetWithCuentaCorrienteAsync(pagoItem.ClienteId.Value, ct);
                if (cliente?.CuentaCorriente is null)
                    return new ResultadoCobro { Exitoso = false, Mensaje = "El cliente seleccionado no tiene una cuenta corriente habilitada." };

                var nuevoSaldo = cliente.CuentaCorriente.SaldoActual + pagoItem.Monto;
                if (cliente.LimiteCredito > 0 && nuevoSaldo > cliente.LimiteCredito)
                    return new ResultadoCobro { Exitoso = false, Mensaje = $"El cliente '{cliente.Nombre} {cliente.Apellido}' superaría su límite de crédito ({cliente.LimiteCredito:N2})." };

                // Actualizar saldo
                cliente.CuentaCorriente.SaldoActual = nuevoSaldo;
                cliente.CuentaCorriente.UpdatedAt = DateTime.UtcNow;
                await _cuentaCorrienteRepository.UpdateAsync(cliente.CuentaCorriente, ct);

                // Crear movimiento de cargo
                await _movimientoCCRepository.AddAsync(new MovimientoCuentaCorriente
                {
                    CuentaCorrienteId = cliente.CuentaCorriente.Id,
                    ComandaId = comandaId,
                    Tipo = TipoMovimientoCuentaCorriente.Cargo,
                    Monto = pagoItem.Monto,
                    Detalle = $"Consumo comanda #{comandaId.ToString()[..8]}",
                    SyncEstado = SyncEstado.Pendiente
                }, ct);

                _logger.LogInformation("Cargo a Cuenta Corriente registrado para Cliente {ClienteId}. Monto: {Monto}. Nuevo saldo: {NuevoSaldo}", pagoItem.ClienteId, pagoItem.Monto, nuevoSaldo);
            }

            // Crear registro de pago (para todas las transacciones, incluidas las de cuenta corriente)
            var pago = new Pago
            {
                ComandaId = comandaId,
                TurnoCajaId = turnoCajaId,
                MetodoPagoId = pagoItem.MetodoPagoId,
                Monto = pagoItem.Monto,
                SyncEstado = SyncEstado.Pendiente
            };

            await _pagoRepository.AddAsync(pago, ct);
            creados.Add(pago);
            _logger.LogInformation("Pago registrado para Comanda {ComandaId}. Método: {MetodoPagoId}, Monto: {Monto}", comandaId, pagoItem.MetodoPagoId, pagoItem.Monto);
        }

        // 3. Verificar si algún método de pago requiere factura AFIP
        var resultado = new ResultadoCobro
        {
            Exitoso = true,
            PagoId = creados.FirstOrDefault()?.Id,
            Mensaje = "Cobro procesado correctamente."
        };

        bool requiereAfip = false;
        foreach (var pagoItem in pagos)
        {
            var mp = await _metodoPagoRepository.GetByIdAsync(pagoItem.MetodoPagoId, ct);
            if (mp != null && mp.RequiereFacturaAfip)
            {
                requiereAfip = true;
                break;
            }
        }

        if (requiereAfip)
        {
            resultado = await ProcesarFacturaAfipAsync(resultado, comanda, totalPagado, ct);
        }

        // 4. Si la comanda es una cuenta corriente abierta (exenta de turno), recién ahora —
        // al cerrarla — se le asigna la fecha contable y el turno/caja con el que se liquida.
        if (comanda.FechaContable is null || string.IsNullOrEmpty(comanda.Turno))
        {
            var turnoCajaCierre = await _turnoCajaRepository.GetByIdAsync(turnoCajaId, ct);
            if (turnoCajaCierre != null)
            {
                comanda.FechaContable = DateTime.SpecifyKind(turnoCajaCierre.FechaContable, DateTimeKind.Utc);
                comanda.Turno = turnoCajaCierre.Turno;
            }
        }

        // 5. Marcar la comanda como cobrada
        comanda.Estado = ComandaEstado.Cobrada;
        comanda.SyncEstado = SyncEstado.Pendiente;
        comanda.UpdatedAt = DateTime.UtcNow;
        await _comandaRepository.UpdateAsync(comanda, ct);

        // Liberar la mesa
        if (comanda.MesaId.HasValue)
        {
            var mesa = await _mesaRepository.GetByIdAsync(comanda.MesaId.Value, ct);
            if (mesa != null)
            {
                mesa.Ocupada = false;
                mesa.UpdatedAt = DateTime.UtcNow;
                await _mesaRepository.UpdateAsync(mesa, ct);
            }
        }

        _logger.LogInformation("Comanda {ComandaId} cobrada exitosamente. AFIP: {AfipEmitida}", comandaId, resultado.FacturaAfipEmitida);
        return resultado;
    }

    // ═══════════════════════════════════════════════════════
    // IMPRESIÓN CONFIGURABLE
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Envía un objeto Comanda directamente a las impresoras habilitadas de la sucursal.
    /// </summary>
    private async Task<List<ResultadoImpresion>> EnviarTicketComandaObjetoAsync(Comanda comanda, CancellationToken ct)
    {
        try
        {
            // Determinar la sucursal (desde la mesa o desde el usuario)
            var sucursalId = comanda.Mesa?.SucursalId ?? comanda.Usuario?.SucursalId;
            if (sucursalId is null)
            {
                _logger.LogWarning("No se pudo determinar la sucursal para la comanda {ComandaId}.", comanda.Id);
                return [new ResultadoImpresion
                {
                    Exitoso = false,
                    Mensaje = "No se pudo determinar la sucursal para la impresión."
                }];
            }

            _logger.LogInformation(
                "Enviando ticket de comanda para Comanda {ComandaId}. Ítems a imprimir: {ItemCount}",
                comanda.Id, comanda.Items?.Count ?? 0);

            var resultados = await _impresoraService.ImprimirTicketAsync(
                sucursalId.Value, "Comanda", comanda, ct: ct);

            foreach (var resultado in resultados)
            {
                if (resultado.Exitoso)
                {
                    _logger.LogInformation(
                        "✓ Ticket de comanda impreso exitosamente para Comanda {ComandaId}. Impresora: {Impresora}",
                        comanda.Id, resultado.ImpresoraUtilizada);
                }
                else
                {
                    _logger.LogWarning(
                        "✗ Fallo al imprimir ticket de comanda para Comanda {ComandaId}: {Mensaje}",
                        comanda.Id, resultado.Mensaje);
                }
            }

            return resultados;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico al enviar ticket de comanda para Comanda {ComandaId}", comanda.Id);
            return [new ResultadoImpresion
            {
                Exitoso = false,
                Mensaje = $"Error de impresión: {ex.Message}"
            }];
        }
    }

    /// <summary>
    /// Carga la comanda con detalles y envía el ticket de tipo "Comanda"
    /// a todas las impresoras configuradas de la sucursal.
    /// </summary>
    private async Task<List<ResultadoImpresion>> EnviarTicketComandaAsync(Guid comandaId, CancellationToken ct)
    {
        var comanda = await _comandaRepository.GetWithDetailsAsync(comandaId, ct);
        if (comanda is null)
        {
            _logger.LogWarning("No se pudo cargar la comanda {ComandaId} para impresión.", comandaId);
            return [new ResultadoImpresion
            {
                Exitoso = false,
                Mensaje = $"Comanda '{comandaId}' no encontrada para impresión."
            }];
        }

        return await EnviarTicketComandaObjetoAsync(comanda, ct);
    }

    // ═══════════════════════════════════════════════════════
    // FACTURACIÓN ELECTRÓNICA — ARCA WSFEv1
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Emite el comprobante electrónico de la venta contra WSFEv1 y, si ARCA lo autorizó,
    /// lo manda a imprimir.
    ///
    /// El cobro nunca se revierte por un problema de facturación: si ARCA no responde, el
    /// comprobante queda pendiente de reintento y la venta sigue registrada.
    /// </summary>
    private async Task<ResultadoCobro> ProcesarFacturaAfipAsync(
        ResultadoCobro resultado, Comanda comanda, decimal montoTotal, CancellationToken ct)
    {
        var sucursalId = comanda.Mesa?.SucursalId ?? comanda.Usuario?.SucursalId;
        if (sucursalId is null)
        {
            _logger.LogWarning("No se pudo determinar la sucursal para facturar la comanda {ComandaId}.", comanda.Id);
            resultado.Mensaje = "Cobro registrado, pero no se pudo determinar la sucursal para facturar.";
            return resultado;
        }

        Comprobante comprobante;
        try
        {
            comprobante = await _facturacionElectronicaService.EmitirDesdeComandaAsync(comanda, sucursalId.Value, ct);
        }
        catch (InvalidOperationException ex)
        {
            // Falta configuración fiscal: no es algo que se resuelva reintentando.
            _logger.LogError(ex, "No se pudo facturar la comanda {ComandaId}.", comanda.Id);
            resultado.FacturaAfipEmitida = false;
            resultado.Mensaje = $"Cobro registrado sin factura. {ex.Message}";
            return resultado;
        }

        resultado.ComprobanteNumero = $"{comprobante.PuntoVenta:D4}-{comprobante.NumeroComprobante:D8}";

        if (comprobante.Estado != EstadoComprobante.Emitido)
        {
            resultado.FacturaAfipEmitida = false;
            resultado.Mensaje = comprobante.Estado == EstadoComprobante.Pendiente
                ? $"Cobro registrado. La factura quedó pendiente de autorización: {comprobante.UltimoError}"
                : $"Cobro registrado. ARCA rechazó la factura: {comprobante.UltimoError}";
            return resultado;
        }

        resultado.FacturaAfipEmitida = true;
        resultado.CaeNumero = comprobante.Cae;
        resultado.CaeVencimiento = comprobante.CaeVencimiento?.ToString("yyyyMMdd");
        resultado.Mensaje = "Cobro procesado y factura electrónica autorizada por ARCA.";

        var datosExtra = new Dictionary<string, string>
        {
            { "CAE", comprobante.Cae ?? string.Empty },
            { "CAE_VTO", resultado.CaeVencimiento ?? string.Empty },
            { "COMPROBANTE_NRO", resultado.ComprobanteNumero },
            { "METODO_PAGO", "—" },
            { "MONTO_PAGADO", montoTotal.ToString("N2") }
        };

        var resultadosImpresion = await _impresoraService.ImprimirTicketAsync(
            sucursalId.Value, comprobante.TipoComprobante.ToString(), comanda, datosExtra, ct);

        var resultadoImpresion = resultadosImpresion.FirstOrDefault(r => r.TicketContenido != null);
        resultado.OrdenImpresionUsb = resultadoImpresion?.TicketContenido;
        resultado.Simulado = resultadoImpresion?.Simulado ?? false;

        _logger.LogInformation(
            "Comprobante {Numero} de la comanda {ComandaId} enviado a {Count} impresora(s).",
            resultado.ComprobanteNumero, comanda.Id, resultadosImpresion.Count(r => r.Exitoso));

        return resultado;
    }

    public async Task<Comanda> UpdateComandaAsync(Guid id, Comanda updatedEntity, CancellationToken ct = default)
    {
        var existing = await _comandaRepository.GetWithDetailsAsync(id, ct);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Comanda '{id}' no encontrada.");
        }

        if (existing.Estado != ComandaEstado.Abierta)
        {
            throw new InvalidOperationException($"La comanda no está abierta. Estado actual: {existing.Estado}.");
        }

        // La mesa/comanda queda "tomada" por el mozo que la abrió: sólo ese mozo, o un
        // usuario con perfil de gerente/administrador, puede modificarla. Esto evita que
        // un mozo distinto edite (y de paso se apropie de) la orden de otro compañero.
        if (existing.UsuarioId != Guid.Empty && updatedEntity.UsuarioId != Guid.Empty && updatedEntity.UsuarioId != existing.UsuarioId)
        {
            var autorizado = await EsGerenteOAdministradorAsync(updatedEntity.UsuarioId, ct);
            if (!autorizado)
                throw new UnauthorizedAccessException("Esta mesa/comanda está tomada por otro mozo. Se requiere un gerente o administrador para modificarla.");
        }

        // 1. Calcular delta de ítems antes de sobrescribir en BD
        var deltaItems = new List<ComandaItem>();
        var sucursalIdStock = existing.Mesa?.SucursalId ?? existing.Usuario?.SucursalId;

        foreach (var newItem in updatedEntity.Items)
        {
            // Ignora ítems ya anulados como base de comparación: siguen físicamente en
            // existing.Items (preservados para no romper la auditoría de anulación),
            // pero no deben confundirse con la cantidad "actual" del producto.
            var existingItem = existing.Items.Where(i => !i.Cancelado).FirstOrDefault(i => i.ProductoId == newItem.ProductoId);
            var oldQty = existingItem?.Cantidad ?? 0;
            var deltaQty = newItem.Cantidad - oldQty;

            if (deltaQty > 0)
            {
                var producto = existingItem?.Producto ?? await _productoRepository.GetByIdAsync(newItem.ProductoId, ct);
                if (producto == null)
                {
                    _logger.LogWarning("Producto con ID {ProductoId} no encontrado en catálogo al calcular delta (incremento) de comanda {ComandaId}.", newItem.ProductoId, id);
                    continue;
                }
                deltaItems.Add(new ComandaItem
                {
                    ProductoId = newItem.ProductoId,
                    Producto = producto,
                    Cantidad = deltaQty,
                    PrecioUnitario = newItem.PrecioUnitario,
                    Notas = newItem.Notas,
                    EstadoPreparacion = newItem.EstadoPreparacion
                });

                // Ítems recién comandados: descontar stock por la cantidad agregada.
                if (sucursalIdStock.HasValue)
                    await AjustarStockPorProductoAsync(newItem.ProductoId, deltaQty, sucursalIdStock.Value, restituir: false, ct);
            }
            else if (deltaQty < 0)
            {
                var producto = existingItem?.Producto ?? await _productoRepository.GetByIdAsync(newItem.ProductoId, ct);
                if (producto == null)
                {
                    _logger.LogWarning("Producto con ID {ProductoId} no encontrado en catálogo al calcular delta (reducción) de comanda {ComandaId}.", newItem.ProductoId, id);
                    continue;
                }
                deltaItems.Add(new ComandaItem
                {
                    ProductoId = newItem.ProductoId,
                    Producto = new Producto
                    {
                        Id = producto.Id,
                        Nombre = $"QUITAR {producto.Nombre}",
                        RequiereCocina = producto.RequiereCocina,
                        CategoriaId = producto.CategoriaId,
                        ColorUi = producto.ColorUi
                    },
                    Cantidad = Math.Abs(deltaQty),
                    PrecioUnitario = newItem.PrecioUnitario,
                    Notas = newItem.Notas,
                    EstadoPreparacion = newItem.EstadoPreparacion
                });

                // Cantidad reducida: restituir el stock correspondiente.
                if (sucursalIdStock.HasValue)
                    await AjustarStockPorProductoAsync(newItem.ProductoId, Math.Abs(deltaQty), sucursalIdStock.Value, restituir: true, ct);
            }
        }

        // Verificar ítems eliminados por completo (los ya anulados se excluyen: esa
        // baja ya se comunicó a través del flujo de anulación, no debe generar un
        // delta "QUITAR" duplicado en el ticket de cocina).
        foreach (var oldItem in existing.Items.Where(i => !i.Cancelado))
        {
            var stillExists = updatedEntity.Items.Any(i => i.ProductoId == oldItem.ProductoId);
            if (!stillExists)
            {
                var producto = oldItem.Producto;
                if (producto == null)
                {
                    _logger.LogWarning("Producto del item eliminado de la comanda {ComandaId} es nulo y no pudo procesarse el delta.", id);
                    continue;
                }
                deltaItems.Add(new ComandaItem
                {
                    ProductoId = oldItem.ProductoId,
                    Producto = new Producto
                    {
                        Id = producto.Id,
                        Nombre = $"QUITAR {producto.Nombre}",
                        RequiereCocina = producto.RequiereCocina,
                        CategoriaId = producto.CategoriaId,
                        ColorUi = producto.ColorUi
                    },
                    Cantidad = oldItem.Cantidad,
                    PrecioUnitario = oldItem.PrecioUnitario,
                    Notas = oldItem.Notas,
                    EstadoPreparacion = oldItem.EstadoPreparacion
                });

                // Ítem eliminado por completo: restituir todo su stock.
                if (sucursalIdStock.HasValue)
                    await AjustarStockPorProductoAsync(oldItem.ProductoId, oldItem.Cantidad, sucursalIdStock.Value, restituir: true, ct);
            }
        }

        // 2. Actualizar cabecera y persistir
        existing.Subtotal = updatedEntity.Subtotal;
        existing.Descuento = updatedEntity.Descuento;
        existing.Total = updatedEntity.Total;
        existing.SyncEstado = SyncEstado.Pendiente;

        var newItems = updatedEntity.Items.ToList();
        await _comandaRepository.UpdateComandaWithItemsAsync(existing, newItems, ct);

        // 3. Imprimir solo si hay deltas (modificaciones)
        if (deltaItems.Any())
        {
            var deltaComanda = new Comanda
            {
                Id = existing.Id,
                MesaId = existing.MesaId,
                Mesa = existing.Mesa,
                UsuarioId = existing.UsuarioId,
                // La navegación puede venir sin cargar; el ticket solo necesita el
                // nombre del mozo cuando está disponible.
                Usuario = existing.Usuario!,
                TipoVentaId = existing.TipoVentaId,
                TipoVenta = existing.TipoVenta,
                Subtotal = existing.Subtotal,
                Descuento = existing.Descuento,
                Total = existing.Total,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                Items = deltaItems
            };

            var resultadosImpresion = await EnviarTicketComandaObjetoAsync(deltaComanda, ct);
            existing.UltimaImpresion = ConsolidarResultadoImpresion(resultadosImpresion);
        }

        return existing;
    }

    public async Task<IEnumerable<Comanda>> GetAllWithDetailsAsync(CancellationToken ct = default)
        => await _comandaRepository.GetAllWithDetailsAsync(ct);

    public async Task<ResultadoImpresion> ImprimirTicketNoFiscalAsync(Guid comandaId, CancellationToken ct = default)
    {
        var comanda = await _comandaRepository.GetWithDetailsAsync(comandaId, ct);
        if (comanda is null)
        {
            _logger.LogWarning("No se pudo cargar la comanda {ComandaId} para imprimir ticket no fiscal.", comandaId);
            return new ResultadoImpresion
            {
                Exitoso = false,
                Mensaje = $"Comanda '{comandaId}' no encontrada."
            };
        }

        var sucursalId = comanda.Mesa?.SucursalId ?? comanda.Usuario?.SucursalId;
        if (sucursalId is null)
        {
            _logger.LogWarning("No se pudo determinar la sucursal para la comanda {ComandaId}.", comandaId);
            return new ResultadoImpresion
            {
                Exitoso = false,
                Mensaje = "No se pudo determinar la sucursal para la impresión."
            };
        }

        var datosExtra = new Dictionary<string, string>
        {
            { "CAE", "DOCUMENTO NO VALIDO COMO FACTURA" },
            { "CAE_VTO", "—" },
            { "COMPROBANTE_NRO", "*** TICKET X (NO FISCAL) ***" },
            { "METODO_PAGO", "—" },
            { "MONTO_PAGADO", comanda.Total.ToString("N2") }
        };

        var resultados = await _impresoraService.ImprimirTicketAsync(
            sucursalId.Value, "FacturaB", comanda, datosExtra, ct);

        if (resultados.Count == 0)
            return new ResultadoImpresion { Exitoso = true, Mensaje = "No hay impresoras configuradas para FacturaB." };

        var exitosos = resultados.Count(r => r.Exitoso);
        var fallidos = resultados.Count - exitosos;

        return new ResultadoImpresion
        {
            Exitoso = fallidos == 0,
            Mensaje = $"Impresión no fiscal: {exitosos} exitosa(s), {fallidos} fallida(s).",
            ImpresoraUtilizada = string.Join(", ", resultados.Where(r => r.ImpresoraUtilizada != null).Select(r => r.ImpresoraUtilizada)),
            TicketContenido = resultados.FirstOrDefault(r => r.TicketContenido != null)?.TicketContenido,
            Simulado = resultados.Any(r => r.Simulado)
        };
    }

    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var comanda = await _repository.GetByIdAsync(id, ct);
        if (comanda != null && comanda.MesaId.HasValue)
        {
            var mesa = await _mesaRepository.GetByIdAsync(comanda.MesaId.Value, ct);
            if (mesa != null)
            {
                mesa.Ocupada = false;
                mesa.UpdatedAt = DateTime.UtcNow;
                await _mesaRepository.UpdateAsync(mesa, ct);
            }
        }
        await base.DeleteAsync(id, ct);
    }

    /// <summary>
    /// Indica si el usuario tiene perfil de Gerente/Administrador (o permiso explícito
    /// "gerente.override"), habilitado para tomar/editar la mesa de otro mozo.
    /// </summary>
    private async Task<bool> EsGerenteOAdministradorAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId, ct);
        if (usuario is null)
            return false;

        var rol = await _rolRepository.GetByIdAsync(usuario.RolId, ct);
        if (rol is null)
            return false;

        return string.Equals(rol.Nombre, "gerente", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rol.Nombre, "administrador", StringComparison.OrdinalIgnoreCase)
            || rol.Permisos.Contains("gerente.override");
    }

    /// <summary>
    /// Valida que el usuario exista y tenga permiso de anulación
    /// (rol Cajero/Administrador, o permiso explícito "pos.anular").
    /// </summary>
    private async Task ValidarPermisoAnulacionAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId, ct);
        if (usuario is null)
            throw new KeyNotFoundException($"Usuario '{usuarioId}' no encontrado.");

        var rol = await _rolRepository.GetByIdAsync(usuario.RolId, ct);
        if (rol is null)
            throw new InvalidOperationException("El usuario no tiene un rol asignado.");

        var tienePermiso =
            string.Equals(rol.Nombre, "cajero", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rol.Nombre, "administrador", StringComparison.OrdinalIgnoreCase) ||
            rol.Permisos.Contains("pos.anular");

        if (!tienePermiso)
            throw new UnauthorizedAccessException("El usuario no tiene permiso para anular comandas o ítems.");
    }

    public async Task AnularComandaAsync(Guid comandaId, Guid usuarioId, string motivo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo de anulación es obligatorio.");

        await ValidarPermisoAnulacionAsync(usuarioId, ct);

        var comanda = await _comandaRepository.GetWithDetailsAsync(comandaId, ct);
        if (comanda is null)
            throw new KeyNotFoundException($"Comanda '{comandaId}' no encontrada.");

        if (comanda.Estado == ComandaEstado.Cobrada)
            throw new InvalidOperationException("No se puede anular una comanda que ya fue cobrada.");

        // Marca como anulados todos los ítems que aún no lo estaban (los que ya
        // habían sido anulados individualmente conservan su propio motivo/fecha).
        // Comanda.Total NO se toca: queda como el monto vigente al momento de esta
        // anulación final, que es justamente el valor que representa la transacción
        // anulada para el reporte correspondiente.
        var ahora = DateTime.UtcNow;
        foreach (var item in comanda.Items.Where(i => !i.Cancelado))
        {
            item.Cancelado = true;
            item.MotivoAnulacion = motivo.Trim();
            item.AnuladoPorUsuarioId = usuarioId;
            item.FechaAnulacion = ahora;
            item.UpdatedAt = ahora;
            await _comandaItemRepository.UpdateAsync(item, ct);
        }

        comanda.Estado = ComandaEstado.Anulada;
        comanda.SyncEstado = SyncEstado.Pendiente;
        comanda.UpdatedAt = ahora;
        await _comandaRepository.UpdateAsync(comanda, ct);

        // Liberar la mesa
        if (comanda.MesaId.HasValue)
        {
            var mesa = await _mesaRepository.GetByIdAsync(comanda.MesaId.Value, ct);
            if (mesa != null)
            {
                mesa.Ocupada = false;
                mesa.UpdatedAt = ahora;
                await _mesaRepository.UpdateAsync(mesa, ct);
            }
        }
    }

    public async Task AnularItemComandaAsync(Guid comandaId, Guid comandaItemId, Guid usuarioId, string motivo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo de anulación es obligatorio.");

        await ValidarPermisoAnulacionAsync(usuarioId, ct);

        var comanda = await _comandaRepository.GetWithDetailsAsync(comandaId, ct);
        if (comanda is null)
            throw new KeyNotFoundException($"Comanda '{comandaId}' no encontrada.");

        if (comanda.Estado != ComandaEstado.Abierta)
            throw new InvalidOperationException("No se puede anular un ítem de una comanda que no está abierta.");

        var item = comanda.Items.FirstOrDefault(i => i.Id == comandaItemId);
        if (item is null)
            throw new KeyNotFoundException($"Ítem '{comandaItemId}' no encontrado en la comanda.");

        if (item.Cancelado)
            throw new InvalidOperationException("El ítem ya fue anulado.");

        item.Cancelado = true;
        item.MotivoAnulacion = motivo.Trim();
        item.AnuladoPorUsuarioId = usuarioId;
        item.FechaAnulacion = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        await _comandaItemRepository.UpdateAsync(item, ct);

        comanda.Subtotal = comanda.Items.Where(i => !i.Cancelado).Sum(i => i.Cantidad * i.PrecioUnitario);
        comanda.Total = comanda.Subtotal - comanda.Descuento;
        comanda.SyncEstado = SyncEstado.Pendiente;
        comanda.UpdatedAt = DateTime.UtcNow;
        await _comandaRepository.UpdateAsync(comanda, ct);
    }

    /// <summary>
    /// Construye un objeto <see cref="FiscalFacturaRequest"/> mapeando la comanda,
    /// incluyendo la alícuota de IVA individual configurada para cada producto en el catálogo.
    /// </summary>
    public static BaresFamilia.Fiscal.Engine.FiscalFacturaRequest CrearSolicitudFiscalDesdeComanda(
        Comanda comanda, int tipoComprobante, FiscalClienteDto? clienteOverride = null)
    {
        var request = new BaresFamilia.Fiscal.Engine.FiscalFacturaRequest
        {
            TipoComprobante = tipoComprobante,
            Cliente = clienteOverride ?? new BaresFamilia.Fiscal.Engine.FiscalClienteDto
            {
                Nombre = "Consumidor Final",
                CondicionIva = BaresFamilia.Fiscal.Engine.FiscalConstants.COND_CONSUMIDOR_FINAL
            },
            DescuentoGeneralMonto = comanda.Descuento
        };

        if (comanda.Items != null)
        {
            foreach (var item in comanda.Items)
            {
                // Extraer el código entero de la alícuota de IVA del producto (5=21%, 4=10.5%, 6=27%, 1=Exento, 0=NoGravado)
                int alicuotaCode = item.Producto != null
                    ? (int)item.Producto.AlicuotaIva
                    : BaresFamilia.Fiscal.Engine.FiscalConstants.IVA_21;

                request.Items.Add(new BaresFamilia.Fiscal.Engine.FiscalItemDto
                {
                    Descripcion = item.Producto?.Nombre ?? "Producto",
                    Cantidad = item.Cantidad,
                    PrecioUnitarioConIva = item.PrecioUnitario,
                    AlicuotaIvaCode = alicuotaCode
                });
            }
        }

        if (comanda.Pagos != null)
        {
            foreach (var pago in comanda.Pagos)
            {
                request.Pagos.Add(new BaresFamilia.Fiscal.Engine.FiscalPagoDto
                {
                    FormaPagoCode = BaresFamilia.Fiscal.Engine.FiscalConstants.PAGO_EFECTIVO,
                    Monto = pago.Monto,
                    Descripcion = pago.MetodoPago?.Nombre ?? "Efectivo"
                });
            }
        }

        return request;
    }
}
