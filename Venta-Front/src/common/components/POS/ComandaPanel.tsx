import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Plus, Minus, Trash2, ShoppingCart, CreditCard, Receipt, X, Send, Ban } from 'lucide-react';
import { useComanda, useAuth } from '@/contexts/AppContext';
import { useCaja } from '@/contexts/CajaContext';
import type { ItemComanda } from '@/common/types';
import api from '@/services/api';
import ModalCobro from '@/common/components/POS/ModalCobro';
import { ModalAnulacion } from '@/common/components/POS/ModalAnulacion';

function formatARS(monto: number): string {
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(monto);
}

const isGuid = (val: string | null | undefined) =>
  val ? /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(val) : false;

export default function ComandaPanel() {
  const { items, comandaId, total, cantidadItems, incrementar, decrementar, quitarItem, limpiarComanda, marcarItemCancelado } = useComanda();
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

      if (comandaId) {
        await api.put(`/comanda/${comandaId}`, {
          usuarioId: usuario.id,
          subtotal: total,
          descuento: 0,
          total: total,
          items: mappedItems
        });
      } else {
        await api.post('/comanda', {
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

      procesarCobro('¡Pedido Enviado a Cocina!');
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
      procesarCobro(cobrarRes.data.facturaAfipEmitida ? '¡Comprobante AFIP Emitido!' : '¡Cobro Registrado!');
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
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-[var(--radius-btn)] bg-slate-850 border border-border-default flex items-center justify-center">
              <Receipt className="w-4 h-4 text-text-secondary" />
            </div>
            <div>
              <h2 className="text-sm font-bold text-text-primary tracking-tight">Comanda</h2>
              <p className="text-xs text-text-muted">{cantidadItems} {cantidadItems === 1 ? 'ítem' : 'ítems'}</p>
            </div>
          </div>
          {items.length > 0 && (
            <button id="btn-limpiar-comanda" onClick={limpiarComanda}
              className="touch-btn p-2.5 rounded-[var(--radius-btn)] bg-slate-900 text-text-muted hover:text-danger-400 hover:bg-danger-500/5 border border-border-default transition-all duration-200"
              title="Limpiar comanda">
              <X className="w-4 h-4" />
            </button>
          )}
        </div>

        {/* Items */}
        <div className="flex-1 overflow-y-auto p-4 space-y-1">
          {items.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-text-muted">
              <ShoppingCart className="w-10 h-10 mb-3 opacity-15" />
              <p className="text-sm font-semibold text-text-secondary">Comanda vacía</p>
              <p className="text-xs mt-0.5 text-center text-text-muted/80">Seleccione productos del catálogo</p>
            </div>
          ) : (
            items.map((item: ItemComanda) => (
              <div key={item.id} className={`flex items-center gap-3 py-2.5 border-b animate-fade-in ${
                item.cancelado ? 'border-danger-500/20 opacity-70' : 'border-border-default/40'
              }`}>
                <div className="flex-1 min-w-0">
                  <p className={`text-sm font-semibold truncate ${item.cancelado ? 'text-danger-400 line-through' : 'text-text-primary'}`}>
                    {item.producto.nombre}
                  </p>
                  {item.cancelado ? (
                    <p className="text-[11px] text-danger-400 font-bold uppercase tracking-wider mt-0.5">Anulado</p>
                  ) : (
                    <p className="text-[11px] text-text-muted mt-0.5">{formatARS(item.producto.precio)} c/u</p>
                  )}
                </div>

                {item.cancelado ? (
                  <span className="w-6 text-center text-xs font-bold text-danger-400 tabular-nums">{item.cantidad}</span>
                ) : (
                  /* Controles de Cantidad +/- */
                  <div className="flex items-center gap-1 bg-slate-950/40 p-0.5 rounded-md border border-border-subtle/50">
                    <button id={`btn-dec-${item.id}`} onClick={() => decrementar(item.id)}
                      className="touch-btn w-6 h-6 rounded bg-slate-900 text-text-secondary hover:text-text-primary hover:bg-slate-800 flex items-center justify-center min-h-[24px] min-w-[24px] border border-border-default/40 active:scale-90 transition-all">
                      <Minus className="w-3.5 h-3.5" />
                    </button>
                    <span className="w-6 text-center text-xs font-bold text-text-primary tabular-nums">{item.cantidad}</span>
                    <button id={`btn-inc-${item.id}`} onClick={() => incrementar(item.id)}
                      className="touch-btn w-6 h-6 rounded bg-slate-900 text-text-secondary hover:text-text-primary hover:bg-slate-800 flex items-center justify-center min-h-[24px] min-w-[24px] border border-border-default/40 active:scale-90 transition-all">
                      <Plus className="w-3.5 h-3.5" />
                    </button>
                  </div>
                )}

                <span className={`text-xs font-bold w-16 text-right tabular-nums ${item.cancelado ? 'text-danger-400 line-through' : 'text-text-primary'}`}>
                  {formatARS(item.subtotal)}
                </span>

                {item.cancelado ? (
                  <span className="w-7 h-7 flex items-center justify-center" title="Ítem anulado">
                    <Ban className="w-3.5 h-3.5 text-danger-400" />
                  </span>
                ) : isGuid(item.id) && comandaId ? (
                  puedeAnular && (
                    <button id={`btn-anular-${item.id}`} onClick={() => setItemAAnular(item)}
                      title="Anular ítem (ya comandado)"
                      className="touch-btn w-7 h-7 rounded text-text-muted hover:text-danger-500 hover:bg-danger-500/5 flex items-center justify-center min-h-[28px] min-w-[28px] transition-colors duration-150">
                      <Ban className="w-3.5 h-3.5" />
                    </button>
                  )
                ) : (
                  <button id={`btn-del-${item.id}`} onClick={() => quitarItem(item.id)}
                    className="touch-btn w-7 h-7 rounded text-text-muted hover:text-danger-500 hover:bg-danger-500/5 flex items-center justify-center min-h-[28px] min-w-[28px] transition-colors duration-150">
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                )}
              </div>
            ))
          )}
        </div>

        {/* Footer */}
        <div className="border-t border-border-default p-4 bg-slate-950/20 space-y-3">
          <div className="flex items-center justify-between py-1">
            <span className="text-xs font-semibold uppercase tracking-wide text-text-muted">Total a Pagar</span>
            <span className="text-2xl font-bold text-text-primary tabular-nums">{formatARS(total)}</span>
          </div>

          <div className={`grid ${usuario && (usuario.rol === 'mozo' || usuario.rol === 'moso') ? 'grid-cols-1' : 'grid-cols-2'} gap-2 mt-1`}>
            <button id="btn-enviar" onClick={handleEnviar} disabled={items.length === 0 || cobrado || enviando}
              className={`touch-btn flex items-center justify-center gap-1.5 py-3 rounded-[var(--radius-btn)] text-sm font-bold transition-all duration-150 min-h-[46px] border ${
                items.length === 0 || cobrado || enviando
                  ? 'bg-slate-900/30 text-text-muted border-border-default/30 cursor-not-allowed'
                  : 'bg-slate-900 text-text-secondary hover:bg-slate-850 hover:text-text-primary border-border-default active:scale-[0.98]'
              }`}>
              <Send className="w-4 h-4" />
              {enviando ? 'Enviando...' : 'Enviar'}
            </button>

            {!(usuario && (usuario.rol === 'mozo' || usuario.rol === 'moso')) && (
              <button id="btn-cobrar" onClick={handleCobrar} disabled={items.length === 0 || cobrado || enviando}
                className={`touch-btn flex items-center justify-center gap-2 py-3 rounded-[var(--radius-btn)] text-sm font-bold transition-all duration-150 min-h-[46px] ${
                  cobrado
                    ? 'bg-success-600 text-white col-span-2 shadow-sm'
                    : items.length === 0 || enviando
                    ? 'bg-slate-900/30 text-text-muted border border-border-default/30 cursor-not-allowed'
                    : 'bg-amber-500 text-slate-950 hover:bg-amber-400 shadow-sm active:scale-[0.98]'
                }`}>
                {cobrado ? (<><Receipt className="w-5 h-5" />{mensajeCobro}</>) : (<><CreditCard className="w-5 h-5" />Cobrar</>)}
              </button>
            )}
          </div>
          {puedeAnular && (
            <button id="btn-anular-comanda" onClick={() => setShowModalAnularComanda(true)} disabled={cobrado || enviando}
              className="touch-btn w-full flex items-center justify-center gap-1.5 py-2.5 rounded-[var(--radius-btn)] text-xs font-bold transition-all duration-150 min-h-[38px] border border-danger-500/30 text-danger-400 hover:bg-danger-500/10 disabled:opacity-40 disabled:cursor-not-allowed">
              <Ban className="w-4 h-4" />
              Anular Comanda
            </button>
          )}
          {usuario && (
            <p className="text-center text-[10px] text-text-muted pt-1 flex items-center justify-center gap-1.5">
              <span>Operador: <strong className="font-semibold text-text-secondary">{usuario.nombre}</strong></span>
              <span className="w-1 h-1 rounded-full bg-slate-700" />
              <span className={`px-1 rounded-[3px] font-bold uppercase tracking-wider text-[8px] border ${
                usuario.rol === 'gerente' 
                  ? 'bg-success-500/5 text-success-400 border-success-500/20' 
                  : 'bg-info-500/5 text-info-400 border-info-500/20'
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
