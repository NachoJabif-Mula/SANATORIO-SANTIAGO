using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BaresFamilia.Core.Models.Entities.Catalogo;
using BaresFamilia.Core.Models.Entities.CuentasCorrientes;
using BaresFamilia.Core.Models.Entities.Transaccional;
using BaresFamilia.Core.Models.Enums;
using BaresFamilia.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaresFamilia.Nube.Api.Controllers;

/// <summary>
/// Controlador para procesar la sincronización bidireccional desde las terminales locales.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly NubeContext _context;

    public SyncController(NubeContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Recibe comandas, pagos y movimientos desde el POS local y los persiste en la Nube.
    /// </summary>
    [HttpPost("recibir")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Recibir([FromBody] SyncPayloadDto payload, CancellationToken ct)
    {
        try
        {
            if (payload == null)
                return BadRequest(new { message = "El payload no puede ser nulo." });

            // Obtener la sucursal desde los Claims del Token JWT
            var sucursalIdClaim = User.FindFirst("sucursal_id")?.Value;
            Guid sucursalId = Guid.Empty;

            if (string.IsNullOrEmpty(sucursalIdClaim) || !Guid.TryParse(sucursalIdClaim, out sucursalId))
            {
                // Intentar usar la sucursal de las comandas si no se encuentra en el token
                var primeraComandaConUsuario = payload.Comandas.FirstOrDefault();
                if (primeraComandaConUsuario != null)
                {
                    var usuario = await _context.Usuarios
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == primeraComandaConUsuario.UsuarioId, ct);
                    if (usuario != null)
                    {
                        sucursalId = usuario.SucursalId;
                    }
                }

                if (sucursalId == Guid.Empty)
                {
                    // Si aún es vacía, usar la primera sucursal activa en la base de datos como fallback
                    var primeraSucursal = await _context.Sucursales.FirstOrDefaultAsync(s => s.IsActive, ct);
                    if (primeraSucursal != null)
                    {
                        sucursalId = primeraSucursal.Id;
                    }
                    else
                    {
                        return BadRequest(new { message = "No se pudo determinar la sucursal para la sincronización." });
                    }
                }
            }

            // Registrar estadísticas de sincronización (PUSH)
            var sucursal = await _context.Sucursales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sucursalId, ct);
            var sucursalNombre = sucursal?.Nombre ?? "Sucursal";

            if (payload.Comandas.Any()) SyncManagerStore.RecordPush(sucursalId, sucursalNombre, "comandas");
            if (payload.Pagos.Any()) SyncManagerStore.RecordPush(sucursalId, sucursalNombre, "pagos");
            if (payload.Movimientos.Any()) SyncManagerStore.RecordPush(sucursalId, sucursalNombre, "movimientos");
            if (payload.CierresDiarios != null && payload.CierresDiarios.Any()) SyncManagerStore.RecordPush(sucursalId, sucursalNombre, "cierresdiarios");

            // 1. Procesar las Cajas reales de la sucursal antes que los Turnos de Caja, para que
            // estos últimos puedan enlazar por FK contra la Caja real (antes la Nube fabricaba su
            // propia "Caja Sincronizada Principal" con un Id distinto al de la Caja local real).
            foreach (var syncCaja in payload.Cajas)
            {
                Enum.TryParse<TipoCaja>(syncCaja.TipoCaja, true, out var tipoCaja);
                var cajaExistente = await _context.Cajas.FirstOrDefaultAsync(c => c.Id == syncCaja.Id, ct);
                if (cajaExistente == null)
                {
                    _context.Cajas.Add(new Caja
                    {
                        Id = syncCaja.Id,
                        SucursalId = sucursalId,
                        Nombre = syncCaja.Nombre,
                        TipoCaja = tipoCaja,
                        SyncEstado = SyncEstado.Sincronizado,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true
                    });
                }
                else
                {
                    cajaExistente.Nombre = syncCaja.Nombre;
                    cajaExistente.TipoCaja = tipoCaja;
                    cajaExistente.SyncEstado = SyncEstado.Sincronizado;
                    cajaExistente.UpdatedAt = DateTime.UtcNow;
                }
            }
            if (payload.Cajas.Any())
                await _context.SaveChangesAsync(ct);

            // 2. Procesar los Turnos de Caja reales enviados por el POS (apertura/cierre/fondo/arqueo
            // reales), antes de que el paso de FK-safety de abajo tenga que fabricar un stub.
            foreach (var syncTurno in payload.TurnosCaja)
            {
                var cajaExiste = await _context.Cajas.AnyAsync(c => c.Id == syncTurno.CajaId, ct);
                var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == syncTurno.UsuarioId, ct);
                if (!cajaExiste || !usuarioExiste)
                    continue; // Se reintentará en un próximo ciclo cuando la Caja/Usuario ya haya llegado a la Nube.

                var turnoExistente = await _context.TurnosCaja.FirstOrDefaultAsync(t => t.Id == syncTurno.Id, ct);
                if (turnoExistente == null)
                {
                    _context.TurnosCaja.Add(new TurnoCaja
                    {
                        Id = syncTurno.Id,
                        CajaId = syncTurno.CajaId,
                        UsuarioId = syncTurno.UsuarioId,
                        FechaApertura = DateTime.SpecifyKind(syncTurno.FechaApertura, DateTimeKind.Utc),
                        FechaCierre = syncTurno.FechaCierre.HasValue ? DateTime.SpecifyKind(syncTurno.FechaCierre.Value, DateTimeKind.Utc) : null,
                        FechaContable = DateTime.SpecifyKind(syncTurno.FechaContable, DateTimeKind.Utc),
                        Turno = syncTurno.Turno,
                        FondoInicial = syncTurno.FondoInicial,
                        DiferenciaArqueo = syncTurno.DiferenciaArqueo,
                        SyncEstado = SyncEstado.Sincronizado,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true
                    });
                }
                else
                {
                    // Incluye CajaId/UsuarioId: turnos creados antes por el stub de FK-safety
                    // quedaron enlazados a una Caja/Usuario fabricados y hay que realinearlos
                    // a los reales una vez que estos ya llegaron a la Nube.
                    turnoExistente.CajaId = syncTurno.CajaId;
                    turnoExistente.UsuarioId = syncTurno.UsuarioId;
                    turnoExistente.FechaApertura = DateTime.SpecifyKind(syncTurno.FechaApertura, DateTimeKind.Utc);
                    turnoExistente.FechaCierre = syncTurno.FechaCierre.HasValue ? DateTime.SpecifyKind(syncTurno.FechaCierre.Value, DateTimeKind.Utc) : null;
                    turnoExistente.FechaContable = DateTime.SpecifyKind(syncTurno.FechaContable, DateTimeKind.Utc);
                    turnoExistente.Turno = syncTurno.Turno;
                    turnoExistente.FondoInicial = syncTurno.FondoInicial;
                    turnoExistente.DiferenciaArqueo = syncTurno.DiferenciaArqueo;
                    turnoExistente.SyncEstado = SyncEstado.Sincronizado;
                    turnoExistente.UpdatedAt = DateTime.UtcNow;
                }
            }
            if (payload.TurnosCaja.Any())
                await _context.SaveChangesAsync(ct);

            // 3. Garantizar que existan las Cajas y Turnos de Caja referenciados para evitar errores de FK
            // (cubre solo los casos borde no resueltos por los pasos anteriores, p. ej. turnos de
            // sucursales que activaron el sistema hace tiempo y todavía no reenviaron su turno real).
            var turnosCajaRequeridos = new HashSet<Guid>();
            foreach (var pago in payload.Pagos) turnosCajaRequeridos.Add(pago.TurnoCajaId);
            foreach (var mov in payload.Movimientos) turnosCajaRequeridos.Add(mov.TurnoCajaId);

            if (turnosCajaRequeridos.Any())
            {
                // Obtener turnos existentes
                var turnosExistentes = await _context.TurnosCaja
                    .Where(t => turnosCajaRequeridos.Contains(t.Id))
                    .Select(t => t.Id)
                    .ToListAsync(ct);

                var turnosFaltantes = turnosCajaRequeridos.Except(turnosExistentes).ToList();

                if (turnosFaltantes.Any())
                {
                    // Asegurar que exista al menos una Caja para la sucursal
                    var caja = await _context.Cajas
                        .FirstOrDefaultAsync(c => c.SucursalId == sucursalId && c.IsActive, ct);

                    if (caja == null)
                    {
                        caja = new Caja
                            {
                                Id = Guid.NewGuid(),
                                SucursalId = sucursalId,
                                Nombre = "Caja Sincronizada Principal",
                                TipoCaja = TipoCaja.Principal,
                                SyncEstado = SyncEstado.Sincronizado,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                IsActive = true
                            };
                        _context.Cajas.Add(caja);
                        await _context.SaveChangesAsync(ct);
                    }

                    // Asegurar que exista al menos un usuario para asociar al turno de caja
                    var usuario = await _context.Usuarios
                        .FirstOrDefaultAsync(u => u.SucursalId == sucursalId && u.IsActive, ct);

                    if (usuario == null)
                    {
                        // Obtener primer rol existente
                        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.IsActive, ct);
                        var rolId = rol?.Id ?? Guid.NewGuid();
                        if (rol == null)
                        {
                            var nuevoRol = new Rol
                            {
                                Id = rolId,
                                Nombre = "Operador",
                                Permisos = new List<string> { "Ventas" },
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                IsActive = true
                            };
                            _context.Roles.Add(nuevoRol);
                            await _context.SaveChangesAsync(ct);
                        }

                        usuario = new Usuario
                        {
                            Id = Guid.NewGuid(),
                            SucursalId = sucursalId,
                            RolId = rolId,
                            Nombre = "Usuario Sincronizado",
                            Email = $"sync_{sucursalId}@baresfamilia.com",
                            PasswordHash = "SYNC_HASH_PLACEHOLDER",
                            PinAcceso = "1234",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        _context.Usuarios.Add(usuario);
                        await _context.SaveChangesAsync(ct);
                    }

                    foreach (var turnoId in turnosFaltantes)
                    {
                        var nuevoTurno = new TurnoCaja
                        {
                            Id = turnoId,
                            CajaId = caja.Id,
                            UsuarioId = usuario.Id,
                            FechaApertura = DateTime.UtcNow,
                            FondoInicial = 0,
                            SyncEstado = SyncEstado.Sincronizado,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        _context.TurnosCaja.Add(nuevoTurno);
                    }
                    await _context.SaveChangesAsync(ct);
                }
            }

            // 4. Procesar Comandas
            foreach (var syncComanda in payload.Comandas)
            {
                var comandaExistente = await _context.Comandas
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.Id == syncComanda.Id, ct);

                // Validar que el TipoVenta exista
                var tipoVentaExiste = await _context.TiposVenta.AnyAsync(t => t.Id == syncComanda.TipoVentaId, ct);
                if (!tipoVentaExiste)
                {
                    var defaultTipoVenta = await _context.TiposVenta.FirstOrDefaultAsync(t => t.IsActive, ct);
                    if (defaultTipoVenta == null)
                    {
                        defaultTipoVenta = new TipoVenta
                        {
                            Id = syncComanda.TipoVentaId,
                            Nombre = "Salón",
                            AplicaRecargo = false,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        _context.TiposVenta.Add(defaultTipoVenta);
                        await _context.SaveChangesAsync(ct);
                    }
                    else
                    {
                        syncComanda.TipoVentaId = defaultTipoVenta.Id;
                    }
                }

                // Validar que el Usuario exista
                var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == syncComanda.UsuarioId, ct);
                if (!usuarioExiste)
                {
                    var defaultUsuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.SucursalId == sucursalId && u.IsActive, ct);
                    if (defaultUsuario != null)
                    {
                        syncComanda.UsuarioId = defaultUsuario.Id;
                    }
                }

                Enum.TryParse<ComandaEstado>(syncComanda.Estado, true, out var parsedEstado);

                if (comandaExistente == null)
                {
                    var nuevaComanda = new Comanda
                    {
                        Id = syncComanda.Id,
                        TipoVentaId = syncComanda.TipoVentaId,
                        MesaId = syncComanda.MesaId,
                        UsuarioId = syncComanda.UsuarioId,
                        Estado = parsedEstado,
                        Subtotal = syncComanda.Subtotal,
                        Descuento = syncComanda.Descuento,
                        Total = syncComanda.Total,
                        FechaContable = DateTime.SpecifyKind(syncComanda.FechaContable, DateTimeKind.Utc),
                        Turno = syncComanda.Turno,
                        SyncEstado = SyncEstado.Sincronizado,
                        CreatedAt = syncComanda.CreatedAt,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    foreach (var syncItem in syncComanda.Items)
                    {
                        // Validar que el producto exista para evitar FK errors
                        var productoExiste = await _context.Productos.AnyAsync(p => p.Id == syncItem.ProductoId, ct);
                        if (!productoExiste) continue; // Si no existe en la nube, saltar este ítem para evitar caída del sync

                        // El usuario que anuló puede no haber llegado aún a la Nube; en ese
                        // caso se guarda sin ese vínculo en vez de fallar por FK.
                        Guid? anuladoPorUsuarioId = syncItem.AnuladoPorUsuarioId;
                        if (anuladoPorUsuarioId.HasValue && !await _context.Usuarios.AnyAsync(u => u.Id == anuladoPorUsuarioId.Value, ct))
                            anuladoPorUsuarioId = null;

                        nuevaComanda.Items.Add(new ComandaItem
                        {
                            Id = syncItem.Id,
                            ProductoId = syncItem.ProductoId,
                            Cantidad = syncItem.Cantidad,
                            PrecioUnitario = syncItem.PrecioUnitario,
                            Notas = syncItem.Notas,
                            EstadoPreparacion = EstadoPreparacion.Entregado,
                            Cancelado = syncItem.Cancelado,
                            MotivoAnulacion = syncItem.MotivoAnulacion,
                            AnuladoPorUsuarioId = anuladoPorUsuarioId,
                            FechaAnulacion = syncItem.FechaAnulacion,
                            CreatedAt = syncComanda.CreatedAt,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        });
                    }

                    _context.Comandas.Add(nuevaComanda);
                }
                else
                {
                    comandaExistente.Estado = parsedEstado;
                    comandaExistente.Subtotal = syncComanda.Subtotal;
                    comandaExistente.Descuento = syncComanda.Descuento;
                    comandaExistente.Total = syncComanda.Total;
                    comandaExistente.FechaContable = DateTime.SpecifyKind(syncComanda.FechaContable, DateTimeKind.Utc);
                    comandaExistente.Turno = syncComanda.Turno;
                    comandaExistente.SyncEstado = SyncEstado.Sincronizado;
                    comandaExistente.UpdatedAt = DateTime.UtcNow;

                    // Eliminar y recrear solo los ítems NO anulados; los ya anulados se
                    // preservan intactos para evitar churn innecesario (no cambian una vez
                    // marcados) y porque la información de la anulación vive en el propio
                    // ítem, no en una tabla externa.
                    var idsPreservados = comandaExistente.Items.Where(i => i.Cancelado).Select(i => i.Id).ToHashSet();
                    var itemsARemover = comandaExistente.Items.Where(i => !i.Cancelado).ToList();
                    _context.ComandaItems.RemoveRange(itemsARemover);
                    foreach (var item in itemsARemover) comandaExistente.Items.Remove(item);

                    foreach (var syncItem in syncComanda.Items)
                    {
                        if (idsPreservados.Contains(syncItem.Id)) continue; // ya preservado, no se toca

                        var productoExiste = await _context.Productos.AnyAsync(p => p.Id == syncItem.ProductoId, ct);
                        if (!productoExiste) continue;

                        Guid? anuladoPorUsuarioId = syncItem.AnuladoPorUsuarioId;
                        if (anuladoPorUsuarioId.HasValue && !await _context.Usuarios.AnyAsync(u => u.Id == anuladoPorUsuarioId.Value, ct))
                            anuladoPorUsuarioId = null;

                        comandaExistente.Items.Add(new ComandaItem
                        {
                            Id = syncItem.Id,
                            ProductoId = syncItem.ProductoId,
                            Cantidad = syncItem.Cantidad,
                            PrecioUnitario = syncItem.PrecioUnitario,
                            Notas = syncItem.Notas,
                            EstadoPreparacion = EstadoPreparacion.Entregado,
                            Cancelado = syncItem.Cancelado,
                            MotivoAnulacion = syncItem.MotivoAnulacion,
                            AnuladoPorUsuarioId = anuladoPorUsuarioId,
                            FechaAnulacion = syncItem.FechaAnulacion,
                            CreatedAt = syncComanda.CreatedAt,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        });
                    }
                }
            }
            await _context.SaveChangesAsync(ct);

            // 5. Procesar Pagos
            foreach (var syncPago in payload.Pagos)
            {
                var pagoExistente = await _context.Pagos.AnyAsync(p => p.Id == syncPago.Id, ct);
                if (!pagoExistente)
                {
                    // Validar ComandaId
                    var comandaExiste = await _context.Comandas.AnyAsync(c => c.Id == syncPago.ComandaId, ct);
                    if (!comandaExiste) continue;

                    // Validar MetodoPagoId
                    var metodoPagoExiste = await _context.MetodosPago.AnyAsync(m => m.Id == syncPago.MetodoPagoId, ct);
                    if (!metodoPagoExiste)
                    {
                        var defaultMetodo = await _context.MetodosPago.FirstOrDefaultAsync(m => m.IsActive, ct);
                        if (defaultMetodo != null)
                        {
                            syncPago.MetodoPagoId = defaultMetodo.Id;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    var nuevoPago = new Pago
                    {
                        Id = syncPago.Id,
                        ComandaId = syncPago.ComandaId,
                        TurnoCajaId = syncPago.TurnoCajaId,
                        MetodoPagoId = syncPago.MetodoPagoId,
                        Monto = syncPago.Monto,
                        SyncEstado = SyncEstado.Sincronizado,
                        CreatedAt = syncPago.CreatedAt,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _context.Pagos.Add(nuevoPago);
                }
            }
            await _context.SaveChangesAsync(ct);

            // 6. Procesar Movimientos de Caja
            foreach (var syncMov in payload.Movimientos)
            {
                var movExistente = await _context.MovimientosCaja.AnyAsync(m => m.Id == syncMov.Id, ct);
                if (!movExistente)
                {
                    Enum.TryParse<TipoMovimientoCaja>(syncMov.Tipo, true, out var parsedTipo);

                    var nuevoMov = new MovimientoCaja
                    {
                        Id = syncMov.Id,
                        TurnoCajaId = syncMov.TurnoCajaId,
                        Tipo = parsedTipo,
                        Monto = syncMov.Monto,
                        Concepto = syncMov.Concepto,
                        SyncEstado = SyncEstado.Sincronizado,
                        CreatedAt = syncMov.CreatedAt,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _context.MovimientosCaja.Add(nuevoMov);
                }
            }
            await _context.SaveChangesAsync(ct);

            // 7. Procesar Cierres Diarios
            if (payload.CierresDiarios != null)
            {
                foreach (var syncCierre in payload.CierresDiarios)
                {
                    var cierreExistente = await _context.CierresDiarios.AnyAsync(c => c.Id == syncCierre.Id, ct);
                    if (!cierreExistente)
                    {
                        // Validar integridad referencial (UsuarioCierreId y CajaId)
                        var cajaExiste = await _context.Cajas.AnyAsync(c => c.Id == syncCierre.CajaId, ct);
                        if (!cajaExiste) continue;

                        var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == syncCierre.UsuarioCierreId, ct);
                        if (!usuarioExiste) continue;

                        var nuevoCierre = new CierreDiario
                        {
                            Id = syncCierre.Id,
                            CajaId = syncCierre.CajaId,
                            Fecha = DateTime.SpecifyKind(syncCierre.Fecha, DateTimeKind.Utc),
                            UsuarioCierreId = syncCierre.UsuarioCierreId,
                            TotalVentas = syncCierre.TotalVentas,
                            TotalEgresos = syncCierre.TotalEgresos,
                            TotalNeto = syncCierre.TotalNeto,
                            ResumenJson = syncCierre.ResumenJson,
                            Observaciones = syncCierre.Observaciones,
                            SyncEstado = SyncEstado.Sincronizado,
                            CreatedAt = DateTime.SpecifyKind(syncCierre.CreatedAt, DateTimeKind.Utc),
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        _context.CierresDiarios.Add(nuevoCierre);
                    }
                }
                await _context.SaveChangesAsync(ct);
            }

            // 8. Procesar Clientes (altas locales) — crea la CuentaCorriente inicial si el cliente es nuevo.
            // Estos clientes son "solo del POS": únicamente se sincroniza el nombre (dato necesario para
            // el reporte de Cuentas Corrientes). Teléfono/Email/Límite de Crédito quedan solo en el POS.
            foreach (var syncCliente in payload.Clientes)
            {
                var clienteExistente = await _context.Clientes.AnyAsync(c => c.Id == syncCliente.Id, ct);
                if (!clienteExistente)
                {
                    var nuevoCliente = new Cliente
                    {
                        Id = syncCliente.Id,
                        Nombre = syncCliente.Nombre,
                        Apellido = syncCliente.Apellido,
                        SyncEstado = SyncEstado.Sincronizado,
                        CreatedAt = syncCliente.CreatedAt,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _context.Clientes.Add(nuevoCliente);
                    await _context.SaveChangesAsync(ct);

                    var cuentaExiste = await _context.CuentasCorrientes.AnyAsync(cc => cc.ClienteId == syncCliente.Id, ct);
                    if (!cuentaExiste)
                    {
                        _context.CuentasCorrientes.Add(new CuentaCorriente
                        {
                            ClienteId = syncCliente.Id,
                            SaldoActual = 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        });
                        await _context.SaveChangesAsync(ct);
                    }
                }
            }

            // 9. Procesar Movimientos de Cuenta Corriente — idempotente, aplica el delta al saldo
            foreach (var syncMov in payload.MovimientosCuentaCorriente)
            {
                var movExistente = await _context.Set<MovimientoCuentaCorriente>().AnyAsync(m => m.Id == syncMov.Id, ct);
                if (movExistente) continue;

                var cuenta = await _context.CuentasCorrientes.FirstOrDefaultAsync(cc => cc.ClienteId == syncMov.ClienteId, ct);
                if (cuenta is null) continue; // el cliente debe existir (procesado en el paso anterior)

                // Validar integridad referencial de la comanda; si no existe (aún no sincronizada), se registra sin vínculo.
                Guid? comandaId = syncMov.ComandaId;
                if (comandaId.HasValue)
                {
                    var comandaExiste = await _context.Comandas.AnyAsync(c => c.Id == comandaId.Value, ct);
                    if (!comandaExiste) comandaId = null;
                }

                Enum.TryParse<TipoMovimientoCuentaCorriente>(syncMov.Tipo, true, out var parsedTipo);

                var nuevoMovimiento = new MovimientoCuentaCorriente
                {
                    Id = syncMov.Id,
                    CuentaCorrienteId = cuenta.Id,
                    ComandaId = comandaId,
                    Tipo = parsedTipo,
                    Monto = syncMov.Monto,
                    Detalle = syncMov.Detalle,
                    SyncEstado = SyncEstado.Sincronizado,
                    CreatedAt = syncMov.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                _context.Set<MovimientoCuentaCorriente>().Add(nuevoMovimiento);

                cuenta.SaldoActual += parsedTipo == TipoMovimientoCuentaCorriente.Cargo ? syncMov.Monto : -syncMov.Monto;
                cuenta.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(ct);

            return Ok(new { message = "Sincronización procesada correctamente en la nube." });
        }
        catch (Exception ex)
        {
            SyncManagerStore.AddLog(Guid.Empty, "Nube", "ERROR", $"Error al procesar payload de sync: {ex.Message}", false);
            return BadRequest(new { message = $"Error en el servidor de Nube: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene las estadísticas de sincronización para todas las sucursales.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var sucursales = await _context.Sucursales.Where(s => s.IsActive).ToListAsync(ct);
        var statsList = new List<object>();

        foreach (var suc in sucursales)
        {
            var sucursalId = suc.Id;
            var info = SyncManagerStore.Status.GetOrAdd(sucursalId, id => new SyncInfo { SucursalId = sucursalId, SucursalNombre = suc.Nombre });
            info.SucursalNombre = suc.Nombre;

            // Consultar conteos en la base de datos de la Nube
            var comandasCount = await _context.Comandas.CountAsync(c => c.Usuario.SucursalId == sucursalId, ct);
            var pagosCount = await _context.Pagos.CountAsync(p => p.Comanda.Usuario.SucursalId == sucursalId, ct);
            var movimientosCount = await _context.MovimientosCaja.CountAsync(m => m.TurnoCaja.Usuario.SucursalId == sucursalId, ct);
            var cierresDiariosCount = await _context.CierresDiarios.CountAsync(c => c.Caja.SucursalId == sucursalId, ct);
            var mesasCount = await _context.Mesas.CountAsync(m => m.SucursalId == sucursalId, ct);
            var configuracionesCount = await _context.ConfiguracionesPos.CountAsync(c => c.SucursalId == sucursalId, ct);
            var usuariosCount = await _context.Usuarios.CountAsync(u => u.SucursalId == sucursalId, ct);
            var rolesCount = await _context.Roles.CountAsync(ct);

            var interval = SyncManagerStore.SyncIntervals.TryGetValue(sucursalId, out var sec) ? sec : 30;
            var thresholdSeconds = (interval * 2) + 10;
            var activeThreshold = DateTime.UtcNow.AddSeconds(-thresholdSeconds);

            int connectedPosCount = 0;
            if (SyncManagerStore.ConnectedDevices.TryGetValue(sucursalId, out var devices))
            {
                connectedPosCount = devices.Values.Count(lastSeen => lastSeen > activeThreshold);
            }
 
            statsList.Add(new
            {
                sucursalId = suc.Id,
                sucursalNombre = suc.Nombre,
                comandasCount,
                pagosCount,
                movimientosCount,
                cierresDiariosCount,
                mesasCount,
                configuracionesCount,
                usuariosCount,
                rolesCount,
                connectedPosCount,
                lastPushComandas = info.LastPushComandas,
                lastPushPagos = info.LastPushPagos,
                lastPushMovimientos = info.LastPushMovimientos,
                lastPushCierresDiarios = info.LastPushCierresDiarios,
                lastPullConfig = info.LastPullConfig,
                lastPullMesas = info.LastPullMesas,
                lastPullRoles = info.LastPullRoles,
                lastPullUsuarios = info.LastPullUsuarios,
                forceSyncPending = SyncManagerStore.ForceSyncRequests.ContainsKey(sucursalId) && SyncManagerStore.ForceSyncRequests[sucursalId],
                syncIntervalSeconds = interval
            });
        }

        return Ok(statsList);
    }

    /// <summary>
    /// Fuerza una sincronización inmediata para la sucursal indicada.
    /// </summary>
    [HttpPost("force/{sucursalId:guid}")]
    public IActionResult ForceSync(Guid sucursalId)
    {
        SyncManagerStore.ForceSyncRequests[sucursalId] = true;
        
        // Registrar en el listado
        var info = SyncManagerStore.Status.GetOrAdd(sucursalId, id => new SyncInfo { SucursalId = sucursalId });
        
        return Ok(new { message = "Sincronización forzada solicitada correctamente en la Nube." });
    }

    /// <summary>
    /// Consulta si hay una solicitud de sincronización forzada para la sucursal y retorna el intervalo.
    /// </summary>
    [HttpGet("check-force/{sucursalId:guid}")]
    [AllowAnonymous]
    public IActionResult CheckForceSync(Guid sucursalId, [FromQuery] Guid? dispositivoId)
    {
        if (dispositivoId.HasValue)
        {
            var devices = SyncManagerStore.ConnectedDevices.GetOrAdd(sucursalId, _ => new ConcurrentDictionary<Guid, DateTime>());
            devices[dispositivoId.Value] = DateTime.UtcNow;
        }

        var force = SyncManagerStore.ForceSyncRequests.TryRemove(sucursalId, out var f) && f;
        var interval = SyncManagerStore.SyncIntervals.TryGetValue(sucursalId, out var sec) ? sec : 30;

        return Ok(new
        {
            forceSync = force,
            syncIntervalSeconds = interval
        });
    }

    /// <summary>
    /// Configura el intervalo de sincronización para una sucursal.
    /// </summary>
    [HttpPost("interval/{sucursalId:guid}")]
    public IActionResult SetSyncInterval(Guid sucursalId, [FromBody] SetSyncIntervalRequest request)
    {
        if (request == null || request.IntervalSeconds < 5 || request.IntervalSeconds > 300)
        {
            return BadRequest(new { message = "El intervalo debe estar entre 5 y 300 segundos." });
        }

        SyncManagerStore.SyncIntervals[sucursalId] = request.IntervalSeconds;
        SyncManagerStore.GuardarIntervalos();

        // Registrar log de cambio de config
        var sucursalNombre = _context.Sucursales.AsNoTracking()
            .Where(s => s.Id == sucursalId)
            .Select(s => s.Nombre)
            .FirstOrDefault() ?? "Sucursal";
        SyncManagerStore.AddLog(sucursalId, sucursalNombre, "CONFIG", $"⚙️ Intervalo de sync modificado a {request.IntervalSeconds}s.", true);

        return Ok(new { message = $"Intervalo de sincronización actualizado a {request.IntervalSeconds} segundos." });
    }

    /// <summary>
    /// Recibe reportes de logs de las terminales locales.
    /// </summary>
    [HttpPost("log")]
    [AllowAnonymous]
    public IActionResult ReportLog([FromBody] ReportLogRequest request)
    {
        if (request == null) return BadRequest();

        SyncManagerStore.AddLog(request.SucursalId, request.SucursalNombre, request.Tipo, request.Mensaje, request.Exitoso);
        return Ok();
    }

    /// <summary>
    /// Obtiene el historial reciente de logs de sincronización.
    /// </summary>
    [HttpGet("logs")]
    public IActionResult GetLogs()
    {
        var logs = SyncManagerStore.RecentLogs.OrderByDescending(l => l.Timestamp).Take(50).ToList();
        return Ok(logs);
    }
}

// ═══════════════════════════════════
// DTOs de Sincronización (Nube)
// ═══════════════════════════════════

public class SetSyncIntervalRequest
{
    public int IntervalSeconds { get; set; }
}

public class ReportLogRequest
{
    public Guid SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty; // "PUSH", "PULL", "ERROR"
    public string Mensaje { get; set; } = string.Empty;
    public bool Exitoso { get; set; }
}

public class SyncLogDto
{
    public Guid Id { get; set; }
    public Guid SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Tipo { get; set; } = string.Empty; // "PUSH", "PULL", "CONFIG", "ERROR"
    public string Mensaje { get; set; } = string.Empty;
    public bool Exitoso { get; set; }
}

public class SyncPayloadDto
{
    public DateTime Timestamp { get; set; }
    public List<SyncComandaDto> Comandas { get; set; } = [];
    public List<SyncPagoDto> Pagos { get; set; } = [];
    public List<SyncMovimientoDto> Movimientos { get; set; } = [];
    public List<SyncCierreDiarioDto> CierresDiarios { get; set; } = [];
    public List<SyncClienteDto> Clientes { get; set; } = [];
    public List<SyncMovimientoCuentaCorrienteDto> MovimientosCuentaCorriente { get; set; } = [];
    public List<SyncTurnoCajaDto> TurnosCaja { get; set; } = [];
    public List<SyncCajaDto> Cajas { get; set; } = [];
}

/// <summary>
/// DTO de recepción (PUSH del POS) de la Caja real de la sucursal.
/// </summary>
public class SyncCajaDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string TipoCaja { get; set; } = string.Empty;
}

/// <summary>
/// DTO de recepción (PUSH del POS) del turno de caja real. Reemplaza el turno "stub"
/// que antes se fabricaba solo para satisfacer la FK de Pagos/Movimientos.
/// </summary>
public class SyncTurnoCajaDto
{
    public Guid Id { get; set; }
    public Guid CajaId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public DateTime FechaContable { get; set; }
    public string Turno { get; set; } = string.Empty;
    public decimal FondoInicial { get; set; }
    public decimal? DiferenciaArqueo { get; set; }
}

/// <summary>
/// DTO de recepción (PUSH del POS). Solo lleva el nombre: los clientes altados en el POS
/// son locales a esa sucursal, y a la Nube únicamente le interesa el dato necesario para el reporte.
/// </summary>
public class SyncClienteDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SyncMovimientoCuentaCorrienteDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid? ComandaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SyncCierreDiarioDto
{
    public Guid Id { get; set; }
    public Guid CajaId { get; set; }
    public DateTime Fecha { get; set; }
    public Guid UsuarioCierreId { get; set; }
    public decimal TotalVentas { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal TotalNeto { get; set; }
    public string? ResumenJson { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SyncComandaDto
{
    public Guid Id { get; set; }
    public Guid TipoVentaId { get; set; }
    public Guid? MesaId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
    public DateTime FechaContable { get; set; }
    public string Turno { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<SyncComandaItemDto> Items { get; set; } = [];
}

public class SyncComandaItemDto
{
    public Guid Id { get; set; }
    public Guid ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public string? Notas { get; set; }
    public bool Cancelado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public Guid? AnuladoPorUsuarioId { get; set; }
    public DateTime? FechaAnulacion { get; set; }
}

public class SyncPagoDto
{
    public Guid Id { get; set; }
    public Guid ComandaId { get; set; }
    public Guid TurnoCajaId { get; set; }
    public Guid MetodoPagoId { get; set; }
    public decimal Monto { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SyncMovimientoDto
{
    public Guid Id { get; set; }
    public Guid TurnoCajaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public static class SyncManagerStore
{
    public static readonly ConcurrentDictionary<Guid, SyncInfo> Status = new();
    public static readonly ConcurrentDictionary<Guid, bool> ForceSyncRequests = new();
    public static readonly ConcurrentDictionary<Guid, int> SyncIntervals = new();
    public static readonly ConcurrentQueue<SyncLogDto> RecentLogs = new();
    public static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, DateTime>> ConnectedDevices = new();

    private static readonly string NubeIntervalFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_intervals.json");

    static SyncManagerStore()
    {
        CargarIntervalos();
    }

    private static void CargarIntervalos()
    {
        if (File.Exists(NubeIntervalFilePath))
        {
            try
            {
                var json = File.ReadAllText(NubeIntervalFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<Guid, int>>(json);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        SyncIntervals[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch {}
        }
    }

    public static void GuardarIntervalos()
    {
        try
        {
            var dict = SyncIntervals.ToDictionary(k => k.Key, v => v.Value);
            var json = JsonSerializer.Serialize(dict);
            File.WriteAllText(NubeIntervalFilePath, json);
        }
        catch {}
    }

    public static void AddLog(Guid sucursalId, string sucursalNombre, string tipo, string mensaje, bool exitoso)
    {
        RecentLogs.Enqueue(new SyncLogDto
        {
            Id = Guid.NewGuid(),
            SucursalId = sucursalId,
            SucursalNombre = sucursalNombre,
            Timestamp = DateTime.UtcNow,
            Tipo = tipo,
            Mensaje = mensaje,
            Exitoso = exitoso
        });

        while (RecentLogs.Count > 100)
        {
            RecentLogs.TryDequeue(out _);
        }
    }

    public static void RecordPush(Guid sucursalId, string sucursalNombre, string type)
    {
        var info = Status.GetOrAdd(sucursalId, id => new SyncInfo { SucursalId = sucursalId, SucursalNombre = sucursalNombre });
        info.SucursalNombre = sucursalNombre;
        
        if (type == "comandas") info.LastPushComandas = DateTime.UtcNow;
        else if (type == "pagos") info.LastPushPagos = DateTime.UtcNow;
        else if (type == "movimientos") info.LastPushMovimientos = DateTime.UtcNow;
        else if (type == "cierresdiarios") info.LastPushCierresDiarios = DateTime.UtcNow;
    }

    public static void RecordPull(Guid sucursalId, string type)
    {
        var info = Status.GetOrAdd(sucursalId, id => new SyncInfo { SucursalId = sucursalId });
        
        if (type == "config") info.LastPullConfig = DateTime.UtcNow;
        else if (type == "mesas") info.LastPullMesas = DateTime.UtcNow;
        else if (type == "roles") info.LastPullRoles = DateTime.UtcNow;
        else if (type == "usuarios") info.LastPullUsuarios = DateTime.UtcNow;
    }
}

public class SyncInfo
{
    public Guid SucursalId { get; set; }
    public string SucursalNombre { get; set; } = "Sucursal";
    public DateTime? LastPushComandas { get; set; }
    public DateTime? LastPushPagos { get; set; }
    public DateTime? LastPushMovimientos { get; set; }
    public DateTime? LastPushCierresDiarios { get; set; }
    public DateTime? LastPullConfig { get; set; }
    public DateTime? LastPullMesas { get; set; }
    public DateTime? LastPullRoles { get; set; }
    public DateTime? LastPullUsuarios { get; set; }
}

