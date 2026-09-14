using BaresFamilia.Core.Models.Contratos.Sincronizacion;
using BaresFamilia.Core.Models.Dtos.Sincronizacion;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Core.Models.Exceptions;
using BaresFamilia.Core.Models.Interfaces;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Ingesta del lote de sincronización que envían las sucursales.
///
/// La premisa es que la sucursal es la fuente de verdad y nunca debe quedar
/// bloqueada: si un dato referenciado todavía no llegó a la Nube, el registro se
/// saltea o se enlaza de forma degradada y se reintenta en el próximo ciclo, en
/// lugar de abortar el lote completo.
///
/// El orden de los pasos respeta las dependencias de clave foránea:
/// cajas → turnos → comandas → pagos → movimientos → cierres → clientes → cuenta corriente.
/// </summary>
public class SincronizacionNubeService : ISincronizacionNubeService
{
    private const int IntervaloMinimoSegundos = 5;
    private const int IntervaloMaximoSegundos = 300;

    private const int MaximoLargoNombreSucursal = 200;
    private const int MaximoLargoTipo = 50;
    private const int MaximoLargoMensaje = 1000;

    private readonly ISincronizacionNubeRepository _repositorio;
    private readonly IMonitorSincronizacion _monitor;

    public SincronizacionNubeService(ISincronizacionNubeRepository repositorio, IMonitorSincronizacion monitor)
    {
        _repositorio = repositorio;
        _monitor = monitor;
    }

    // ═══════════════════════════════════════════════════════
    // INGESTA DEL LOTE
    // ═══════════════════════════════════════════════════════

    public async Task RecibirPayloadAsync(SyncPayloadDto payload, Guid? sucursalIdDelToken, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var sucursalId = await ResolverSucursalAsync(payload, sucursalIdDelToken, ct);
        await RegistrarActividadDePushAsync(payload, sucursalId, ct);

        await ProcesarCajasAsync(payload.Cajas, sucursalId, ct);
        await ProcesarTurnosCajaAsync(payload.TurnosCaja, ct);
        await AsegurarTurnosReferenciadosAsync(payload, sucursalId, ct);
        await ProcesarComandasAsync(payload.Comandas, sucursalId, ct);
        await ProcesarPagosAsync(payload.Pagos, ct);
        await ProcesarMovimientosCajaAsync(payload.Movimientos, ct);
        await ProcesarCierresDiariosAsync(payload.CierresDiarios, ct);
        await ProcesarClientesAsync(payload.Clientes, ct);
        await ProcesarMovimientosCuentaCorrienteAsync(payload.MovimientosCuentaCorriente, ct);
    }

    /// <summary>
    /// La sucursal sale del token M2M. Si el token no la declara (instalaciones
    /// viejas), se deduce del usuario de la primera comanda y, como último recurso,
    /// se usa la primera sucursal activa.
    /// </summary>
    private async Task<Guid> ResolverSucursalAsync(SyncPayloadDto payload, Guid? sucursalIdDelToken, CancellationToken ct)
    {
        if (sucursalIdDelToken is { } desdeToken && desdeToken != Guid.Empty)
            return desdeToken;

        var primeraComanda = payload.Comandas.FirstOrDefault();
        if (primeraComanda is not null
            && await _repositorio.GetSucursalDeUsuarioAsync(primeraComanda.UsuarioId, ct) is { } desdeUsuario)
        {
            return desdeUsuario;
        }

        var primeraSucursal = await _repositorio.GetPrimeraSucursalActivaAsync(ct);
        return primeraSucursal?.Id
            ?? throw new ReglaNegocioException("No se pudo determinar la sucursal para la sincronización.");
    }

