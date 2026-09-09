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
  const [activeModal, setActiveModal] = useState<'historial' | 'reimprimir' | 'sincronizar' | null>(null);
  
  // Estado de Historial
  const [loadingComandas, setLoadingComandas] = useState(false);
  const [selectedComanda, setSelectedComanda] = useState<ComandaDetail | null>(null);

  // Estado de Sincronización
  const [syncStep, setSyncStep] = useState<'idle' | 'running' | 'success' | 'error'>('idle');
  const [syncLogs, setSyncLogs] = useState<Array<{ id: string; timestamp: string; tipo: string; mensaje: string; exitoso: boolean }>>([]);
  const [syncPollingInterval, setSyncPollingInterval] = useState<any>(null);

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

  const runSync = async () => {
    setSyncStep('running');
    setSyncLogs([]);
    
    try {
      await api.post('/sync/run');
      
      const logsRes = await api.get('/sync/logs');
      setSyncLogs(logsRes.data || []);
      
      const interval = setInterval(async () => {
        try {
          const res = await api.get('/sync/logs');
          const currentLogs = res.data || [];
          setSyncLogs(currentLogs);
          
          const hasSuccess = currentLogs.some((l: any) => 
            l.tipo === 'PULL' && l.exitoso && l.mensaje.includes('completada con éxito')
          );
          const hasWarning = currentLogs.some((l: any) => 
            l.tipo === 'PULL' && !l.exitoso && l.mensaje.includes('finalizada con advertencias')
          );
          const hasError = currentLogs.some((l: any) => 
            l.tipo === 'ERROR' || (l.tipo === 'PULL' && !l.exitoso && l.mensaje.includes('Error general'))
          );
          
          if (hasSuccess || hasWarning) {
            setSyncStep('success');
            clearInterval(interval);
          } else if (hasError) {
            setSyncStep('error');
            clearInterval(interval);
          }
        } catch (err) {
          console.error('Error polling sync logs:', err);
        }
      }, 1500);
      
      setSyncPollingInterval(interval);
    } catch (err: any) {
      console.error('Error starting sync:', err);
      setSyncStep('error');
      const errorMsg = err.response?.data?.message || 'Error al conectar con la API Local.';
      setSyncLogs(prev => [
        {
          id: 'err-local',
          timestamp: new Date().toISOString(),
          tipo: 'ERROR',
          mensaje: `❌ Error al iniciar sync: ${errorMsg}`,
          exitoso: false
        },
        ...prev
      ]);
    }
  };

  useEffect(() => {
    return () => {
      if (syncPollingInterval) clearInterval(syncPollingInterval);
    };
  }, [syncPollingInterval]);

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
        setSyncStep('idle');
        setActiveModal('sincronizar');
        break;
    }
  };

  return (
    <div className="h-screen w-screen flex flex-col bg-slate-950 overflow-hidden text-text-primary">
      {/* Header */}
      <header className="relative z-10 flex items-center justify-between px-6 py-4 bg-surface-base border-b border-border-default flex-shrink-0 shadow-md">
        <div className="flex items-center gap-3">
          <button 
            onClick={() => navigate('/')} 
            className="touch-btn p-2 rounded-xl bg-slate-800 text-text-secondary hover:text-text-primary hover:bg-slate-700 border border-border-default transition-all"
            title="Volver al Salón"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div className="h-6 w-[1px] bg-border-default mx-1" />
          <h1 className="text-base font-bold tracking-tight">
            Panel de Operaciones Admin
          </h1>
          <span className="text-[9px] font-semibold uppercase tracking-wider bg-slate-850 text-text-muted px-2 py-0.5 rounded-[3px] border border-border-default/50">
            Seguro
          </span>
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-[var(--radius-btn)] bg-slate-900/40 border border-border-default/50">
            <User className="w-4 h-4 text-text-muted" />
            <span className="text-sm font-semibold text-text-secondary">{usuario.nombre}</span>
            <span className="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider bg-info-500/5 text-info-400 border border-info-500/10">
              {usuario.rol}
            </span>
          </div>
        </div>
      </header>

      {/* Main Workspace */}
      <main className="flex-1 overflow-y-auto p-6 md:p-12 flex flex-col items-center justify-center bg-slate-900/40 relative">
        {loadingConfig ? (
          <div className="flex flex-col items-center">
            <div className="w-9 h-9 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
            <p className="mt-4 text-sm text-text-muted">Cargando permisos del panel...</p>
          </div>
        ) : (
          <div className="w-full max-w-4xl space-y-8 animate-fade-in">
            <div className="text-center">
              <h2 className="text-lg md:text-xl font-bold text-text-primary flex items-center justify-center gap-2.5">
                <KeyRound className="w-5 h-5 text-amber-500" />
                Acciones Especiales y Caja
              </h2>
              <p className="text-sm text-text-muted mt-2">Accede a las opciones autorizadas para tu perfil de encargado.</p>
            </div>

            {/* Grid de Botones */}
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
              {buttons.map(btn => {
                if (!btn.enabled) return null;
                const hasPerm = tienePermiso(btn.requiredPermission);
                
                // Asignar icono dinámico
                const getIcon = () => {
                  switch (btn.id) {
                    case 'cierre-caja': return <Lock className="w-7 h-7" />;
                    case 'egresos': return <Wallet className="w-7 h-7" />;
                    case 'cuentas-corrientes': return <Wallet className="w-7 h-7" />;
                    case 'historial': return <History className="w-7 h-7" />;
                    case 'reimprimir': return <Printer className="w-7 h-7" />;
                    case 'sincronizar': return <RefreshCw className="w-7 h-7" />;
                    default: return <KeyRound className="w-7 h-7" />;
                  }
                };

                return (
                  <button
                    key={btn.id}
                    onClick={() => handleActionClick(btn)}
                    disabled={!hasPerm}
                    className={`touch-btn flex flex-col items-center justify-center p-8 rounded-[var(--radius-card)] border transition-all duration-150 text-center relative group select-none min-h-[190px]
                      ${hasPerm
                        ? 'bg-slate-900/50 border-border-default hover:border-border-strong hover:bg-slate-850 cursor-pointer active:scale-[0.98]'
                        : 'bg-slate-950/40 border-border-subtle/50 text-text-muted cursor-not-allowed opacity-50'}
                    `}
                  >
                    {/* Icono Principal */}
                    <div className={`w-12 h-12 rounded-[var(--radius-btn)] flex items-center justify-center mb-4
                      ${hasPerm
                        ? 'bg-slate-850 border border-border-default text-amber-500'
                        : 'bg-slate-950 text-text-muted border border-border-subtle'}
                    `}>
                      {getIcon()}
                    </div>

                    {/* Texto y Estado */}
                    <span className="text-sm font-bold text-text-primary">{btn.label}</span>

                    {/* Indicador de Restricción */}
                    {!hasPerm ? (
                      <div className="mt-3 flex items-center justify-center gap-1 text-[10px] font-semibold text-danger-400 bg-danger-500/5 px-2.5 py-1 rounded-[3px] border border-danger-500/15">
                        <Lock className="w-3 h-3" />
                        Acceso Restringido
                      </div>
                    ) : (
                      <span className="text-[10px] text-text-muted mt-2 block font-mono">Reclama: {btn.requiredPermission}</span>
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
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: 'rgba(0,0,0,0.85)', backdropFilter: 'blur(8px)' }}>
          <div className="bg-slate-900 border border-border-strong w-full max-w-md rounded-[var(--radius-card)] flex flex-col shadow-[var(--shadow-modal)] overflow-hidden animate-modal-content">
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

      {/* --- MODAL: SINCRONIZACIÓN MANUAL --- */}
      {activeModal === 'sincronizar' && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: 'rgba(0,0,0,0.85)', backdropFilter: 'blur(8px)' }}>
          <div className="bg-slate-900 border border-border-strong w-full max-w-lg rounded-[var(--radius-card)] p-6 shadow-[var(--shadow-modal)] animate-modal-content">
            <div className="flex flex-col items-center text-center space-y-5">

              {/* Icono de Sincronización */}
              <div className={`w-12 h-12 rounded-[var(--radius-btn)] border flex items-center justify-center
                ${syncStep === 'success' ? 'bg-success-500/10 border-success-500/25 text-success-500' :
                  syncStep === 'error' ? 'bg-danger-500/10 border-danger-500/25 text-danger-400' : 'bg-slate-850 border-border-default text-amber-500'}
              `}>
                <RefreshCw className={`w-6 h-6 ${syncStep === 'running' ? 'animate-spin' : ''}`} />
              </div>

              {/* Título y Mensaje */}
              <div className="space-y-1.5">
                <h3 className="text-base font-bold text-text-primary">
                  {syncStep === 'idle' && 'Forzar Sincronización'}
                  {syncStep === 'running' && 'Sincronizando con Nube...'}
                  {syncStep === 'success' && 'Sincronización Exitosa'}
                  {syncStep === 'error' && 'Sincronización Fallida'}
                </h3>
                <p className="text-xs text-text-muted max-w-xs leading-relaxed">
                  {syncStep === 'idle' && 'Esto iniciará un ciclo de sincronización manual de catálogos y transacciones desde este POS local.'}
                  {syncStep === 'running' && 'Ejecutando transferencia bidireccional de datos en tiempo real...'}
                  {syncStep === 'success' && 'Todas las comandas locales han sido enviadas y los catálogos del Backoffice están actualizados.'}
                  {syncStep === 'error' && 'Hubo un inconveniente al conectar con la Nube o procesar los datos de sincronización.'}
                </p>
              </div>

              {/* Progreso Visual y Logs */}
              {syncStep !== 'idle' && (
                <div className="w-full space-y-3">
                  <div className="flex items-center justify-between text-xs text-text-muted">
                    <span className="font-semibold uppercase tracking-wider text-[10px]">Detalle del Proceso:</span>
                    {syncStep === 'running' && (
                      <span className="flex items-center gap-1.5 text-amber-500 font-bold">
                        <RefreshCw size={11} className="animate-spin" />
                        Sincronizando...
                      </span>
                    )}
                    {syncStep === 'success' && <span className="text-success-400 font-bold">Completado</span>}
                    {syncStep === 'error' && <span className="text-danger-400 font-bold">Error de Sync</span>}
                  </div>

                  {/* Contenedor de Logs estilo terminal */}
                  <div className="w-full h-48 bg-slate-950 rounded-[var(--radius-btn)] border border-border-default/50 p-3 overflow-y-auto text-left font-mono text-[10px] leading-relaxed space-y-1.5 scrollbar-thin">
                    {syncLogs.length === 0 ? (
                      <div className="text-text-muted italic">Iniciando conexión local...</div>
                    ) : (
                      syncLogs.map((log) => {
                        let colorClass = 'text-text-muted';
                        if (log.tipo === 'ERROR') colorClass = 'text-danger-400 font-bold';
                        else if (log.tipo === 'PUSH') colorClass = 'text-amber-400';
                        else if (log.tipo === 'PULL') colorClass = 'text-success-400';
                        else if (log.tipo === 'CONFIG') colorClass = 'text-cyan-400';

                        const timeStr = new Date(log.timestamp).toLocaleTimeString('es-AR', {
                          hour: '2-digit',
                          minute: '2-digit',
                          second: '2-digit'
                        });

                        return (
                          <div key={log.id} className="flex gap-2 items-start hover:bg-slate-900/60 py-0.5 rounded px-1">
                            <span className="text-text-muted font-medium shrink-0">{timeStr}</span>
                            <span className={`${colorClass} break-words`}>{log.mensaje}</span>
                          </div>
                        );
                      })
                    )}
                  </div>
                </div>
              )}

              {/* Botones de Acción */}
              <div className="w-full flex gap-3 pt-3">
                {syncStep === 'idle' && (
                  <>
                    <button
                      onClick={() => setActiveModal(null)}
                      className="touch-btn flex-1 py-3 rounded-[var(--radius-btn)] bg-slate-900 text-text-secondary hover:text-text-primary hover:bg-slate-850 text-sm font-bold border border-border-default cursor-pointer active:scale-95 transition-all text-center"
                    >
                      Cancelar
                    </button>
                    <button
                      onClick={runSync}
                      className="touch-btn flex-1 py-3 rounded-[var(--radius-btn)] bg-amber-500 text-slate-950 hover:bg-amber-400 text-sm font-bold shadow-sm cursor-pointer active:scale-95 transition-all text-center"
                    >
                      Sincronizar Ahora
                    </button>
                  </>
                )}
                {syncStep === 'running' && (
                  <button
                    disabled
                    className="w-full py-3 rounded-[var(--radius-btn)] bg-slate-900 text-text-muted text-sm font-bold border border-border-default cursor-not-allowed text-center"
                  >
                    Procesando, por favor espere...
                  </button>
                )}
                {(syncStep === 'success' || syncStep === 'error') && (
                  <button
                    onClick={() => {
                      if (syncPollingInterval) clearInterval(syncPollingInterval);
                      setActiveModal(null);
                    }}
                    className={`touch-btn w-full py-3 rounded-[var(--radius-btn)] text-white text-sm font-bold shadow-sm cursor-pointer active:scale-95 transition-all text-center ${
                      syncStep === 'success' ? 'bg-success-600 hover:bg-success-500' : 'bg-danger-600 hover:bg-danger-500'
                    }`}
                  >
                    Cerrar
                  </button>
                )}
              </div>

            </div>
          </div>
        </div>
      )}
    </div>
  );
}
