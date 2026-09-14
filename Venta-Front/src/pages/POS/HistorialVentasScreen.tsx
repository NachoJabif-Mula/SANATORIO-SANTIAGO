import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ArrowLeft,
  User,
  AlertCircle,
  Printer,
  RefreshCw,
  FileText,
  Edit2
} from 'lucide-react';
import api from '@/services/api';
import { useAuth } from '@/contexts/AppContext';
import { useCaja } from '@/contexts/CajaContext';
import ModalCobro from '@/common/components/POS/ModalCobro';

interface ComandaItemDetail {
  id: string;
  cantidad: number;
  precioUnitario: number;
  productoNombre: string;
}

interface ComandaDetail {
  id: string;
  createdAt: string;
  updatedAt: string;
  fechaContable?: string | null;
  subtotal: number;
  descuento: number;
  total: number;
  estado: number | string; // 0 = Abierta, 1 = Cobrada, 2 = Anulada
  usuario?: { id: string; nombre: string };
  tipoVenta?: { id: string; nombre: string };
  mesa?: { id: string; etiqueta: string };
  clienteId?: string;
  cliente?: { nombre: string; apellido: string };
  items: ComandaItemDetail[];
  pagos: any[];
}

const FILTROS = [
  { key: 'todos', label: 'Todos' },
  { key: 'abierta', label: 'Abiertos' },
  { key: 'cobrada', label: 'Cerrados' },
  { key: 'anulada', label: 'Anulados' }
] as const;

type FiltroKey = typeof FILTROS[number]['key'];

function estadoNormalizado(estado: number | string): 'abierta' | 'cobrada' | 'anulada' {
  const est = typeof estado === 'string' ? estado.toLowerCase() : estado;
  if (est === 1 || est === 'cobrada') return 'cobrada';
  if (est === 2 || est === 'anulada') return 'anulada';
  return 'abierta';
}