    private async Task RegistrarActividadDePushAsync(SyncPayloadDto payload, Guid sucursalId, CancellationToken ct)
    {
        var nombre = await _repositorio.GetNombreSucursalAsync(sucursalId, ct) ?? "Sucursal";

        if (payload.Comandas.Count > 0) _monitor.RegistrarPush(sucursalId, nombre, TipoPush.Comandas);
        if (payload.Pagos.Count > 0) _monitor.RegistrarPush(sucursalId, nombre, TipoPush.Pagos);
        if (payload.Movimientos.Count > 0) _monitor.RegistrarPush(sucursalId, nombre, TipoPush.Movimientos);
        if (payload.CierresDiarios.Count > 0) _monitor.RegistrarPush(sucursalId, nombre, TipoPush.CierresDiarios);
    }

    /// <summary>
    /// Las cajas reales de la sucursal se procesan primero para que los turnos
    /// enlacen contra ellas en vez de contra una caja fabricada por la Nube.
    /// </summary>
    private async Task ProcesarCajasAsync(List<SyncCajaDto> cajas, Guid sucursalId, CancellationToken ct)
    {
        if (cajas.Count == 0)
            return;

        var existentes = await _repositorio.GetCajasPorIdAsync(cajas.Select(c => c.Id).ToList(), ct);
        var nuevas = new List<Caja>();

        foreach (var sincronizada in cajas)
        {
            Enum.TryParse<TipoCaja>(sincronizada.TipoCaja, true, out var tipoCaja);

            if (existentes.TryGetValue(sincronizada.Id, out var caja))
            {
                caja.Nombre = sincronizada.Nombre;
                caja.TipoCaja = tipoCaja;
                caja.SyncEstado = SyncEstado.Sincronizado;
                caja.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                nuevas.Add(new Caja
                {
                    Id = sincronizada.Id,
                    SucursalId = sucursalId,
                    Nombre = sincronizada.Nombre,
                    TipoCaja = tipoCaja,
                    SyncEstado = SyncEstado.Sincronizado
                });
            }
        }

        await _repositorio.AgregarAsync(nuevas, ct);
        await _repositorio.GuardarCambiosAsync(ct);
    }

    private async Task ProcesarTurnosCajaAsync(List<SyncTurnoCajaDto> turnos, CancellationToken ct)
    {
        if (turnos.Count == 0)
            return;

        var cajasExistentes = await _repositorio.FiltrarIdsExistentesAsync<Caja>(
            turnos.Select(t => t.CajaId).Distinct().ToList(), ct);
        var usuariosExistentes = await _repositorio.FiltrarIdsExistentesAsync<Usuario>(
            turnos.Select(t => t.UsuarioId).Distinct().ToList(), ct);

        var existentes = await _repositorio.GetTurnosCajaPorIdAsync(turnos.Select(t => t.Id).ToList(), ct);
        var nuevos = new List<TurnoCaja>();

        foreach (var sincronizado in turnos)
        {
            // Si la caja o el usuario todavía no llegaron, el turno se reintenta en
            // el próximo ciclo en lugar de insertarse con una FK inválida.
            if (!cajasExistentes.Contains(sincronizado.CajaId) || !usuariosExistentes.Contains(sincronizado.UsuarioId))
                continue;

            if (existentes.TryGetValue(sincronizado.Id, out var turno))
            {
                // CajaId y UsuarioId se reasignan a propósito: los turnos creados por
                // el paso de integridad quedaron atados a entidades fabricadas y hay
                // que realinearlos a las reales una vez que llegaron.
                turno.CajaId = sincronizado.CajaId;
                turno.UsuarioId = sincronizado.UsuarioId;
                AplicarDatosDeTurno(turno, sincronizado);
                turno.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var nuevo = new TurnoCaja
                {
                    Id = sincronizado.Id,
                    CajaId = sincronizado.CajaId,
                    UsuarioId = sincronizado.UsuarioId
                };
                AplicarDatosDeTurno(nuevo, sincronizado);
                nuevos.Add(nuevo);
            }
        }

        await _repositorio.AgregarAsync(nuevos, ct);
        await _repositorio.GuardarCambiosAsync(ct);
    }

