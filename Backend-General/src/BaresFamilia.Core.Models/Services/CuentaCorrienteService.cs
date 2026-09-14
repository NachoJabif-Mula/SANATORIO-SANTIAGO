using BaresFamilia.Core.Models.Contratos.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;
using Microsoft.Extensions.Logging;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Servicio de negocio de cuentas corrientes de clientes.
/// </summary>
public class CuentaCorrienteService : GenericService<CuentaCorriente>, ICuentaCorrienteService
{
    private const string MetodoPagoEfectivo = "Efectivo";

    private readonly ICuentaCorrienteRepository _cuentaCorrienteRepository;
    private readonly IRepository<MovimientoCuentaCorriente> _movimientoRepository;
    private readonly IMetodoPagoRepository _metodoPagoRepository;
    private readonly ITurnoCajaRepository _turnoCajaRepository;
    private readonly IMovimientoCajaRepository _movimientoCajaRepository;
    private readonly ILogger<CuentaCorrienteService> _logger;

    public CuentaCorrienteService(
        ICuentaCorrienteRepository cuentaCorrienteRepository,
        IRepository<MovimientoCuentaCorriente> movimientoRepository,
        IMetodoPagoRepository metodoPagoRepository,
        ITurnoCajaRepository turnoCajaRepository,
        IMovimientoCajaRepository movimientoCajaRepository,
        ILogger<CuentaCorrienteService> logger)
        : base(cuentaCorrienteRepository)
    {
        _cuentaCorrienteRepository = cuentaCorrienteRepository;
        _movimientoRepository = movimientoRepository;
        _metodoPagoRepository = metodoPagoRepository;
        _turnoCajaRepository = turnoCajaRepository;
        _movimientoCajaRepository = movimientoCajaRepository;
        _logger = logger;
    }

    public async Task<ResumenCuentaCorriente> GetResumenPorClienteAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cuenta = await _cuentaCorrienteRepository.GetPorClienteAsync(clienteId, ct)
            ?? throw new RecursoNoEncontradoException("El cliente no tiene cuenta corriente.");

        var movimientos = (await _cuentaCorrienteRepository.GetMovimientosAsync(cuenta.Id, ct)).ToList();

        _logger.LogInformation(
            "Historial de movimientos obtenido para cliente {ClienteId}. Total movimientos: {Count}",
            clienteId, movimientos.Count);

        return new ResumenCuentaCorriente(cuenta.SaldoActual, movimientos);
    }

    public async Task<ResultadoAbono> AbonarAsync(
        Guid clienteId,
        Guid turnoCajaId,
        Guid metodoPagoId,
        decimal monto,
        CancellationToken ct = default)
    {
        if (monto <= 0)
            throw new ReglaNegocioException("El monto a abonar debe ser mayor a cero.");

        var cuenta = await _cuentaCorrienteRepository.GetPorClienteConClienteAsync(clienteId, ct)
            ?? throw new RecursoNoEncontradoException("El cliente no tiene cuenta corriente.");

        if (monto > cuenta.SaldoActual)
            throw new ReglaNegocioException($"El monto a abonar ({monto:N2}) supera el saldo adeudado ({cuenta.SaldoActual:N2}).");

        var metodoPago = await _metodoPagoRepository.GetByIdAsync(metodoPagoId, ct)
            ?? throw new ReglaNegocioException("Método de pago no encontrado.");

        // Cancelar una deuda generando otra deuda dejaría el saldo igual y duplicaría el movimiento.
        if (metodoPago.EsCuentaCorriente)
            throw new ReglaNegocioException("No se puede liquidar una cuenta corriente usando 'Cuenta Corriente' como método de pago.");

        if (await _turnoCajaRepository.GetAbiertoPorIdAsync(turnoCajaId, ct) is null)
            throw new ReglaNegocioException("No hay un turno de caja activo válido.");

        var nombreCliente = $"{cuenta.Cliente.Nombre} {cuenta.Cliente.Apellido}";
        var saldoAnterior = cuenta.SaldoActual;

        await _movimientoRepository.AddAsync(new MovimientoCuentaCorriente
        {
            CuentaCorrienteId = cuenta.Id,
            ComandaId = null,
            Tipo = TipoMovimientoCuentaCorriente.Pago,
            Monto = monto,
            Detalle = $"Abono a cuenta corriente vía {metodoPago.Nombre} — {nombreCliente}",
            SyncEstado = SyncEstado.Pendiente
        }, ct);

        cuenta.SaldoActual -= monto;
        await _cuentaCorrienteRepository.UpdateAsync(cuenta, ct);

        // Solo el efectivo entra físicamente a la caja: los demás métodos se
        // concilian por fuera y no deben alterar el arqueo del turno.
        if (metodoPago.Nombre.Equals(MetodoPagoEfectivo, StringComparison.OrdinalIgnoreCase))
        {
            await _movimientoCajaRepository.AddAsync(new MovimientoCaja
            {
                TurnoCajaId = turnoCajaId,
                Tipo = TipoMovimientoCaja.Ingreso,
                Monto = monto,
                Concepto = $"Cobro Cta. Cte. — {nombreCliente}",
                SyncEstado = SyncEstado.Pendiente
            }, ct);
        }

        _logger.LogInformation(
            "Abono de Cta. Cte. registrado. Cliente {ClienteId}. Monto {Monto}. Nuevo saldo {Saldo}",
            clienteId, monto, cuenta.SaldoActual);

        return new ResultadoAbono(saldoAnterior, cuenta.SaldoActual, monto);
    }
}