export default function HistorialVentasScreen() {
  const navigate = useNavigate();
  const { usuario } = useAuth();
  const { turnoActivo } = useCaja();

  const [comandas, setComandas] = useState<ComandaDetail[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState('');

  const [filtro, setFiltro] = useState<FiltroKey>('todos');
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const [showModalCobroComanda, setShowModalCobroComanda] = useState<ComandaDetail | null>(null);

  const fetchComandas = async () => {
    setLoading(true);
    setErrorMsg('');
    try {
      const res = await api.get('/comanda');
      const data: ComandaDetail[] = res.data || [];
      setComandas(data);
      if (!selectedId) {
        const fechaActual = (turnoActivo?.fechaContable || new Date().toISOString()).slice(0, 10);
        const delDia = data.filter(c => !c.fechaContable || c.fechaContable.slice(0, 10) === fechaActual);
        if (delDia.length > 0) {
          setSelectedId(delDia[0].id);
        }
      }
    } catch (err: any) {
      console.error('Error cargando historial de comandas:', err);
      setErrorMsg('No se pudo cargar el historial de ventas. Verifique la conexión.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchComandas();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const formatARS = (monto: number) =>
    new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(monto);

  const origenDe = (c: ComandaDetail) => {
    if (c.mesa) return `Mesa ${c.mesa.etiqueta}`;
    if (c.clienteId) return c.cliente ? `Cta. cte. ${c.cliente.nombre} ${c.cliente.apellido}` : 'Cuenta corriente';
    return c.tipoVenta?.nombre || 'Mostrador';
  };

  const detalleDe = (c: ComandaDetail) => {
    const mozo = c.usuario ? `Atiende ${c.usuario.nombre}` : '';
    const itemsCount = c.items?.length ? `${c.items.length} ${c.items.length === 1 ? 'ítem' : 'ítems'}` : '';
    return [mozo, itemsCount].filter(Boolean).join(' · ');
  };

  const ESTADO_LABEL: Record<string, string> = { abierta: 'Abierto', cobrada: 'Cerrado', anulada: 'Anulado' };
  const ESTADO_COLOR: Record<string, string> = { abierta: 'var(--acc)', cobrada: 'var(--success-500)', anulada: 'var(--danger-500)' };

  // Sólo pedidos de la fecha contable vigente (la del turno activo, o el día de hoy si
  // no hay turno abierto). Las cuentas corrientes abiertas quedan exentas de turno/fecha
  // contable hasta que se cierran, así que se muestran siempre mientras estén abiertas.
  const fechaContableActual = (turnoActivo?.fechaContable || new Date().toISOString()).slice(0, 10);
  const comandasDelDia = comandas.filter(c =>
    !c.fechaContable || c.fechaContable.slice(0, 10) === fechaContableActual
  );

  const lista = filtro === 'todos' ? comandasDelDia : comandasDelDia.filter(c => estadoNormalizado(c.estado) === filtro);
  const counts: Record<FiltroKey, number> = {
    todos: comandasDelDia.length,
    abierta: comandasDelDia.filter(c => estadoNormalizado(c.estado) === 'abierta').length,
    cobrada: comandasDelDia.filter(c => estadoNormalizado(c.estado) === 'cobrada').length,
    anulada: comandasDelDia.filter(c => estadoNormalizado(c.estado) === 'anulada').length
  };

  const cobradas = comandasDelDia.filter(c => estadoNormalizado(c.estado) === 'cobrada');
  const abiertas = comandasDelDia.filter(c => estadoNormalizado(c.estado) === 'abierta');
  const anuladas = comandasDelDia.filter(c => estadoNormalizado(c.estado) === 'anulada');
  const kpis = [
    { label: 'Vendido en el turno', value: formatARS(cobradas.reduce((a, c) => a + c.total, 0)), sub: `${cobradas.length} pedidos cerrados`, accent: 'var(--acc)' },
    { label: 'Abierto en salón', value: formatARS(abiertas.reduce((a, c) => a + c.total, 0)), sub: `${abiertas.length} pedidos sin cerrar`, accent: 'var(--slate-500)' },
    { label: 'Ticket promedio', value: formatARS(cobradas.length ? Math.round(cobradas.reduce((a, c) => a + c.total, 0) / cobradas.length) : 0), sub: 'Sobre pedidos cerrados', accent: 'var(--success-500)' },
    { label: 'Anulado', value: formatARS(anuladas.reduce((a, c) => a + c.total, 0)), sub: `${anuladas.length} comprobantes anulados`, accent: 'var(--danger-500)' }
  ];

  const sel = comandasDelDia.find(c => c.id === selectedId && (filtro === 'todos' || estadoNormalizado(c.estado) === filtro));
  const selEstado = sel ? estadoNormalizado(sel.estado) : null;

  const handleEditar = (comanda: ComandaDetail) => {
    const mesaQuery = comanda.mesa
      ? `&mesaId=${comanda.mesa.id}&mesaName=${encodeURIComponent(comanda.mesa.etiqueta)}`
      : '';
    navigate(`/pos?comandaId=${comanda.id}${mesaQuery}`);
  };

  const handleCobrarEnElMomento = (comanda: ComandaDetail) => {
    if (!turnoActivo) {
      alert('Debe tener un turno de caja abierto para registrar cobros.');
      return;
    }
    setShowModalCobroComanda(comanda);
  };

  const handleConfirmarCobro = async (comandaId: string, pagos: { metodoPagoId: string; monto: number }[]) => {
    if (!turnoActivo) return;
    try {
      await api.post(`/comanda/${comandaId}/cobrar`, { turnoCajaId: turnoActivo.turnoId, pagos });
      setShowModalCobroComanda(null);
      fetchComandas();
    } catch (err: any) {
      console.error('Error al cobrar comanda desde historial:', err);
      const msg = err.response?.data?.message || 'Error de conexión';
      alert(`No se pudo procesar el cobro: ${msg}`);
      throw err;
    }
  };

  const handleAnular = async (comanda: ComandaDetail) => {
    const confirmacion = window.confirm(`¿Está seguro de que desea ANULAR el pedido ${comanda.id.slice(0, 8).toUpperCase()}?\nEsta acción es irreversible.`);
    if (!confirmacion) return;
    try {
      await api.post(`/comanda/${comanda.id}/anular`);
      fetchComandas();
    } catch (err: any) {
      console.error('Error al anular la comanda:', err);
      const msg = err.response?.data?.message || 'Error de conexión';
      alert(`No se pudo anular el pedido: ${msg}`);
    }
  };

  const handleImprimirNoFiscal = async (comanda: ComandaDetail) => {
    try {
      await api.post(`/comanda/${comanda.id}/imprimir-no-fiscal`);
      alert('✓ Ticket X (No Fiscal) enviado a la cola de impresión.');
    } catch (err: any) {
      console.error('Error al imprimir ticket no fiscal:', err);
      const msg = err.response?.data?.message || 'Error de conexión';
      alert(`No se pudo imprimir el ticket: ${msg}`);
    }
  };

  const actBtn = 'h-[54px] w-full rounded-[var(--radius-btn)] text-[13.5px] font-semibold leading-tight border';

  return (
    <div className="h-screen w-screen flex flex-col bg-slate-950 text-text-primary overflow-hidden">
      {/* Header */}
      <header className="flex items-center gap-3 px-3.5 py-2 min-h-[56px] bg-surface-base border-b border-border-default shrink-0">
        <div className="flex items-center gap-2.5">
          <div className="w-4 h-4 border-2 border-amber-500 rounded-[5px] flex-shrink-0" />
          <span className="text-sm font-semibold tracking-tight text-text-primary">Bares Familia</span>
        </div>

        <div className="flex-1" />

        {usuario && (
          <div className="flex items-center gap-2 px-2.5 py-1 rounded-[var(--radius-btn)] bg-surface-overlay border border-border-default">
            <User className="w-3.5 h-3.5 text-text-muted" />
            <span className="text-xs font-semibold text-text-secondary">{usuario.nombre}</span>
            <span className="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider text-info-500 border border-info-500/30 font-mono">
              {usuario.rol}
            </span>
          </div>
        )}

        <button
          onClick={fetchComandas}
          className="touch-btn h-12 w-12 flex items-center justify-center rounded-[var(--radius-btn)] bg-surface-base border border-border-default text-text-muted hover:border-amber-500 hover:text-amber-500 min-h-0"
          title="Refrescar"
        >
          <RefreshCw className="w-4 h-4" />
        </button>
      </header>

      <main className="flex-1 flex flex-col min-h-0">
        {/* KPIs */}
        <div className="flex-shrink-0 px-4.5 pt-3.5 grid grid-cols-2 md:grid-cols-4 gap-2.5">
          {kpis.map(k => (
            <div key={k.label} className="flex flex-col gap-0.5 p-3 rounded-[var(--radius-card)] bg-surface-base border border-border-default" style={{ borderTop: `2px solid ${k.accent}` }}>
              <span className="mono-label">{k.label}</span>
              <span className="font-mono text-[21px] font-semibold leading-tight tracking-tight">{k.value}</span>
              <span className="text-[11px] text-text-muted truncate">{k.sub}</span>
            </div>
          ))}
        </div>

        {/* Filtros */}
        <div className="flex-shrink-0 flex items-center gap-2 px-4.5 py-3 overflow-x-auto">
          <button
            onClick={() => navigate('/')}
            className="touch-btn h-12 px-3 flex items-center gap-1.5 rounded-[var(--radius-btn)] bg-surface-base text-text-secondary hover:border-amber-500 hover:text-amber-500 border border-border-default text-[12.5px] font-medium min-h-0 flex-shrink-0"
          >
            <ArrowLeft className="w-4 h-4" />
            Mapa
          </button>
          <span className="w-px h-[22px] bg-border-default flex-shrink-0" />
          {FILTROS.map(f => {
            const on = filtro === f.key;
            return (
              <button
                key={f.key}
                onClick={() => setFiltro(f.key)}
                className={`touch-btn h-12 px-4 flex-shrink-0 whitespace-nowrap rounded-[var(--radius-btn)] text-[13.5px] font-semibold min-h-0 border ${
                  on ? 'bg-amber-500 border-amber-500 text-white' : 'bg-surface-base border-border-default text-text-secondary'
                }`}
              >
                {f.label} <span className="opacity-70">{counts[f.key]}</span>
              </button>
            );
          })}
        </div>

        {/* Lista + detalle */}
        <div className="flex-1 flex min-h-0">
          <div className="flex-1 min-h-0 overflow-y-auto px-4.5 pb-4.5 flex flex-col gap-1.5">
            <div className="grid grid-cols-[70px_minmax(0,1fr)_100px_80px_100px] gap-2.5 px-3 pb-1.5 mono-label">
              <span>Nº</span><span>Origen</span><span>Estado</span><span>Hora</span><span className="text-right">Importe</span>
            </div>

            {loading ? (
              <div className="flex-1 flex flex-col items-center justify-center py-16">
                <div className="w-8 h-8 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
              </div>
            ) : errorMsg ? (
              <div className="flex flex-col items-center justify-center p-10 text-center gap-3">
                <AlertCircle className="w-8 h-8 text-danger-500" />
                <p className="text-sm text-text-muted max-w-sm">{errorMsg}</p>
                <button onClick={fetchComandas} className="touch-btn h-11 px-5 rounded-[var(--radius-btn)] bg-amber-500 border border-amber-500 text-white text-sm font-semibold min-h-0">
                  Reintentar
                </button>
              </div>
            ) : lista.length === 0 ? (
              <div className="flex-1 flex flex-col items-center justify-center py-16 text-center gap-2">
                <FileText className="w-8 h-8 text-text-muted opacity-30" />
                <p className="text-sm text-text-muted">No hay pedidos con este filtro.</p>
              </div>
            ) : (
              lista.map(c => {
                const est = estadoNormalizado(c.estado);
                return (
                  <button
                    key={c.id}
                    onClick={() => setSelectedId(c.id)}
                    className="grid grid-cols-[70px_minmax(0,1fr)_100px_80px_100px] gap-2.5 items-center px-3 py-3 rounded-[var(--radius-btn)] text-left bg-surface-base border"
                    style={{ borderColor: c.id === selectedId ? 'var(--acc)' : 'var(--border-default)' }}
                  >
                    <span className="font-mono text-[12.5px] font-semibold">{c.id.slice(0, 6).toUpperCase()}</span>
                    <span className="min-w-0 flex flex-col gap-0.5">
                      <span className="text-[15px] font-semibold truncate">{origenDe(c)}</span>
                      <span className="text-[11px] text-text-muted truncate">{detalleDe(c)}</span>
                    </span>
                    <span className="font-mono text-[10px] font-semibold uppercase tracking-wider" style={{ color: ESTADO_COLOR[est] }}>
                      {ESTADO_LABEL[est]}
                    </span>
                    <span className="font-mono text-[11.5px] text-text-muted">
                      {new Date(c.createdAt).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' })}
                    </span>
                    <span className="font-mono text-[13px] font-semibold text-right">{formatARS(c.total)}</span>
                  </button>
                );
              })
            )}
          </div>

          {sel && (
            <aside className="flex-shrink-0 w-[340px] min-w-[280px] bg-surface-base border-l border-border-default flex flex-col min-h-0">
              <div className="flex-shrink-0 p-4 border-b border-border-default flex flex-col gap-0.5">
                <span className="mono-label">Pedido {sel.id.slice(0, 6).toUpperCase()}</span>
                <span className="text-[15px] font-semibold">{origenDe(sel)}</span>
                <span className="font-mono text-[11px] text-text-muted">
                  {ESTADO_LABEL[selEstado!]} · {new Date(sel.createdAt).toLocaleString('es-AR')}
                </span>
              </div>

              <div className="flex-1 min-h-[96px] overflow-y-auto p-4 flex flex-col gap-2">
                {(sel.items || []).map(l => (
                  <div key={l.id} className="flex items-baseline justify-between gap-2.5">
                    <span className="text-[12.5px] min-w-0 truncate">{l.cantidad} × {l.productoNombre}</span>
                    <span className="font-mono text-[12.5px] font-semibold whitespace-nowrap">{formatARS(l.cantidad * l.precioUnitario)}</span>
                  </div>
                ))}
              </div>

              <div className="flex-shrink-0 border-t border-border-default p-4 flex flex-col gap-2.5">
                <div className="flex items-baseline justify-between">
                  <span className="text-[13px] font-semibold">Total</span>
                  <span className="font-mono text-[22px] font-semibold tracking-tight">{formatARS(sel.total)}</span>
                </div>

                <div className="grid grid-cols-2 gap-2">
                  {selEstado === 'abierta' && (
                    <>
                      <button onClick={() => handleEditar(sel)} className={`${actBtn} bg-amber-500 border-amber-500 text-white col-span-2 flex items-center justify-center gap-1.5`}>
                        <Edit2 className="w-4 h-4" />
                        Abrir en punto de venta
                      </button>
                      <button onClick={() => handleCobrarEnElMomento(sel)} className={`${actBtn} bg-surface-base border-border-default text-text-primary hover:border-success-500 hover:text-success-500`}>
                        Cobrar y cerrar
                      </button>
                      <button onClick={() => handleAnular(sel)} className={`${actBtn} bg-surface-base border-border-default text-danger-500 hover:border-danger-500`}>
                        Anular pedido
                      </button>
                    </>
                  )}
                  {selEstado === 'cobrada' && (
                    <>
                      <button onClick={() => handleImprimirNoFiscal(sel)} className={`${actBtn} bg-amber-500 border-amber-500 text-white col-span-2 flex items-center justify-center gap-1.5`}>
                        <Printer className="w-4 h-4" />
                        Reimprimir comprobante
                      </button>
                      <button onClick={() => handleAnular(sel)} className={`${actBtn} bg-surface-base border-border-default text-danger-500 hover:border-danger-500 col-span-2`}>
                        Anular con autorización
                      </button>
                    </>
                  )}
                  {selEstado === 'anulada' && (
                    <div className="col-span-2 text-center text-[12.5px] text-text-muted py-2">
                      Este pedido fue anulado.
                    </div>
                  )}
                </div>
              </div>
            </aside>
          )}
        </div>
      </main>

      {/* Modal Cobro táctil */}
      {showModalCobroComanda && (
        <ModalCobro
          total={showModalCobroComanda.total}
          onConfirmar={async (pagos) => {
            await handleConfirmarCobro(showModalCobroComanda.id, pagos);
          }}
          onCancelar={() => setShowModalCobroComanda(null)}
        />
      )}
    </div>
  );
}

