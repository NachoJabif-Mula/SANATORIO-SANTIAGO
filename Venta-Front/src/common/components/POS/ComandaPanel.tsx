import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Plus, Minus, Trash2, ShoppingCart, CreditCard, Receipt, Send, Ban } from 'lucide-react';
import { useComanda, useAuth } from '@/contexts/AppContext';
import { useCaja } from '@/contexts/CajaContext';
import type { ItemComanda } from '@/common/types';
import api from '@/services/api';
import ModalCobro from '@/common/components/POS/ModalCobro';
import { ModalAnulacion } from '@/common/components/POS/ModalAnulacion';
import { TicketModal } from '@/common/components/POS/TicketModal';
import { isGuid } from '@/common/utils/guid';

function formatARS(monto: number): string {
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(monto);
}

export default function ComandaPanel() {
  const { items, comandaId, total, cantidadItems, huboModificaciones, incrementar, decrementar, quitarItem, limpiarComanda, marcarItemCancelado } = useComanda();
  const { usuario, logout, tienePermiso } = useAuth();
  const { turnoActivo } = useCaja();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [showModalCobro, setShowModalCobro] = useState(false);
  const [cobrado, setCobrado] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [mensajeCobro, setMensajeCobro] = useState('¡Cobro Registrado!');
  const [showSuccessOverlay, setShowSuccessOverlay] = useState(false);
  const [successOverlayMessage, setSuccessOverlayMessage] = useState('');
  const [itemAAnular, setItemAAnular] = useState<ItemComanda | null>(null);
  const [showModalAnularComanda, setShowModalAnularComanda] = useState(false);
  const [ticketModal, setTicketModal] = useState<{ titulo: string; contenido: string; onCerrar: () => void } | null>(null);

  const puedeAnular = !!comandaId && tienePermiso('pos.anular');

  const clienteIdParam = searchParams.get('clienteId');
  const clienteNombreParam = searchParams.get('clienteNombre');
  const clienteAsociado = isGuid(clienteIdParam) && clienteNombreParam
    ? { id: clienteIdParam as string, nombreCompleto: decodeURIComponent(clienteNombreParam) }
    : null;

  const handleAnularItem = async (motivo: string) => {
    if (!itemAAnular || !comandaId || !usuario) return;
    await api.post(`/comanda/${comandaId}/items/${itemAAnular.id}/anular`, {
      usuarioId: usuario.id,
      motivo
    });
    marcarItemCancelado(itemAAnular.id);
    setItemAAnular(null);
  };

  const handleAnularComanda = async (motivo: string) => {
    if (!comandaId || !usuario) return;
    await api.post(`/comanda/${comandaId}/anular`, {
      usuarioId: usuario.id,
      motivo
    });
    setShowModalAnularComanda(false);
    limpiarComanda();
    navigate('/');
  };

  const handleCobrar = () => {
    setShowModalCobro(true);
  };
 
  const procesarCobro = (mensaje = '¡Cobro Registrado!') => {
    setMensajeCobro(mensaje);
    setCobrado(true);
    const esMozo = usuario && (usuario.rol === 'mozo' || usuario.rol === 'moso');
    if (esMozo) {
      setSuccessOverlayMessage(mensaje);
      setShowSuccessOverlay(true);
    }
    setTimeout(() => {
      limpiarComanda();
      setCobrado(false);
      if (esMozo) {
        setShowSuccessOverlay(false);
        logout();
        navigate('/login', { replace: true });
      } else {
        navigate('/'); // Retornar al plano del salón
      }
    }, 2000);
  };
 
  const handleEnviar = async () => {
    if (!usuario) {
      alert('Debe iniciar sesión para enviar comandas.');
      return;
    }
    setEnviando(true);
    try {
      const tvsRes = await api.get('/catalogo/tipos-venta');
      if (tvsRes.data.length === 0) {
        throw new Error('No hay tipos de venta configurados.');
      }
      const tipoVentaId = tvsRes.data[0].id;
      
      const rawMesaId = searchParams.get('mesaId');
      const isGuid = (val: string | null) => 
        val ? /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(val) : false;
      const mesaId = isGuid(rawMesaId) ? rawMesaId : null;

      const mappedItems = items.filter(i => !i.cancelado).map(i => ({
        productoId: i.producto.id,
        cantidad: i.cantidad,
        precioUnitario: i.producto.precio
      }));

      let comandaRes;
      if (comandaId) {
        comandaRes = await api.put(`/comanda/${comandaId}`, {
          usuarioId: usuario.id,
          subtotal: total,
          descuento: 0,
          total: total,
          items: mappedItems
        });
      } else {
        comandaRes = await api.post('/comanda', {
          tipoVentaId,
          mesaId,
          clienteId: clienteAsociado?.id ?? null,
          usuarioId: usuario.id,
          subtotal: total,
          descuento: 0,
          total: total,
          items: mappedItems
        });
      }

      const impresion = comandaRes.data?.ultimaImpresion;
      if (impresion?.simulado && impresion?.ticketContenido) {
        setTicketModal({
          titulo: 'Ticket de Comanda',
          contenido: impresion.ticketContenido,
          onCerrar: () => {
            setTicketModal(null);
            procesarCobro('¡Pedido Enviado a Cocina!');
          }
        });
      } else {
        procesarCobro('¡Pedido Enviado a Cocina!');
      }
    } catch (err: any) {
      console.error('Error al enviar la comanda:', err);
      const msg = err?.response?.data?.message || err.message || 'Error de conexión';
      alert(`No se pudo enviar el pedido: ${msg}`);
    } finally {
      setEnviando(false);
    }
  };

  const handleConfirmarCobro = async (pagos: { metodoPagoId: string; monto: number }[]) => {
    if (!usuario || !turnoActivo) {
      alert('Debe iniciar sesión y tener una caja abierta.');
      return;
    }

    try {
      let activeId = comandaId;

      // 1. Obtener tipos de venta y usar el primero
      const tvsRes = await api.get('/catalogo/tipos-venta');
      if (tvsRes.data.length === 0) {
        throw new Error('No hay tipos de venta configurados en el sistema.');
      }
      const tipoVentaId = tvsRes.data[0].id;
      
      const rawMesaId = searchParams.get('mesaId');
      const isGuid = (val: string | null) => 
        val ? /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(val) : false;
      const mesaId = isGuid(rawMesaId) ? rawMesaId : null;

      const mappedItems = items.filter(i => !i.cancelado).map(i => ({
        productoId: i.producto.id,
        cantidad: i.cantidad,
        precioUnitario: i.producto.precio
      }));

      // 2. Crear cabecera comanda o actualizar si ya existe abierta
      if (!activeId) {
        const comandaRes = await api.post('/comanda', {
          tipoVentaId,
          mesaId,
          clienteId: clienteAsociado?.id ?? null,
          usuarioId: usuario.id,
          subtotal: total,
          descuento: 0,
          total: total,
          items: mappedItems
        });
        activeId = comandaRes.data.id;
      } else {
        await api.put(`/comanda/${activeId}`, {
          usuarioId: usuario.id,
          subtotal: total,
          descuento: 0,
          total: total,
          items: mappedItems
        });
      }

      // 3. Registrar cobro
      const cobrarRes = await api.post(`/comanda/${activeId}/cobrar`, {
        turnoCajaId: turnoActivo.turnoId,
        pagos: pagos
      });

      setShowModalCobro(false);

      const resultadoCobro = cobrarRes.data;
      const mensajeFinal = resultadoCobro.facturaAfipEmitida ? '¡Comprobante AFIP Emitido!' : '¡Cobro Registrado!';
      if (resultadoCobro.simulado && resultadoCobro.ordenImpresionUsb) {
        setTicketModal({
          titulo: 'Comprobante Fiscal (AFIP simulado)',
          contenido: resultadoCobro.ordenImpresionUsb,
          onCerrar: () => {
            setTicketModal(null);
            procesarCobro(mensajeFinal);
          }
        });
      } else {
        procesarCobro(mensajeFinal);
      }
    } catch (err: any) {
      console.error('Error al procesar el cobro:', err);
      const msg = err?.response?.data?.message || err.message || 'Error de conexión';
      alert(`No se pudo procesar el cobro: ${msg}`);
      throw err; // Re-lanzar para que el modal muestre el error
    }
  };

  return (
    <>
      <div className="flex flex-col h-full bg-surface-base border-l border-border-default">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b border-border-default">
          <span className="mono-label">Ticket</span>
          <div className="flex items-center gap-3">
            <span className="font-mono text-[11px] text-text-muted">{cantidadItems} {cantidadItems === 1 ? 'ítem' : 'ítems'}</span>
          </div>
        </div>

        {/* Items */}
        <div className="flex-1 overflow-y-auto p-2">
          {items.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-text-muted">
              <ShoppingCart className="w-10 h-10 mb-3 opacity-15" />
              <p className="text-sm font-semibold text-text-secondary">Comanda vacía</p>
              <p className="text-xs mt-0.5 text-center text-text-muted/80">Seleccione productos del catálogo</p>
            </div>
          ) : (
            items.map((item: ItemComanda) => {
              const yaComandado = isGuid(item.id) && !!comandaId;
              return (
              <div key={item.id} className={`flex flex-col gap-2 p-3 border-b animate-fade-in ${
                item.cancelado ? 'border-danger-500/20 opacity-70' : 'border-border-default'
              }`}>
                <div className="flex items-start gap-2">
                  <span className={`flex-1 min-w-0 text-[15px] font-medium leading-tight ${item.cancelado ? 'text-danger-500 line-through' : 'text-text-primary'}`}>
                    {item.producto.nombre}
                  </span>
                  <span className={`font-mono text-[15px] font-semibold ${item.cancelado ? 'text-danger-500 line-through' : 'text-text-primary'}`}>
                    {formatARS(item.subtotal)}
                  </span>
                </div>
                {item.cancelado && (
                  <div className="text-[11.5px] text-danger-500 font-semibold uppercase tracking-wide">Anulado</div>
                )}

                <div className="flex items-center gap-1.5">
                  {item.cancelado ? (
                    <span className="font-mono text-[13px] text-text-muted">{item.cantidad} un.</span>
                  ) : yaComandado ? (
                    // Ítem ya comandado: la cantidad es fija (ya está en base de datos).
                    // Para pedir más de este producto se agrega desde el catálogo, lo que
                    // genera una fila nueva en el ticket en vez de sumarse acá.
                    <span className="min-w-[30px] text-center font-mono text-[15px] font-semibold text-text-primary">{item.cantidad}</span>
                  ) : (
                    <>
                      <button id={`btn-dec-${item.id}`} onClick={() => decrementar(item.id)}
                        className="touch-btn w-9 h-9 bg-surface-overlay border border-border-default rounded-[var(--radius-btn)] flex items-center justify-center min-h-0 min-w-0 text-text-primary">
                        <Minus className="w-3.5 h-3.5" />
                      </button>
                      <span className="min-w-[30px] text-center font-mono text-[15px] font-semibold text-text-primary">{item.cantidad}</span>
                      <button id={`btn-inc-${item.id}`} onClick={() => incrementar(item.id)}
                        className="touch-btn w-9 h-9 bg-surface-overlay border border-border-default rounded-[var(--radius-btn)] flex items-center justify-center min-h-0 min-w-0 text-text-primary">
                        <Plus className="w-3.5 h-3.5" />
                      </button>
                    </>
                  )}

                  <div className="flex-1" />

                  {item.cancelado ? (
                    <span className="w-8 h-8 flex items-center justify-center" title="Ítem anulado">
                      <Ban className="w-3.5 h-3.5 text-danger-500" />
                    </span>
                  ) : yaComandado ? (
                    puedeAnular && (
                      <button id={`btn-anular-${item.id}`} onClick={() => setItemAAnular(item)}
                        title="Anular ítem (ya comandado)"
                        className="touch-btn h-8 px-3 rounded-[var(--radius-btn)] border border-border-default text-[11.5px] font-semibold text-text-muted hover:border-danger-500 hover:text-danger-500 min-h-0">
                        Quitar
                      </button>
                    )
                  ) : (
                    <button id={`btn-del-${item.id}`} onClick={() => quitarItem(item.id)}
                      className="touch-btn h-8 px-3 rounded-[var(--radius-btn)] border border-border-default text-[11.5px] font-semibold text-text-muted hover:border-danger-500 hover:text-danger-500 min-h-0 flex items-center gap-1">
                      <Trash2 className="w-3.5 h-3.5" />
                      Quitar
                    </button>
                  )}
                </div>
              </div>
              );
            })
          )}
        </div>

        {/* Footer */}
        <div className="border-t border-border-default p-4 space-y-2.5">
          <div className="flex items-center justify-between">
            <span className="text-xs text-text-muted">Subtotal</span>
            <span className="font-mono text-xs text-text-muted">{formatARS(total)}</span>
          </div>
          <div className="flex items-center justify-between">
            <span className="text-[13px] font-semibold text-text-primary">Total</span>
            <span className="font-mono text-2xl font-semibold tracking-tight text-text-primary">{formatARS(total)}</span>
          </div>

          <div className={`grid ${usuario && (usuario.rol === 'mozo' || usuario.rol === 'moso') ? 'grid-cols-1' : 'grid-cols-2'} gap-2 mt-1`}>
            <button id="btn-enviar" onClick={handleEnviar} disabled={items.length === 0 || !huboModificaciones || cobrado || enviando}
              className={`touch-btn flex items-center justify-center gap-1.5 h-[60px] rounded-[var(--radius-btn)] text-[15px] font-semibold min-h-0 border ${
                items.length === 0 || !huboModificaciones || cobrado || enviando
                  ? 'bg-surface-overlay text-text-muted border-border-default cursor-not-allowed'
                  : 'bg-surface-overlay text-text-primary border-border-default hover:border-amber-500 hover:text-amber-500'
              }`}>
              <Send className="w-4 h-4" />
              {enviando ? 'Enviando...' : 'Comandar'}
            </button>

            {!(usuario && (usuario.rol === 'mozo' || usuario.rol === 'moso')) && (
              <button id="btn-cobrar" onClick={handleCobrar} disabled={items.length === 0 || cobrado || enviando}
                className={`touch-btn flex items-center justify-center gap-2 h-[60px] rounded-[var(--radius-btn)] text-[15px] font-semibold min-h-0 border ${
                  cobrado
                    ? 'bg-success-500 text-white border-success-500 col-span-2'
                    : items.length === 0 || enviando
                    ? 'bg-surface-overlay text-text-muted border-border-default cursor-not-allowed'
                    : 'bg-amber-500 text-white border-amber-500 hover:bg-amber-600'
                }`}>
                {cobrado ? (<><Receipt className="w-5 h-5" />{mensajeCobro}</>) : (<><CreditCard className="w-5 h-5" />Rendir pedido</>)}
              </button>
            )}
          </div>
          {puedeAnular && (
            <button id="btn-anular-comanda" onClick={() => setShowModalAnularComanda(true)} disabled={cobrado || enviando}
              className="touch-btn w-full flex items-center justify-center gap-1.5 h-[48px] rounded-[var(--radius-btn)] text-[13px] font-semibold min-h-0 border border-border-default text-danger-500 hover:border-danger-500 disabled:opacity-40 disabled:cursor-not-allowed">
              <Ban className="w-4 h-4" />
              Anular orden
            </button>
          )}
          {usuario && (
            <p className="text-center text-[10px] text-text-muted pt-1 flex items-center justify-center gap-1.5 font-mono">
              <span>Operador: <strong className="font-semibold text-text-secondary">{usuario.nombre}</strong></span>
              <span className="w-1 h-1 rounded-full bg-border-strong" />
              <span className={`px-1 rounded-[3px] font-bold uppercase tracking-wider text-[8px] border ${
                usuario.rol === 'gerente'
                  ? 'text-success-500 border-success-500/40'
                  : 'text-info-500 border-info-500/40'
              }`}>{usuario.rol}</span>
            </p>
          )}
        </div>
      </div>
      {showModalCobro && (
        <ModalCobro
          total={total}
          clienteAsociado={clienteAsociado}
          onConfirmar={handleConfirmarCobro}
          onCancelar={() => setShowModalCobro(false)}
        />
      )}

      {itemAAnular && (
        <ModalAnulacion
          titulo="Anular Ítem"
          descripcion={`Se anulará "${itemAAnular.producto.nombre}" (${itemAAnular.cantidad} un.) ya enviado a cocina.`}
          onConfirmar={handleAnularItem}
          onCancelar={() => setItemAAnular(null)}
        />
      )}

      {showModalAnularComanda && (
        <ModalAnulacion
          titulo="Anular Comanda"
          descripcion="Se anulará la comanda completa y se liberará la mesa."
          onConfirmar={handleAnularComanda}
          onCancelar={() => setShowModalAnularComanda(false)}
        />
      )}

      {ticketModal && (
        <TicketModal
          titulo={ticketModal.titulo}
          contenido={ticketModal.contenido}
          onCerrar={ticketModal.onCerrar}
        />
      )}

      {showSuccessOverlay && (
        <div className="fixed inset-0 z-[100] flex flex-col items-center justify-center animate-modal-backdrop"
          style={{ backgroundColor: 'rgba(15, 23, 42, 0.9)', backdropFilter: 'blur(12px)' }}>
          <div className="flex flex-col items-center gap-4 p-8 rounded-[var(--radius-card)] bg-slate-900 border border-border-strong shadow-[var(--shadow-modal)] max-w-sm mx-4 text-center animate-modal-content">
            <div className="w-14 h-14 rounded-full bg-success-500/10 border border-success-500/25 flex items-center justify-center text-success-500 mb-1">
              <Send className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-text-primary">{successOverlayMessage}</h3>
            <p className="text-sm text-text-muted mt-1">El pedido se registró correctamente.</p>
            <p className="text-xs text-text-muted font-medium mt-3">Redireccionando al inicio de sesión...</p>
          </div>
        </div>
      )}
    </>
  );
}
