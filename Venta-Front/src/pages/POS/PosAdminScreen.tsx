import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  KeyRound, 
  ArrowLeft, 
  User, 
  Lock, 
  Wallet, 
  History, 
  Printer, 
  RefreshCw, 
  X
} from 'lucide-react';
import { useAuth } from '@/contexts/AppContext';
import api from '@/services/api';

interface AdminButtonConfig {
  id: string;
  label: string;
  requiredPermission: string;
  enabled: boolean;
}

interface ComandaDetail {
  id: string;
  createdAt: string;
  subtotal: number;
  descuento: number;
  total: number;
  estado: number; // 0 = Abierta, 1 = Cobrada, 2 = Anulada
  usuarioNombre?: string;
  tipoVentaNombre?: string;
  items: Array<{
    id: string;
    cantidad: number;
    precioUnitario: number;
    productoNombre: string;
  }>;
  pagos: Array<{
    id: string;
    monto: number;
    metodoPagoNombre: string;
  }>;
}

const DEFAULT_ADMIN_BUTTONS: AdminButtonConfig[] = [
  { id: 'cierre-caja', label: 'Cierre de Caja', requiredPermission: 'pos.cierre_caja', enabled: true },
  { id: 'egresos', label: 'Registrar Egreso', requiredPermission: 'pos.egresos', enabled: true },
  { id: 'cuentas-corrientes', label: 'Cuentas Corrientes', requiredPermission: 'pos.cuentas_corrientes', enabled: true },
  { id: 'historial', label: 'Historial de Ventas', requiredPermission: 'pos.vender', enabled: true },
  { id: 'reimprimir', label: 'Reimprimir Último Ticket', requiredPermission: 'pos.vender', enabled: true },
  { id: 'sincronizar', label: 'Sincronización Manual', requiredPermission: 'gerente.override', enabled: true }
];

