import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  ArrowLeft, 
  History, 
  User, 
  MoreVertical, 
  Edit2, 
  CreditCard, 
  AlertCircle, 
  Printer, 
  Search, 
  RefreshCw, 
  FileText 
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
  producto?: {
    nombre: string;
  };
}

interface ComandaDetail {
  id: string;
  createdAt: string;
  updatedAt: string;
  subtotal: number;
  descuento: number;
  total: number;
  estado: number | string; // 0 = Abierta, 1 = Cobrada, 2 = Anulada
  usuario?: {
    id: string;
    nombre: string;
  };
  tipoVenta?: {
    id: string;
    nombre: string;
  };
  mesa?: {
    id: string;
    etiqueta: string;
  };
  items: ComandaItemDetail[];
  pagos: any[];
}

export default function HistorialVentasScreen() {
  const navigate = useNavigate();
  const { usuario } = useAuth();
  const { turnoActivo } = useCaja();

  // Estados
  const [comandas, setComandas] = useState<ComandaDetail[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState('');
  
  // Filtros
  const [searchTerm, setSearchTerm] = useState('');
  const [estadoFilter, setEstadoFilter] = useState<'todos' | 'abierta' | 'cobrada' | 'anulada'>('todos');

  // Menú de acciones
  const [activeDropdownId, setActiveDropdownId] = useState<string | null>(null);
  const dropdownRef = useRef<HTMLDivElement | null>(null);

  // Cobro en el momento
  const [showModalCobroComanda, setShowModalCobroComanda] = useState<ComandaDetail | null>(null);

  // Recarga en tiempo real
  const [now, setNow] = useState(new Date());

  const fetchComandas = async () => {
    setLoading(true);
    setErrorMsg('');
    try {
      const res = await api.get('/comanda');
      setComandas(res.data || []);
    } catch (err: any) {
      console.error('Error cargando historial de comandas:', err);
      setErrorMsg('No se pudo cargar el historial de ventas. Verifique la conexión.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchComandas();

    // Actualizar tiempo relativo cada 30 segundos
    const timer = setInterval(() => {
      setNow(new Date());
    }, 30000);

    return () => clearInterval(timer);
  }, []);

  // Cerrar dropdown al hacer click afuera
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setActiveDropdownId(null);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const formatARS = (monto: number) => {
    return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(monto);
  };

  const getEstadoLabelAndClass = (estado: number | string) => {
    const est = typeof estado === 'string' ? estado.toLowerCase() : estado;
    if (est === 1 || est === 'cobrada') {
      return {
        label: 'Cobrada',
        badgeClass: 'bg-success-500/10 text-success-400 border-success-500/25'
      };
    }
    if (est === 2 || est === 'anulada') {
      return {
        label: 'Anulada',
        badgeClass: 'bg-danger-500/10 text-danger-400 border-danger-500/25'
      };
    }
    return {
      label: 'Abierta',
      badgeClass: 'bg-amber-500/10 text-amber-400 border-amber-500/25'
    };
  };

  const formatTimeAgo = (createdAtStr: string, estado: number | string) => {
    const est = typeof estado === 'string' ? estado.toLowerCase() : estado;
    const isAbierta = est === 0 || est === 'abierta';
    
    const createdDate = new Date(createdAtStr);
    const diffMs = now.getTime() - createdDate.getTime();
    const diffMins = Math.floor(diffMs / 60000);

    if (isAbierta) {
      if (diffMins < 1) return 'Hace instantes';
      if (diffMins < 60) return `Hace ${diffMins} min`;
      const diffHours = Math.floor(diffMins / 60);
      if (diffHours < 24) return `Hace ${diffHours} h`;
      return createdDate.toLocaleDateString('es-AR');
    } else {
      return createdDate.toLocaleString('es-AR', {
        day: '2-digit',
        month: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      });
    }
  };

  // Acciones de Comanda
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
    setActiveDropdownId(null);
    setShowModalCobroComanda(comanda);
  };

  const handleConfirmarCobro = async (comandaId: string, pagos: { metodoPagoId: string; monto: number }[]) => {
    if (!turnoActivo) return;
    try {
      await api.post(`/comanda/${comandaId}/cobrar`, {
        turnoCajaId: turnoActivo.turnoId,
        pagos
      });
      setShowModalCobroComanda(null);
      alert('✓ Cobro procesado correctamente.');
      fetchComandas();
    } catch (err: any) {
      console.error('Error al cobrar comanda desde historial:', err);
      const msg = err.response?.data?.message || 'Error de conexión';
      alert(`No se pudo procesar el cobro: ${msg}`);
      throw err;
    }
  };

  const handleAnular = async (comanda: ComandaDetail) => {
    setActiveDropdownId(null);
    const confirmacion = window.confirm(`¿Está seguro de que desea ANULAR la comanda ${comanda.id.slice(0, 8).toUpperCase()}?\nEsta acción es irreversible.`);
    if (!confirmacion) return;

    try {
      await api.post(`/comanda/${comanda.id}/anular`);
      alert('✓ Comanda anulada con éxito.');
      fetchComandas();
    } catch (err: any) {
      console.error('Error al anular la comanda:', err);
      const msg = err.response?.data?.message || 'Error de conexión';
      alert(`No se pudo anular la comanda: ${msg}`);
    }
  };

  const handleImprimirNoFiscal = async (comanda: ComandaDetail) => {
    setActiveDropdownId(null);
    try {
      await api.post(`/comanda/${comanda.id}/imprimir-no-fiscal`);
      alert('✓ Ticket X (No Fiscal) enviado a la cola de impresión.');
    } catch (err: any) {
      console.error('Error al imprimir ticket no fiscal:', err);
      const msg = err.response?.data?.message || 'Error de conexión';
      alert(`No se pudo imprimir el ticket: ${msg}`);
    }
  };

  // Filtrado y Búsqueda
  const filteredComandas = comandas.filter(c => {
    // 1. Filtro de búsqueda (ID de comanda, mozo o mesa)
    const idMatch = c.id.toLowerCase().includes(searchTerm.toLowerCase());
    const mozoMatch = c.usuario?.nombre.toLowerCase().includes(searchTerm.toLowerCase());
    const mesaMatch = c.mesa?.etiqueta.toLowerCase().includes(searchTerm.toLowerCase());
    const matchSearch = idMatch || mozoMatch || mesaMatch;

    // 2. Filtro de estado
    const est = typeof c.estado === 'string' ? c.estado.toLowerCase() : c.estado;
    let matchEstado = true;
    if (estadoFilter === 'abierta') {
      matchEstado = est === 0 || est === 'abierta';
    } else if (estadoFilter === 'cobrada') {
      matchEstado = est === 1 || est === 'cobrada';
    } else if (estadoFilter === 'anulada') {
      matchEstado = est === 2 || est === 'anulada';
    }

    return matchSearch && matchEstado;
  });

  return (
    <div className="h-screen w-screen flex flex-col bg-slate-950 text-text-primary overflow-hidden">
      {/* Header */}
      <header className="flex items-center justify-between px-6 py-4 bg-surface-base border-b border-border-default shadow-md shrink-0">
        <div className="flex items-center gap-3">
          <button 
            onClick={() => navigate('/pos-admin')} 
            className="touch-btn p-2 rounded-xl bg-slate-800 text-text-secondary hover:text-text-primary hover:bg-slate-700 border border-border-default transition-all"
            title="Volver al Panel Admin"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div className="h-6 w-[1px] bg-border-default mx-1" />
          <div className="w-9 h-9 bg-slate-850 border border-border-default rounded-[var(--radius-btn)] flex items-center justify-center text-text-secondary">
            <History className="w-4 h-4" />
          </div>
          <div>
            <h1 className="text-base font-bold tracking-tight">Historial de Ventas</h1>
            <p className="text-xs text-text-muted">Consulta, cobra, anula o imprime comprobantes de comandas de la sucursal</p>
          </div>
        </div>

        {usuario && (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-[var(--radius-btn)] bg-slate-900/40 border border-border-default/50">
            <User className="w-4 h-4 text-text-muted" />
            <span className="text-sm font-semibold text-text-secondary">{usuario.nombre}</span>
            <span className="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider bg-info-500/5 text-info-400 border border-info-500/10">
              {usuario.rol}
            </span>
          </div>
        )}
      </header>

      {/* Main Workspace */}
      <main className="flex-1 flex flex-col p-6 overflow-hidden gap-6 bg-slate-900/20">
        
        {/* Barra de Filtros y Búsqueda */}
        <div className="flex flex-col md:flex-row gap-4 items-center justify-between bg-slate-900 border border-border-default rounded-[var(--radius-card)] p-4 shrink-0">
          <div className="w-full md:w-96 relative">
            <Search className="w-4 h-4 text-text-muted absolute left-4 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              placeholder="Buscar por ID, mesa o mozo..."
              value={searchTerm}
              onChange={e => setSearchTerm(e.target.value)}
              className="w-full h-11 bg-slate-950/60 border border-border-strong rounded-[var(--radius-input)] pl-11 pr-4 text-sm font-medium text-text-primary placeholder:text-text-muted focus:outline-none focus:border-amber-500/50"
            />
          </div>

          <div className="flex items-center gap-2 w-full md:w-auto">
            {/* Filtros de Estado */}
            <div className="flex bg-slate-950/60 border border-border-strong rounded-[var(--radius-btn)] p-1 w-full md:w-auto">
              {(['todos', 'abierta', 'cobrada', 'anulada'] as const).map(f => (
                <button
                  key={f}
                  onClick={() => setEstadoFilter(f)}
                  className={`touch-btn flex-1 md:flex-none px-4 py-1.5 rounded-[3px] text-xs font-semibold transition-all uppercase cursor-pointer ${
                    estadoFilter === f
                      ? 'bg-slate-800 text-text-primary border border-border-strong'
                      : 'text-text-secondary hover:text-text-primary hover:bg-slate-900 border border-transparent'
                  }`}
                >
                  {f}
                </button>
              ))}
            </div>

            {/* Botón Refrescar */}
            <button
              onClick={fetchComandas}
              className="touch-btn p-2.5 bg-slate-950/60 border border-border-strong hover:bg-slate-900 text-text-secondary hover:text-text-primary rounded-[var(--radius-btn)] cursor-pointer"
              title="Refrescar Historial"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Tabla / Contenedor principal de datos */}
        <div className="flex-1 bg-slate-900 border border-border-default rounded-[var(--radius-card)] flex flex-col overflow-hidden relative">
          {loading ? (
            <div className="flex-1 flex flex-col items-center justify-center">
              <div className="w-9 h-9 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
              <p className="mt-4 text-sm text-text-muted">Cargando comandas...</p>
            </div>
          ) : errorMsg ? (
            <div className="flex-1 flex flex-col items-center justify-center p-6 text-center">
              <AlertCircle className="w-10 h-10 text-danger-500 mb-4" />
              <h3 className="text-base font-bold">Error de Carga</h3>
              <p className="text-sm text-text-muted mt-2 max-w-sm">{errorMsg}</p>
              <button
                onClick={fetchComandas}
                className="touch-btn mt-6 px-6 py-2.5 rounded-[var(--radius-btn)] bg-amber-500 hover:bg-amber-400 text-slate-950 font-bold transition-all"
              >
                Reintentar
              </button>
            </div>
          ) : filteredComandas.length === 0 ? (
            <div className="flex-1 flex flex-col items-center justify-center text-center p-12">
              <FileText className="w-16 h-16 text-text-muted opacity-20 mb-4" />
              <h3 className="text-base font-bold text-text-secondary">No se encontraron comandas</h3>
              <p className="text-xs text-text-muted mt-1 max-w-xs">Intente cambiar los filtros o realizar otra búsqueda.</p>
            </div>
          ) : (
            <div className="flex-1 overflow-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="border-b border-border-default/60 bg-slate-950/40 text-[10px] font-bold uppercase text-text-muted tracking-wider sticky top-0 z-10">
                    <th className="py-4 px-6">ID / Fecha</th>
                    <th className="py-4 px-6">Empleado</th>
                    <th className="py-4 px-6">Canal de Venta</th>
                    <th className="py-4 px-6">Mesa</th>
                    <th className="py-4 px-6">Tiempo Abierta</th>
                    <th className="py-4 px-6 text-right">Monto</th>
                    <th className="py-4 px-6">Estado</th>
                    <th className="py-4 px-6 text-center">Acciones</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border-default/40 text-sm font-medium">
                  {filteredComandas.map(comanda => {
                    const statusConfig = getEstadoLabelAndClass(comanda.estado);
                    const isAbierta = comanda.estado === 0 || comanda.estado === 'Abierta';
                    const isAnulada = comanda.estado === 2 || comanda.estado === 'Anulada';
                    
                    return (
                      <tr key={comanda.id} className="hover:bg-slate-800/20 transition-all group">
                        {/* ID / Fecha */}
                        <td className="py-4.5 px-6">
                          <span className="font-mono font-bold text-text-primary block tracking-tight">
                            {comanda.id.slice(0, 8).toUpperCase()}
                          </span>
                          <span className="text-[10px] text-text-muted block mt-0.5">
                            {new Date(comanda.createdAt).toLocaleDateString('es-AR')}
                          </span>
                        </td>

                        {/* Empleado */}
                        <td className="py-4.5 px-6 text-text-secondary">
                          {comanda.usuario?.nombre || 'Administrador'}
                        </td>

                        {/* Canal de Venta */}
                        <td className="py-4.5 px-6 text-text-muted font-semibold uppercase text-[10px] tracking-wider">
                          {comanda.tipoVenta?.nombre || 'Salón'}
                        </td>

                        {/* Mesa */}
                        <td className="py-4.5 px-6 text-text-secondary font-semibold">
                          {comanda.mesa?.etiqueta || '—'}
                        </td>

                        {/* Tiempo Abierta */}
                        <td className="py-4.5 px-6 text-text-muted">
                          {formatTimeAgo(comanda.createdAt, comanda.estado)}
                        </td>

                        {/* Monto */}
                        <td className="py-4.5 px-6 text-right font-bold text-text-primary tabular-nums">
                          {formatARS(comanda.total)}
                        </td>

                        {/* Estado */}
                        <td className="py-4.5 px-6">
                          <span className={`px-2 py-1 rounded-[3px] text-[10px] font-bold uppercase border ${statusConfig.badgeClass}`}>
                            {statusConfig.label}
                          </span>
                        </td>

                        {/* Acciones */}
                        <td className="py-4.5 px-6 text-center relative">
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              setActiveDropdownId(activeDropdownId === comanda.id ? null : comanda.id);
                            }}
                            className="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-text-secondary hover:text-text-primary border border-border-default/50 cursor-pointer active:scale-95 transition-all inline-flex items-center"
                          >
                            <MoreVertical className="w-4.5 h-4.5" />
                          </button>

                          {/* Dropdown Menu */}
                          {activeDropdownId === comanda.id && (
                            <div 
                              ref={dropdownRef}
                              className="absolute right-12 top-1/2 -translate-y-1/2 z-50 w-52 rounded-[var(--radius-card)] bg-slate-900 border border-border-strong shadow-[var(--shadow-modal)] p-1.5 flex flex-col gap-0.5 text-left animate-fade-in"
                            >
                              {/* Editar */}
                              <button
                                onClick={() => handleEditar(comanda)}
                                disabled={!isAbierta}
                                className={`w-full py-2.5 px-3 rounded-lg text-xs font-bold flex items-center gap-2.5 transition-all
                                  ${isAbierta 
                                    ? 'text-text-primary hover:bg-slate-800 hover:text-amber-400 cursor-pointer' 
                                    : 'text-text-muted opacity-40 cursor-not-allowed'}
                                `}
                              >
                                <Edit2 className="w-4 h-4" />
                                Editar Orden
                              </button>

                              {/* Cobrar */}
                              <button
                                onClick={() => handleCobrarEnElMomento(comanda)}
                                disabled={!isAbierta}
                                className={`w-full py-2.5 px-3 rounded-lg text-xs font-bold flex items-center gap-2.5 transition-all
                                  ${isAbierta 
                                    ? 'text-success-400 hover:bg-success-500/10 cursor-pointer' 
                                    : 'text-text-muted opacity-40 cursor-not-allowed'}
                                `}
                              >
                                <CreditCard className="w-4 h-4" />
                                Cobrar al Instante
                              </button>

                              {/* Anular */}
                              <button
                                onClick={() => handleAnular(comanda)}
                                disabled={!isAbierta || isAnulada}
                                className={`w-full py-2.5 px-3 rounded-lg text-xs font-bold flex items-center gap-2.5 transition-all
                                  ${isAbierta && !isAnulada 
                                    ? 'text-danger-400 hover:bg-danger-500/10 cursor-pointer' 
                                    : 'text-text-muted opacity-40 cursor-not-allowed'}
                                `}
                              >
                                <AlertCircle className="w-4 h-4" />
                                Anular Comanda
                              </button>

                              <div className="h-[1px] bg-border-default/50 my-1" />

                              {/* Imprimir Ticket X */}
                              <button
                                onClick={() => handleImprimirNoFiscal(comanda)}
                                className="w-full py-2.5 px-3 rounded-lg text-xs font-bold flex items-center gap-2.5 text-sky-400 hover:bg-sky-500/10 transition-all cursor-pointer"
                              >
                                <Printer className="w-4 h-4" />
                                Ticket X (No Fiscal)
                              </button>
                            </div>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
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