    private static void AplicarDatosDeTurno(TurnoCaja turno, SyncTurnoCajaDto sincronizado)
    {
        turno.FechaApertura = ComoUtc(sincronizado.FechaApertura);
        turno.FechaCierre = sincronizado.FechaCierre.HasValue ? ComoUtc(sincronizado.FechaCierre.Value) : null;
        turno.FechaContable = ComoUtc(sincronizado.FechaContable);
        turno.Turno = sincronizado.Turno;
        turno.FondoInicial = sincronizado.FondoInicial;
        turno.DiferenciaArqueo = sincronizado.DiferenciaArqueo;
        turno.SyncEstado = SyncEstado.Sincronizado;
    }

    /// <summary>
    /// Red de contención para pagos y movimientos que referencian un turno que la
    /// sucursal todavía no reenvió (típicamente instalaciones activadas hace tiempo).
    /// Se fabrica un turno mínimo para no perder el movimiento de dinero; el turno
    /// real lo sobrescribe apenas llega.
    /// </summary>
    private async Task AsegurarTurnosReferenciadosAsync(SyncPayloadDto payload, Guid sucursalId, CancellationToken ct)
    {
        var turnosRequeridos = payload.Pagos.Select(p => p.TurnoCajaId)
            .Concat(payload.Movimientos.Select(m => m.TurnoCajaId))
            .Distinct()
            .ToList();

        if (turnosRequeridos.Count == 0)
            return;

        var existentes = await _repositorio.FiltrarIdsExistentesAsync<TurnoCaja>(turnosRequeridos, ct);
        var faltantes = turnosRequeridos.Where(id => !existentes.Contains(id)).ToList();
        if (faltantes.Count == 0)
            return;

        var caja = await ObtenerOCrearCajaDeRespaldoAsync(sucursalId, ct);
        var usuario = await ObtenerOCrearUsuarioDeRespaldoAsync(sucursalId, ct);

        await _repositorio.AgregarAsync(faltantes.Select(id => new TurnoCaja
        {
            Id = id,
            CajaId = caja.Id,
            UsuarioId = usuario.Id,
            FechaApertura = DateTime.UtcNow,
            FondoInicial = 0,
            SyncEstado = SyncEstado.Sincronizado
        }), ct);

        await _repositorio.GuardarCambiosAsync(ct);
    }

    private async Task<Caja> ObtenerOCrearCajaDeRespaldoAsync(Guid sucursalId, CancellationToken ct)
    {
        var caja = await _repositorio.GetCajaActivaDeSucursalAsync(sucursalId, ct);
        if (caja is not null)
            return caja;

        caja = new Caja
        {
            SucursalId = sucursalId,
            Nombre = "Caja Sincronizada Principal",
            TipoCaja = TipoCaja.Principal,
            SyncEstado = SyncEstado.Sincronizado
        };

        await _repositorio.AgregarAsync([caja], ct);
        await _repositorio.GuardarCambiosAsync(ct);
        return caja;
    }

    private async Task<Usuario> ObtenerOCrearUsuarioDeRespaldoAsync(Guid sucursalId, CancellationToken ct)
    {
        var usuario = await _repositorio.GetPrimerUsuarioActivoDeSucursalAsync(sucursalId, ct);
        if (usuario is not null)
            return usuario;

        var rol = await _repositorio.GetPrimerRolActivoAsync(ct);
        if (rol is null)
        {
            rol = new Rol { Nombre = "Operador", Permisos = ["Ventas"] };
            await _repositorio.AgregarAsync([rol], ct);
            await _repositorio.GuardarCambiosAsync(ct);
        }

        usuario = new Usuario
        {
            SucursalId = sucursalId,
            RolId = rol.Id,
            Nombre = "Usuario Sincronizado",
            Email = $"sync_{sucursalId}@baresfamilia.com",
            PasswordHash = "SYNC_HASH_PLACEHOLDER",
            PinAcceso = "1234"
        };

        await _repositorio.AgregarAsync([usuario], ct);
        await _repositorio.GuardarCambiosAsync(ct);
        return usuario;
    }