export default function PosAdminScreen() {
  const { usuario, tienePermiso } = useAuth();
  const navigate = useNavigate();
  
  // Estados de Configuración
  const [buttons, setButtons] = useState<AdminButtonConfig[]>(DEFAULT_ADMIN_BUTTONS);
  const [loadingConfig, setLoadingConfig] = useState(true);

  // Estados de Modales
  const [activeModal, setActiveModal] = useState<'historial' | 'reimprimir' | null>(null);

  // Estado de Historial
  const [loadingComandas, setLoadingComandas] = useState(false);
  const [selectedComanda, setSelectedComanda] = useState<ComandaDetail | null>(null);

  // Cargar configuración de seguridad y botones
  useEffect(() => {
    const loadConfig = async () => {
      try {
        const res = await api.get('/configuracionpos');
        if (res.data && res.data.length > 0) {
          // Buscamos si algún plano tiene la configuración de botones
          for (const cfg of res.data) {
            try {
              const parsed = JSON.parse(cfg.configuracionJson);
              if (parsed.adminPanelButtons && parsed.adminPanelButtons.length > 0) {
                setButtons(parsed.adminPanelButtons);
                break;
              }
            } catch (e) {
              console.error('Error al parsear JSON de plano:', e);
            }
          }
        }
      } catch (err) {
        console.error('Error al cargar la configuración del panel admin:', err);
      } finally {
        setLoadingConfig(false);
      }
    };
    loadConfig();
  }, []);

  // Proteger acceso: Sólo encargado, gerente o administrador
  const esAutorizado = usuario && (usuario.rol === 'encargado' || usuario.rol === 'gerente' || usuario.rol === 'administrador');

  if (!usuario) {
    return (
      <div className="h-screen w-screen flex flex-col items-center justify-center bg-slate-950 text-text-primary px-4 text-center">
        <p className="text-sm text-text-muted">Inicie sesión para acceder al panel de administración.</p>
        <button onClick={() => navigate('/login')} className="touch-btn mt-4 px-6 py-2.5 rounded-[var(--radius-btn)] bg-amber-500 text-slate-950 font-bold">Ir al Login</button>
      </div>
    );
  }

  if (!esAutorizado) {
    return (
      <div className="h-screen w-screen flex flex-col items-center justify-center bg-slate-950 text-text-primary px-4 text-center">
        <Lock className="w-10 h-10 text-danger-400 mb-4" />
        <h3 className="text-lg font-bold">Acceso Denegado</h3>
        <p className="text-sm text-text-muted mt-2 max-w-sm">
          Esta sección es exclusiva para usuarios con rol de Encargado o Administrador.
        </p>
        <button onClick={() => navigate('/')} className="touch-btn mt-6 px-6 py-2.5 rounded-[var(--radius-btn)] bg-slate-900 text-text-primary border border-border-default font-bold hover:bg-slate-850">Volver al Salón</button>
      </div>
    );
  }

  const openReimprimir = async () => {
    setLoadingComandas(true);
    setActiveModal('reimprimir');
    try {
      const res = await api.get('/comanda');
      const sorted = (res.data || []).sort(
        (a: any, b: any) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
      );
      // Seleccionar la comanda más reciente que esté cobrada
      const ultimaCobrada = sorted.find((c: any) => c.estado === 1 || c.estado === 'Cobrada');
      if (ultimaCobrada) {
        setSelectedComanda(ultimaCobrada);
      } else {
        setSelectedComanda(null);
      }
    } catch (err) {
      console.error('Error al obtener última comanda:', err);
    } finally {
      setLoadingComandas(false);
    }
  };

  const formatARS = (monto: number) => {
    return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(monto);
  };

  const handleActionClick = (btn: AdminButtonConfig) => {
    const hasPerm = tienePermiso(btn.requiredPermission);
    if (!hasPerm) return; // Bloqueado

    switch (btn.id) {
      case 'cierre-caja':
        navigate('/cierre-caja');
        break;
      case 'egresos':
        navigate('/egresos');
        break;
      case 'cuentas-corrientes':
        navigate('/cuentas-corrientes');
        break;
      case 'historial':
        navigate('/historial-ventas');
        break;
      case 'reimprimir':
        openReimprimir();
        break;
      case 'sincronizar':
        navigate('/sync');
        break;
    }
  };

  return (
    <div className="h-screen w-screen flex flex-col bg-slate-950 overflow-hidden text-text-primary">
      {/* Header */}
      <header className="flex items-center gap-3 px-3.5 py-2 min-h-[56px] bg-surface-base border-b border-border-default flex-shrink-0">
        <button
          onClick={() => navigate('/')}
          className="touch-btn h-12 px-3 flex items-center gap-1.5 rounded-[var(--radius-btn)] bg-surface-base text-text-primary hover:border-amber-500 hover:text-amber-500 border border-border-default text-[12.5px] font-medium min-h-0"
          title="Volver al Salón"
        >
          <ArrowLeft className="w-4 h-4" />
          Mapa
        </button>
        <h1 className="text-[15px] font-semibold text-text-primary">Funciones de administración</h1>

        <div className="flex-1" />

        <div className="flex items-center gap-2 px-2.5 py-1 rounded-[var(--radius-btn)] bg-surface-overlay border border-border-default">
          <User className="w-3.5 h-3.5 text-text-muted" />
          <span className="text-xs font-semibold text-text-secondary">{usuario.nombre}</span>
          <span className="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider text-info-500 border border-info-500/30 font-mono">
            {usuario.rol}
          </span>
        </div>
      </header>

      {/* Main Workspace */}
      <main className="flex-1 overflow-y-auto p-6 md:p-12 flex flex-col items-center justify-center bg-slate-950 pos-grid-bg relative">
        {loadingConfig ? (
          <div className="flex flex-col items-center">
            <div className="w-9 h-9 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
            <p className="mt-4 text-sm text-text-muted">Cargando permisos del panel...</p>
          </div>
        ) : (
          <div className="w-full max-w-4xl space-y-8 animate-fade-in">
            {/* Grid de Botones */}
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3">
              {buttons.map(btn => {
                if (!btn.enabled) return null;
                const hasPerm = tienePermiso(btn.requiredPermission);

                // Asignar icono dinámico
                const getIcon = () => {
                  switch (btn.id) {
                    case 'cierre-caja': return <Lock className="w-5 h-5" />;
                    case 'egresos': return <Wallet className="w-5 h-5" />;
                    case 'cuentas-corrientes': return <Wallet className="w-5 h-5" />;
                    case 'historial': return <History className="w-5 h-5" />;
                    case 'reimprimir': return <Printer className="w-5 h-5" />;
                    case 'sincronizar': return <RefreshCw className="w-5 h-5" />;
                    default: return <KeyRound className="w-5 h-5" />;
                  }
                };

                return (
                  <button
                    key={btn.id}
                    onClick={() => handleActionClick(btn)}
                    disabled={!hasPerm}
                    className={`touch-btn flex flex-col gap-2.5 items-start justify-center p-4 rounded-[var(--radius-card)] border text-left select-none min-h-[110px]
                      ${hasPerm
                        ? 'bg-surface-base border-border-default hover:border-amber-500 cursor-pointer active:scale-[0.98] text-amber-500'
                        : 'bg-surface-base border-border-subtle text-text-muted cursor-not-allowed opacity-50'}
                    `}
                  >
                    {getIcon()}
                    <span className="text-[13px] font-semibold text-text-primary">{btn.label}</span>

                    {!hasPerm ? (
                      <div className="flex items-center gap-1 text-[10px] font-semibold text-danger-500">
                        <Lock className="w-3 h-3" />
                        Acceso Restringido
                      </div>
                    ) : (
                      <span className="text-[10px] text-text-muted font-mono">Reclama: {btn.requiredPermission}</span>
                    )}
                  </button>
                );
              })}
            </div>
          </div>
        )}
      </main>

      {/* El modal de Historial de Ventas ha sido reemplazado por la vista dedicada /historial-ventas */}

      {/* --- MODAL: REIMPRIMIR ÚLTIMO TICKET --- */}
      {activeModal === 'reimprimir' && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: 'rgba(8, 9, 11, 0.55)' }}>
          <div className="bg-surface-base border border-border-default w-full max-w-md rounded-[14px] flex flex-col shadow-modal overflow-hidden animate-modal-content">
            {/* Header Modal */}
            <div className="flex items-center justify-between p-5 border-b border-border-default shrink-0">
              <div className="flex items-center gap-3">
                <div className="w-9 h-9 bg-slate-850 border border-border-default rounded-[var(--radius-btn)] flex items-center justify-center text-text-secondary">
                  <Printer className="w-4 h-4" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-text-primary">Recibo de Venta</h3>
                  <p className="text-xs text-text-muted">Vista previa de impresión térmica</p>
                </div>
              </div>
              <button
                onClick={() => setActiveModal(null)}
                className="touch-btn p-2 bg-slate-900 hover:bg-slate-850 text-text-secondary hover:text-text-primary border border-border-default rounded-[var(--radius-btn)] cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Receipt Preview */}
            <div className="flex-1 overflow-y-auto p-6 bg-slate-950/40 flex justify-center">
              {loadingComandas ? (
                <div className="flex flex-col items-center py-20">
                  <div className="w-7 h-7 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
                </div>
              ) : !selectedComanda ? (
                <div className="text-center py-12 space-y-3">
                  <Printer className="w-9 h-9 mx-auto text-text-muted opacity-30" />
                  <p className="text-sm text-text-muted">No se encontró ninguna comanda cobrada recientemente para reimprimir.</p>
                </div>
              ) : (
                <div className="w-full bg-white text-slate-900 font-mono p-6 rounded shadow-md border border-slate-200 text-xs space-y-4 max-w-sm">
                  {/* Cabecera Ticket */}
                  <div className="text-center space-y-1">
                    <h4 className="font-extrabold text-sm tracking-widest">BARES FAMILIA</h4>
                    <p className="text-[10px] text-slate-500">Sucursal Central - Bares Familia S.A.</p>
                    <p className="text-[10px] text-slate-500">Av. Sarmiento 1234, Tucumán</p>
                    <p className="text-[10px] text-slate-500">CUIT: 30-71458923-9</p>
                  </div>

                  <div className="border-b border-dashed border-slate-300 py-1" />

                  {/* Metadatos */}
                  <div className="space-y-0.5 text-[10px]">
                    <p>FECHA: {new Date(selectedComanda.createdAt).toLocaleDateString('es-AR')} {new Date(selectedComanda.createdAt).toLocaleTimeString('es-AR')}</p>
                    <p>TICKET NRO: 0001-00047328</p>
                    <p>CAJERO: {selectedComanda.usuarioNombre || 'Administrador'}</p>
                    <p>TIPO VENTA: {selectedComanda.tipoVentaNombre || 'SALON'}</p>
                  </div>

                  <div className="border-b border-dashed border-slate-300 py-1" />

                  {/* Consumos */}
                  <div className="space-y-1">
                    <div className="flex font-bold text-[10px]">
                      <span className="w-8">CANT</span>
                      <span className="flex-1">DESCRIPCIÓN</span>
                      <span className="w-16 text-right">TOTAL</span>
                    </div>
                    <div className="border-b border-dashed border-slate-200" />
                    {selectedComanda.items?.map(it => (
                      <div key={it.id} className="flex text-[10px] py-0.5">
                        <span className="w-8">{it.cantidad}</span>
                        <span className="flex-1 truncate">{it.productoNombre.toUpperCase()}</span>
                        <span className="w-16 text-right">{formatARS(it.cantidad * it.precioUnitario)}</span>
                      </div>
                    ))}
                  </div>

                  <div className="border-b border-dashed border-slate-300 py-1" />

                  {/* Totales */}
                  <div className="space-y-1 text-right">
                    <div className="flex justify-between">
                      <span>SUBTOTAL:</span>
                      <span>{formatARS(selectedComanda.subtotal)}</span>
                    </div>
                    <div className="flex justify-between">
                      <span>DESCUENTO:</span>
                      <span>-{formatARS(selectedComanda.descuento)}</span>
                    </div>
                    <div className="flex justify-between font-extrabold text-sm pt-1 border-t border-dashed border-slate-200">
                      <span>TOTAL NETO:</span>
                      <span>{formatARS(selectedComanda.total)}</span>
                    </div>
                  </div>

                  {/* Medios de Pago */}
                  {selectedComanda.pagos && selectedComanda.pagos.length > 0 && (
                    <div className="pt-2 border-t border-dashed border-slate-300 space-y-0.5 text-[10px]">
                      <p className="font-bold">FORMA DE PAGO:</p>
                      {selectedComanda.pagos.map(p => (
                        <div key={p.id} className="flex justify-between pl-2">
                          <span>* {p.metodoPagoNombre.toUpperCase()}</span>
                          <span>{formatARS(p.monto)}</span>
                        </div>
                      ))}
                    </div>
                  )}

                  <div className="border-b border-dashed border-slate-300 py-1" />

                  {/* AFIP Simulation Footer */}
                  <div className="text-center space-y-1 text-[9px] text-slate-500">
                    <p className="font-bold text-slate-700">COMPROBANTE AUTORIZADO POR AFIP</p>
                    <p>CAE: 74218935624890</p>
                    <p>VTO CAE: {new Date(new Date().setDate(new Date().getDate() + 10)).toLocaleDateString('es-AR')}</p>
                    <div className="flex justify-center py-2">
                      {/* Simulación de código de barras / QR */}
                      <div className="w-32 h-6 bg-slate-300 flex items-center justify-center text-[7px] text-slate-600 tracking-[0.25em]">
                        ||||| ||| |||| || |||| ||||
                      </div>
                    </div>
                    <p className="italic text-[8px]">Gracias por su visita - Bares Familia</p>
                  </div>
                </div>
              )}
            </div>

            {/* Actions Modal */}
            <div className="p-4 border-t border-border-default bg-slate-950/20 flex gap-3 shrink-0">
              <button
                onClick={() => setActiveModal(null)}
                className="touch-btn flex-1 py-3 rounded-[var(--radius-btn)] bg-slate-900 text-text-secondary hover:text-text-primary hover:bg-slate-850 text-sm font-bold border border-border-default cursor-pointer active:scale-95 transition-all text-center"
              >
                Cerrar
              </button>
              <button
                onClick={() => {
                  alert('Ticket enviado a la cola de la Impresora de Caja.');
                  setActiveModal(null);
                }}
                disabled={!selectedComanda}
                className="touch-btn flex-2 py-3 rounded-[var(--radius-btn)] bg-amber-500 disabled:bg-slate-900 disabled:text-text-muted disabled:border-border-default disabled:cursor-not-allowed text-slate-950 hover:bg-amber-400 text-sm font-bold shadow-sm cursor-pointer active:scale-95 transition-all text-center"
              >
                Imprimir Copia
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