    private async Task ProcesarComandasAsync(List<SyncComandaDto> comandas, Guid sucursalId, CancellationToken ct)
    {
        if (comandas.Count == 0)
            return;

        var existentes = await _repositorio.GetComandasConItemsPorIdAsync(comandas.Select(c => c.Id).ToList(), ct);

        var tiposVentaExistentes = await _repositorio.FiltrarIdsExistentesAsync<TipoVenta>(
            comandas.Select(c => c.TipoVentaId).Distinct().ToList(), ct);
        var usuariosExistentes = await _repositorio.FiltrarIdsExistentesAsync<Usuario>(
            comandas.Select(c => c.UsuarioId).Distinct().ToList(), ct);

        var todosLosItems = comandas.SelectMany(c => c.Items).ToList();
        var productosExistentes = await _repositorio.FiltrarIdsExistentesAsync<Producto>(
            todosLosItems.Select(i => i.ProductoId).Distinct().ToList(), ct);
        var anuladoresExistentes = await _repositorio.FiltrarIdsExistentesAsync<Usuario>(
            todosLosItems.Where(i => i.AnuladoPorUsuarioId.HasValue)
                .Select(i => i.AnuladoPorUsuarioId!.Value).Distinct().ToList(), ct);

        var usuarioDeRespaldo = await _repositorio.GetPrimerUsuarioActivoDeSucursalAsync(sucursalId, ct);
        var nuevas = new List<Comanda>();

        foreach (var sincronizada in comandas)
        {
            var tipoVentaId = await ResolverTipoVentaAsync(sincronizada.TipoVentaId, tiposVentaExistentes, ct);

            // Si el usuario de la comanda no llegó, se la atribuye a un usuario de la
            // sucursal para no perder la venta.
            var usuarioId = usuariosExistentes.Contains(sincronizada.UsuarioId)
                ? sincronizada.UsuarioId
                : usuarioDeRespaldo?.Id ?? sincronizada.UsuarioId;

            Enum.TryParse<ComandaEstado>(sincronizada.Estado, true, out var estado);

            if (existentes.TryGetValue(sincronizada.Id, out var comanda))
            {
                comanda.Estado = estado;
                comanda.Subtotal = sincronizada.Subtotal;
                comanda.Descuento = sincronizada.Descuento;
                comanda.Total = sincronizada.Total;
                comanda.FechaContable = ComoUtc(sincronizada.FechaContable);
                comanda.Turno = sincronizada.Turno;
                comanda.SyncEstado = SyncEstado.Sincronizado;
                comanda.UpdatedAt = DateTime.UtcNow;

                ReemplazarItemsNoAnulados(comanda, sincronizada, productosExistentes, anuladoresExistentes);
            }
            else
            {
                var nueva = new Comanda
                {
                    Id = sincronizada.Id,
                    TipoVentaId = tipoVentaId,
                    MesaId = sincronizada.MesaId,
                    UsuarioId = usuarioId,
                    Estado = estado,
                    Subtotal = sincronizada.Subtotal,
                    Descuento = sincronizada.Descuento,
                    Total = sincronizada.Total,
                    FechaContable = ComoUtc(sincronizada.FechaContable),
                    Turno = sincronizada.Turno,
                    SyncEstado = SyncEstado.Sincronizado,
                    CreatedAt = sincronizada.CreatedAt
                };

                foreach (var item in sincronizada.Items)
                {
                    if (!productosExistentes.Contains(item.ProductoId))
                        continue;

                    nueva.Items.Add(CrearItem(item, sincronizada.CreatedAt, anuladoresExistentes));
                }

                nuevas.Add(nueva);
            }
        }

        await _repositorio.AgregarAsync(nuevas, ct);
        await _repositorio.GuardarCambiosAsync(ct);
    }

    /// <summary>
    /// Si el tipo de venta recibido no existe en la Nube se reemplaza por el primero
    /// activo; si tampoco hay ninguno, se crea uno para poder aceptar la comanda.
    /// </summary>
    private async Task<Guid> ResolverTipoVentaAsync(Guid tipoVentaId, HashSet<Guid> tiposExistentes, CancellationToken ct)
    {
        if (tiposExistentes.Contains(tipoVentaId))
            return tipoVentaId;

        var alternativo = await _repositorio.GetPrimerTipoVentaActivoAsync(ct);
        if (alternativo is not null)
        {
            tiposExistentes.Add(alternativo.Id);
            return alternativo.Id;
        }

        var creado = new TipoVenta { Id = tipoVentaId, Nombre = "Salón", AplicaRecargo = false };
        await _repositorio.AgregarAsync([creado], ct);
        await _repositorio.GuardarCambiosAsync(ct);

        tiposExistentes.Add(creado.Id);
        return creado.Id;
    }

    /// <summary>
    /// Los ítems ya anulados se preservan intactos: no vuelven a cambiar y toda la
    /// información de la anulación vive en el propio ítem. Solo se reemplazan los vigentes.
    /// </summary>
    private void ReemplazarItemsNoAnulados(
        Comanda comanda,
        SyncComandaDto sincronizada,
        HashSet<Guid> productosExistentes,
        HashSet<Guid> anuladoresExistentes)
    {
        var idsPreservados = comanda.Items.Where(i => i.Cancelado).Select(i => i.Id).ToHashSet();
        var aRemover = comanda.Items.Where(i => !i.Cancelado).ToList();

        _repositorio.EliminarItemsDeComanda(aRemover);
        foreach (var item in aRemover)
            comanda.Items.Remove(item);

        foreach (var item in sincronizada.Items)
        {
            if (idsPreservados.Contains(item.Id) || !productosExistentes.Contains(item.ProductoId))
                continue;

            comanda.Items.Add(CrearItem(item, sincronizada.CreatedAt, anuladoresExistentes));
        }
    }

    private static ComandaItem CrearItem(SyncComandaItemDto item, DateTime creadoEn, HashSet<Guid> anuladoresExistentes)
        => new()
        {
            Id = item.Id,
            ProductoId = item.ProductoId,
            Cantidad = item.Cantidad,
            PrecioUnitario = item.PrecioUnitario,
            Notas = item.Notas,
            EstadoPreparacion = EstadoPreparacion.Entregado,
            Cancelado = item.Cancelado,
            MotivoAnulacion = item.MotivoAnulacion,
            // El usuario que anuló puede no haber llegado aún: se guarda sin ese
            // vínculo en lugar de fallar por clave foránea.
            AnuladoPorUsuarioId = item.AnuladoPorUsuarioId.HasValue && anuladoresExistentes.Contains(item.AnuladoPorUsuarioId.Value)
                ? item.AnuladoPorUsuarioId
                : null,
            FechaAnulacion = item.FechaAnulacion,
            CreatedAt = creadoEn
        };

    private async Task ProcesarPagosAsync(List<SyncPagoDto> pagos, CancellationToken ct)
    {
        if (pagos.Count == 0)
            return;

        var yaRegistrados = await _repositorio.FiltrarIdsExistentesAsync<Pago>(pagos.Select(p => p.Id).ToList(), ct);
        var comandasExistentes = await _repositorio.FiltrarIdsExistentesAsync<Comanda>(
            pagos.Select(p => p.ComandaId).Distinct().ToList(), ct);
        var metodosExistentes = await _repositorio.FiltrarIdsExistentesAsync<MetodoPago>(
            pagos.Select(p => p.MetodoPagoId).Distinct().ToList(), ct);

        MetodoPago? metodoDeRespaldo = null;
        var nuevos = new List<Pago>();

        foreach (var sincronizado in pagos)
        {
            if (yaRegistrados.Contains(sincronizado.Id))
                continue;

            // Un pago sin su comanda no tiene sentido contable: se reintenta luego.
            if (!comandasExistentes.Contains(sincronizado.ComandaId))
                continue;

            var metodoPagoId = sincronizado.MetodoPagoId;
            if (!metodosExistentes.Contains(metodoPagoId))
            {
                metodoDeRespaldo ??= await _repositorio.GetPrimerMetodoPagoActivoAsync(ct);
                if (metodoDeRespaldo is null)
                    continue;

                metodoPagoId = metodoDeRespaldo.Id;
            }

            nuevos.Add(new Pago
            {
                Id = sincronizado.Id,
                ComandaId = sincronizado.ComandaId,
                TurnoCajaId = sincronizado.TurnoCajaId,
                MetodoPagoId = metodoPagoId,
                Monto = sincronizado.Monto,
                SyncEstado = SyncEstado.Sincronizado,
                CreatedAt = sincronizado.CreatedAt
            });
        }

        await _repositorio.AgregarAsync(nuevos, ct);
        await _repositorio.GuardarCambiosAsync(ct);
    }

    private async Task ProcesarMovimientosCajaAsync(List<SyncMovimientoDto> movimientos, CancellationToken ct)
    {
        if (movimientos.Count == 0)
            return;

        var yaRegistrados = await _repositorio.FiltrarIdsExistentesAsync<MovimientoCaja>(
            movimientos.Select(m => m.Id).ToList(), ct);

        var nuevos = new List<MovimientoCaja>();
        foreach (var sincronizado in movimientos)
        {
            if (yaRegistrados.Contains(sincronizado.Id))
                continue;

            Enum.TryParse<TipoMovimientoCaja>(sincronizado.Tipo, true, out var tipo);

            nuevos.Add(new MovimientoCaja
            {
                Id = sincronizado.Id,
                TurnoCajaId = sincronizado.TurnoCajaId,
                Tipo = tipo,
                Monto = sincronizado.Monto,
                Concepto = sincronizado.Concepto,
                SyncEstado = SyncEstado.Sincronizado,
                CreatedAt = sincronizado.CreatedAt
            });
        }

        await _repositorio.AgregarAsync(nuevos, ct);
        await _repositorio.GuardarCambiosAsync(ct);
    }

    private async Task ProcesarCierresDiariosAsync(List<SyncCierreDiarioDto> cierres, CancellationToken ct)
    {
        if (cierres.Count == 0)
            return;

        var yaRegistrados = await _repositorio.FiltrarIdsExistentesAsync<CierreDiario>(cierres.Select(c => c.Id).ToList(), ct);
        var cajasExistentes = await _repositorio.FiltrarIdsExistentesAsync<Caja>(
            cierres.Select(c => c.CajaId).Distinct().ToList(), ct);
        var usuariosExistentes = await _repositorio.FiltrarIdsExistentesAsync<Usuario>(
            cierres.Select(c => c.UsuarioCierreId).Distinct().ToList(), ct);

        var nuevos = new List<CierreDiario>();
        foreach (var sincronizado in cierres)
        {
            if (yaRegistrados.Contains(sincronizado.Id))
                continue;

            if (!cajasExistentes.Contains(sincronizado.CajaId) || !usuariosExistentes.Contains(sincronizado.UsuarioCierreId))
                continue;

            nuevos.Add(new CierreDiario
            {
                Id = sincronizado.Id,
                CajaId = sincronizado.CajaId,
                Fecha = ComoUtc(sincronizado.Fecha),
                UsuarioCierreId = sincronizado.UsuarioCierreId,
                TotalVentas = sincronizado.TotalVentas,
                TotalEgresos = sincronizado.TotalEgresos,
                TotalNeto = sincronizado.TotalNeto,
                ResumenJson = sincronizado.ResumenJson,
                Observaciones = sincronizado.Observaciones,
                SyncEstado = SyncEstado.Sincronizado,
                CreatedAt = ComoUtc(sincronizado.CreatedAt)
            });
        }

        await _repositorio.AgregarAsync(nuevos, ct);
        await _repositorio.GuardarCambiosAsync(ct);
    }

    /// <summary>
    /// Todo cliente nuevo estrena cuenta corriente en cero: es la cuenta contra la
    /// que después se aplican los movimientos del lote.
    /// </summary>
    private async Task ProcesarClientesAsync(List<SyncClienteDto> clientes, CancellationToken ct)
    {
        if (clientes.Count == 0)
            return;

        var yaRegistrados = await _repositorio.FiltrarIdsExistentesAsync<Cliente>(clientes.Select(c => c.Id).ToList(), ct);
        var nuevos = clientes.Where(c => !yaRegistrados.Contains(c.Id)).ToList();
        if (nuevos.Count == 0)
            return;

        await _repositorio.AgregarAsync(nuevos.Select(c => new Cliente
        {
            Id = c.Id,
            Nombre = c.Nombre,
            Apellido = c.Apellido,
            SyncEstado = SyncEstado.Sincronizado,
            CreatedAt = c.CreatedAt
        }), ct);
        await _repositorio.GuardarCambiosAsync(ct);

        var cuentasExistentes = await _repositorio.GetCuentasCorrientesPorClienteAsync(
            nuevos.Select(c => c.Id).ToList(), ct);

        await _repositorio.AgregarAsync(nuevos
            .Where(c => !cuentasExistentes.ContainsKey(c.Id))
            .Select(c => new CuentaCorriente { ClienteId = c.Id, SaldoActual = 0 }), ct);
        await _repositorio.GuardarCambiosAsync(ct);
    }

    /// <summary>
    /// Idempotente por id de movimiento: un movimiento ya aplicado no vuelve a
    /// impactar el saldo aunque la sucursal reenvíe el lote.
    /// </summary>
    private async Task ProcesarMovimientosCuentaCorrienteAsync(
        List<SyncMovimientoCuentaCorrienteDto> movimientos, CancellationToken ct)
    {
        if (movimientos.Count == 0)
            return;

        var yaRegistrados = await _repositorio.FiltrarIdsExistentesAsync<MovimientoCuentaCorriente>(
            movimientos.Select(m => m.Id).ToList(), ct);
        var cuentas = await _repositorio.GetCuentasCorrientesPorClienteAsync(
            movimientos.Select(m => m.ClienteId).Distinct().ToList(), ct);
        var comandasExistentes = await _repositorio.FiltrarIdsExistentesAsync<Comanda>(
            movimientos.Where(m => m.ComandaId.HasValue).Select(m => m.ComandaId!.Value).Distinct().ToList(), ct);

        var nuevos = new List<MovimientoCuentaCorriente>();

        foreach (var sincronizado in movimientos)
        {
            if (yaRegistrados.Contains(sincronizado.Id))
                continue;

            // El cliente se dio de alta en el paso anterior; si aun así falta, el
            // movimiento se reintenta en el próximo ciclo.
            if (!cuentas.TryGetValue(sincronizado.ClienteId, out var cuenta))
                continue;

            Enum.TryParse<TipoMovimientoCuentaCorriente>(sincronizado.Tipo, true, out var tipo);

            nuevos.Add(new MovimientoCuentaCorriente
            {
                Id = sincronizado.Id,
                CuentaCorrienteId = cuenta.Id,
                // Si la comanda todavía no se sincronizó, el movimiento se registra sin vínculo.
                ComandaId = sincronizado.ComandaId.HasValue && comandasExistentes.Contains(sincronizado.ComandaId.Value)
                    ? sincronizado.ComandaId
                    : null,
                Tipo = tipo,
                Monto = sincronizado.Monto,
                Detalle = sincronizado.Detalle,
                SyncEstado = SyncEstado.Sincronizado,
                CreatedAt = sincronizado.CreatedAt
            });

            cuenta.SaldoActual += tipo == TipoMovimientoCuentaCorriente.Cargo ? sincronizado.Monto : -sincronizado.Monto;
            cuenta.UpdatedAt = DateTime.UtcNow;
        }

        await _repositorio.AgregarAsync(nuevos, ct);
        await _repositorio.GuardarCambiosAsync(ct);
    }

    /// <summary>
    /// Npgsql exige Kind=Utc para comparar contra columnas timestamptz, y las fechas
    /// deserializadas del lote llegan como Unspecified.
    /// </summary>
    private static DateTime ComoUtc(DateTime fecha) => DateTime.SpecifyKind(fecha, DateTimeKind.Utc);

    // ═══════════════════════════════════════════════════════
    // MONITOREO
    // ═══════════════════════════════════════════════════════

    public async Task<IEnumerable<EstadisticasSincronizacion>> GetEstadisticasAsync(CancellationToken ct = default)
    {
        var sucursales = await _repositorio.GetSucursalesActivasAsync(ct);
        var estadisticas = new List<EstadisticasSincronizacion>();

        foreach (var sucursal in sucursales)
        {
            var estado = _monitor.GetEstado(sucursal.Id, sucursal.Nombre);
            estado.SucursalNombre = sucursal.Nombre;

            estadisticas.Add(new EstadisticasSincronizacion(
                sucursal.Id,
                sucursal.Nombre,
                await _repositorio.GetConteosDeSucursalAsync(sucursal.Id, ct),
                estado,
                _monitor.ContarDispositivosConectados(sucursal.Id),
                _monitor.TieneSincronizacionForzadaPendiente(sucursal.Id),
                _monitor.GetIntervaloSegundos(sucursal.Id)));
        }

        return estadisticas;
    }

    public void ForzarSincronizacion(Guid sucursalId) => _monitor.SolicitarSincronizacionForzada(sucursalId);

    public ChequeoSincronizacion ChequearSincronizacionForzada(Guid sucursalId, Guid? dispositivoId)
    {
        if (dispositivoId.HasValue)
            _monitor.RegistrarLatidoDeDispositivo(sucursalId, dispositivoId.Value);

        return new ChequeoSincronizacion(
            _monitor.ConsumirSincronizacionForzada(sucursalId),
            _monitor.GetIntervaloSegundos(sucursalId));
    }

    public async Task ConfigurarIntervaloAsync(Guid sucursalId, int intervaloSegundos, CancellationToken ct = default)
    {
        if (intervaloSegundos < IntervaloMinimoSegundos || intervaloSegundos > IntervaloMaximoSegundos)
            throw new ReglaNegocioException($"El intervalo debe estar entre {IntervaloMinimoSegundos} y {IntervaloMaximoSegundos} segundos.");

        _monitor.SetIntervaloSegundos(sucursalId, intervaloSegundos);

        var nombre = await _repositorio.GetNombreSucursalAsync(sucursalId, ct) ?? "Sucursal";
        _monitor.AgregarRegistro(sucursalId, nombre, "CONFIG", $"⚙️ Intervalo de sync modificado a {intervaloSegundos}s.", true);
    }

    /// <summary>
    /// El POS reporta con token M2M, así que antes de aceptar el registro se valida
    /// que la sucursal exista y se acotan los textos: el historial vive en memoria y
    /// no tiene límite propio de tamaño por entrada.
    /// </summary>
    public async Task RegistrarReporteAsync(
        Guid sucursalId, string? sucursalNombre, string? tipo, string? mensaje, bool exitoso, CancellationToken ct = default)
    {
        if (!await _repositorio.ExisteSucursalAsync(sucursalId, ct))
            throw new ReglaNegocioException("Sucursal inexistente.");

        _monitor.AgregarRegistro(
            sucursalId,
            Acotar(sucursalNombre, MaximoLargoNombreSucursal),
            Acotar(tipo, MaximoLargoTipo),
            Acotar(mensaje, MaximoLargoMensaje),
            exitoso);
    }

    public void RegistrarErrorDeIngesta(string mensaje)
        => _monitor.AgregarRegistro(Guid.Empty, "Nube", "ERROR", Acotar(mensaje, MaximoLargoMensaje), exitoso: false);

    public IReadOnlyList<RegistroSincronizacion> GetRegistrosRecientes(int cantidad)
        => _monitor.GetRegistrosRecientes(cantidad);

    private static string Acotar(string? texto, int largoMaximo)
    {
        if (string.IsNullOrEmpty(texto))
            return string.Empty;

        return texto.Length <= largoMaximo ? texto : texto[..largoMaximo];
    }
}
