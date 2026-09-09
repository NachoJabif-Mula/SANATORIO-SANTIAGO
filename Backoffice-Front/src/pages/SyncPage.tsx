import { useState, useEffect } from 'react';
import { 
  RefreshCw, 
  UploadCloud, 
  DownloadCloud, 
  CheckCircle2, 
  Clock, 
  Database, 
  Building,
  AlertCircle,
  Activity,
  FileText,
  X
} from 'lucide-react';
import api from '@/services/api';

interface SyncStats {
  sucursalId: string;
  sucursalNombre: string;
  comandasCount: number;
  pagosCount: number;
  movimientosCount: number;
  mesasCount: number;
  configuracionesCount: number;
  usuariosCount: number;
  rolesCount: number;
  lastPushComandas: string | null;
  lastPushPagos: string | null;
  lastPushMovimientos: string | null;
  lastPullConfig: string | null;
  lastPullMesas: string | null;
  lastPullRoles: string | null;
  lastPullUsuarios: string | null;
  forceSyncPending: boolean;
  syncIntervalSeconds: number;
  connectedPosCount: number;
}

interface SyncLog {
  id: string;
  sucursalId: string;
  sucursalNombre: string;
  timestamp: string;
  tipo: string;
  mensaje: string;
  exitoso: boolean;
}

export default function SyncPage() {
  const [stats, setStats] = useState<SyncStats[]>([]);
  const [logs, setLogs] = useState<SyncLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingLogs, setLoadingLogs] = useState(true);
  const [forcingId, setForcingId] = useState<string | null>(null);
  const [updatingIntervalId, setUpdatingIntervalId] = useState<string | null>(null);

  // Estados de Sincronización en Vivo
  const [activeSyncModal, setActiveSyncModal] = useState<{ sucursalId: string; sucursalNombre: string } | null>(null);
  const [syncModalLogs, setSyncModalLogs] = useState<SyncLog[]>([]);
  const [modalPollingInterval, setModalPollingInterval] = useState<any>(null);
  const [syncStatus, setSyncStatus] = useState<'waiting' | 'running' | 'success' | 'error'>('waiting');
  const [errorMessage, setErrorMessage] = useState('');
  const [selectedSucursalId, setSelectedSucursalId] = useState<string>('');

  const loadStats = async () => {
    setLoading(true);
    try {
      const res = await api.get('/sync/stats');
      setStats(res.data || []);
    } catch (err) {
      console.error('Error al obtener estadísticas de sync:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadLogs = async () => {
    try {
      const res = await api.get('/sync/logs');
      setLogs(res.data || []);
    } catch (err) {
      console.error('Error al obtener logs de sync:', err);
    } finally {
      setLoadingLogs(false);
    }
  };

  useEffect(() => {
    loadStats();
    loadLogs();
    // Poll stats and logs every 10 seconds to see live updates
    const interval = setInterval(() => {
      loadStats();
      loadLogs();
    }, 10000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    if (stats.length > 0 && !selectedSucursalId) {
      setSelectedSucursalId(stats[0].sucursalId);
    }
  }, [stats, selectedSucursalId]);

  async function handleForceSync(sucursalId: string, sucursalNombre: string) {
    setForcingId(sucursalId);
    setSyncStatus('waiting');
    setErrorMessage('');
    setSyncModalLogs([]);
    setActiveSyncModal({ sucursalId, sucursalNombre });
    
    try {
      await api.post(`/sync/force/${sucursalId}`);
      
      const logsRes = await api.get('/sync/logs');
      const filteredBaseline = (logsRes.data || []).filter((l: any) => l.sucursalId === sucursalId);
      setSyncModalLogs(filteredBaseline);
      
      let secondsElapsed = 0;
      const interval = setInterval(async () => {
        secondsElapsed += 1.5;
        try {
          const res = await api.get('/sync/logs');
          const allLogs = res.data || [];
          const sucursalLogs = allLogs.filter((l: any) => l.sucursalId === sucursalId);
          setSyncModalLogs(sucursalLogs);
          
          const errorLog = sucursalLogs.find((l: any) => 
            (l.tipo === 'ERROR' || (l.tipo === 'PULL' && !l.exitoso) || (l.tipo === 'PUSH' && !l.exitoso)) &&
            new Date(l.timestamp).getTime() > Date.now() - 30000
          );
          const hasSuccess = sucursalLogs.some((l: any) => 
            l.tipo === 'PULL' && l.exitoso &&
            new Date(l.timestamp).getTime() > Date.now() - 30000
          );
          const hasPullStarting = sucursalLogs.some((l: any) => 
            l.tipo === 'PULL' && l.mensaje.includes('Iniciando descarga') &&
            new Date(l.timestamp).getTime() > Date.now() - 30000
          );
          
          if (errorLog) {
            setSyncStatus('error');
            setErrorMessage(errorLog.mensaje);
            clearInterval(interval);
          } else if (hasSuccess) {
            setSyncStatus('success');
            loadStats();
            clearInterval(interval);
          } else if (hasPullStarting || sucursalLogs.length > filteredBaseline.length) {
            setSyncStatus('running');
          } else if (secondsElapsed > 45) {
            setSyncStatus('error');
            setErrorMessage('La terminal no respondió a la solicitud de sincronización (Timeout).');
            clearInterval(interval);
          }
        } catch (err) {
          console.error('Error polling sucursal logs:', err);
        }
      }, 1500);
      
      setModalPollingInterval(interval);
    } catch (err) {
      console.error('Error al forzar sync:', err);
      setSyncStatus('error');
    } finally {
      setForcingId(null);
    }
  }

  const closeSyncModal = () => {
    if (modalPollingInterval) clearInterval(modalPollingInterval);
    setActiveSyncModal(null);
  };

  useEffect(() => {
    return () => {
      if (modalPollingInterval) clearInterval(modalPollingInterval);
    };
  }, [modalPollingInterval]);

  const handleSetInterval = async (sucursalId: string, intervalSeconds: number) => {
    setUpdatingIntervalId(sucursalId);
    try {
      await api.post(`/sync/interval/${sucursalId}`, { intervalSeconds });
      // Optimistic update of stats
      setStats(prev => prev.map(s => 
        s.sucursalId === sucursalId ? { ...s, syncIntervalSeconds: intervalSeconds } : s
      ));
      loadLogs(); // Reload logs to show configuration change event
    } catch (err) {
      console.error('Error al cambiar el intervalo:', err);
      alert('Error al actualizar el intervalo de sincronización.');
    } finally {
      setUpdatingIntervalId(null);
    }
  };

  function formatTime(isoString: string | null): string {
    if (!isoString) return 'Nunca';
    const date = new Date(isoString);
    return date.toLocaleString('es-AR', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      day: '2-digit',
      month: '2-digit',
    });
  }

  const handleRefreshAll = () => {
    loadStats();
    loadLogs();
  };

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Monitoreo de Sincronización</h3>
          <p className="text-xs text-pearl-400">
            Control de sincronización bidireccional en tiempo real entre el Backoffice Cloud y los POS locales
          </p>
        </div>
        <button 
          onClick={handleRefreshAll}
          className="flex items-center justify-center gap-1.5 h-9 px-4 text-sm font-medium border border-pearl-200 text-pearl-600 rounded-lg hover:bg-ice-50 active:scale-[0.98] transition-all cursor-pointer bg-white"
        >
          <RefreshCw size={15} className={loading ? 'animate-spin' : ''} />
          Actualizar Estado
        </button>
      </div>

      {loading && stats.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <RefreshCw size={40} className="animate-spin mb-3 text-brand-500" />
          <p className="text-sm font-medium">Cargando reportes de sincronización...</p>
        </div>
      ) : (
        <div className="space-y-6">
          {/* Selector de Sucursal */}
          <div className="bg-white rounded-2xl border border-pearl-100 p-5 shadow-card flex flex-col sm:flex-row sm:items-center justify-between gap-4 animate-fade-in">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center shrink-0">
                <Building size={20} />
              </div>
              <div>
                <h4 className="text-sm font-bold text-pearl-900">Sucursal de Trabajo</h4>
                <p className="text-[11px] text-pearl-400">Sincroniza y administra la sucursal seleccionada</p>
              </div>
            </div>
            
            <div className="w-full sm:w-72">
              <select
                id="sucursal-sync-selector"
                value={selectedSucursalId}
                onChange={(e) => setSelectedSucursalId(e.target.value)}
                className="w-full h-10 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 font-medium text-pearl-800"
              >
                <option value="" disabled>Seleccione una sucursal...</option>
                {stats.map(s => (
                  <option key={s.sucursalId} value={s.sucursalId}>
                    {s.sucursalNombre}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Mostrar detalles de la sucursal seleccionada */}
          {(() => {
            const s = stats.find(item => item.sucursalId === selectedSucursalId);
            if (!s) {
              return (
                <div className="bg-white rounded-xl border border-pearl-100 p-8 text-center text-pearl-400">
                  Por favor, seleccione una sucursal válida del selector superior.
                </div>
              );
            }
            return (
              <div key={s.sucursalId} className="bg-white rounded-2xl border border-pearl-100 p-6 shadow-card hover:shadow-card-hover transition-all duration-300">
              {/* Header Sucursal */}
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-pearl-100 pb-5 mb-5">
                <div className="flex items-center gap-3">
                  <div className="w-12 h-12 rounded-2xl bg-brand-50 text-brand-600 flex items-center justify-center shrink-0">
                    <Building size={22} />
                  </div>
                  <div>
                    <h4 className="text-base font-bold text-pearl-900">{s.sucursalNombre}</h4>
                    <div className="flex flex-wrap items-center gap-x-3 gap-y-1 mt-0.5 text-xs text-pearl-400">
                      <div className="flex items-center gap-1">
                        <Clock size={13} />
                        <span>Frecuencia: Cada {s.syncIntervalSeconds || 30}s</span>
                      </div>
                      <span className="text-pearl-200">|</span>
                      <div className="flex items-center gap-1 font-semibold text-brand-600 bg-brand-50/50 px-2 py-0.5 rounded">
                        <span className="w-1.5 h-1.5 rounded-full bg-success-500 animate-ping"></span>
                        <span>{s.connectedPosCount || 0} {s.connectedPosCount === 1 ? 'Terminal conectada' : 'Terminales conectadas'}</span>
                      </div>
                    </div>
                  </div>
                </div>

                <div className="flex flex-wrap items-center gap-3">
                  {s.forceSyncPending && (
                    <span className="flex items-center gap-1 text-[11px] font-semibold bg-warning-50 text-warning-600 px-3 py-1 rounded-full border border-warning-100">
                      <AlertCircle size={12} className="animate-pulse" />
                      Sync Forzada Pendiente
                    </span>
                  )}
                  
                  {/* Selector de intervalo */}
                  <div className="flex items-center gap-2 border border-pearl-200 rounded-lg px-3 h-9 bg-pearl-50/50">
                    <span className="text-[11px] font-bold text-pearl-500 uppercase tracking-wider">Intervalo:</span>
                    <select
                      value={s.syncIntervalSeconds || 30}
                      disabled={updatingIntervalId === s.sucursalId}
                      onChange={(e) => handleSetInterval(s.sucursalId, parseInt(e.target.value))}
                      className="text-xs font-bold text-pearl-700 bg-transparent border-none focus:outline-none cursor-pointer disabled:opacity-50"
                    >
                      <option value={5}>5 seg</option>
                      <option value={10}>10 seg</option>
                      <option value={30}>30 seg</option>
                      <option value={60}>1 min</option>
                      <option value={120}>2 min</option>
                      <option value={300}>5 min</option>
                    </select>
                  </div>

                  <button
                    onClick={() => handleForceSync(s.sucursalId, s.sucursalNombre)}
                    disabled={forcingId === s.sucursalId}
                    className="flex items-center gap-1.5 h-9 px-4 text-xs font-bold bg-brand-600 hover:bg-brand-700 text-white rounded-lg active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed transition-all duration-200 cursor-pointer"
                  >
                    <RefreshCw size={13} className={forcingId === s.sucursalId ? 'animate-spin' : ''} />
                    Forzar Sincronización
                  </button>
                </div>
              </div>

              {/* Grid de Syncs */}
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                
                {/* Columna PUSH (Comandas, Pagos, Movimientos) */}
                <div className="space-y-4">
                  <div className="flex items-center gap-2 text-xs font-bold text-pearl-400 uppercase tracking-widest border-b border-pearl-50 pb-2">
                    <UploadCloud size={14} className="text-warning-500" />
                    PUSH: Local ➔ Nube (Ventas y Caja)
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    {/* Comandas */}
                    <div className="bg-ice-50/50 rounded-xl p-3 border border-pearl-100/50">
                      <p className="text-[10px] font-bold text-pearl-400 uppercase">Comandas</p>
                      <div className="flex items-baseline gap-1 mt-1.5">
                        <span className="text-lg font-black text-pearl-800">{s.comandasCount}</span>
                        <span className="text-[10px] text-pearl-400">registros</span>
                      </div>
                      <div className="mt-2 text-[10px] text-pearl-500 flex items-center gap-1">
                        <Clock size={10} />
                        <span>{formatTime(s.lastPushComandas)}</span>
                      </div>
                    </div>

                    {/* Pagos */}
                    <div className="bg-ice-50/50 rounded-xl p-3 border border-pearl-100/50">
                      <p className="text-[10px] font-bold text-pearl-400 uppercase">Pagos</p>
                      <div className="flex items-baseline gap-1 mt-1.5">
                        <span className="text-lg font-black text-pearl-800">{s.pagosCount}</span>
                        <span className="text-[10px] text-pearl-400">registros</span>
                      </div>
                      <div className="mt-2 text-[10px] text-pearl-500 flex items-center gap-1">
                        <Clock size={10} />
                        <span>{formatTime(s.lastPushPagos)}</span>
                      </div>
                    </div>

                    {/* Movimientos */}
                    <div className="bg-ice-50/50 rounded-xl p-3 border border-pearl-100/50">
                      <p className="text-[10px] font-bold text-pearl-400 uppercase">Mov. Caja</p>
                      <div className="flex items-baseline gap-1 mt-1.5">
                        <span className="text-lg font-black text-pearl-800">{s.movimientosCount}</span>
                        <span className="text-[10px] text-pearl-400">registros</span>
                      </div>
                      <div className="mt-2 text-[10px] text-pearl-500 flex items-center gap-1">
                        <Clock size={10} />
                        <span>{formatTime(s.lastPushMovimientos)}</span>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Columna PULL (Planos, Mesas, Roles, Usuarios) */}
                <div className="space-y-4">
                  <div className="flex items-center gap-2 text-xs font-bold text-pearl-400 uppercase tracking-widest border-b border-pearl-50 pb-2">
                    <DownloadCloud size={14} className="text-success-500" />
                    PULL: Nube ➔ Local (Catálogos)
                  </div>
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                    {/* Configuracion */}
                    <div className="bg-ice-50/50 rounded-xl p-3 border border-pearl-100/50">
                      <p className="text-[10px] font-bold text-pearl-400 uppercase">Planos POS</p>
                      <div className="flex items-baseline gap-1 mt-1.5">
                        <span className="text-lg font-black text-pearl-800">{s.configuracionesCount}</span>
                        <span className="text-[10px] text-pearl-400">planos</span>
                      </div>
                      <div className="mt-2 text-[10px] text-pearl-500 flex items-center gap-1">
                        <Clock size={10} />
                        <span className="truncate">{formatTime(s.lastPullConfig)}</span>
                      </div>
                    </div>

                    {/* Mesas */}
                    <div className="bg-ice-50/50 rounded-xl p-3 border border-pearl-100/50">
                      <p className="text-[10px] font-bold text-pearl-400 uppercase">Mesas</p>
                      <div className="flex items-baseline gap-1 mt-1.5">
                        <span className="text-lg font-black text-pearl-800">{s.mesasCount}</span>
                        <span className="text-[10px] text-pearl-400">mesas</span>
                      </div>
                      <div className="mt-2 text-[10px] text-pearl-500 flex items-center gap-1">
                        <Clock size={10} />
                        <span className="truncate">{formatTime(s.lastPullMesas)}</span>
                      </div>
                    </div>

                    {/* Roles */}
                    <div className="bg-ice-50/50 rounded-xl p-3 border border-pearl-100/50">
                      <p className="text-[10px] font-bold text-pearl-400 uppercase">Roles</p>
                      <div className="flex items-baseline gap-1 mt-1.5">
                        <span className="text-lg font-black text-pearl-800">{s.rolesCount}</span>
                        <span className="text-[10px] text-pearl-400">roles</span>
                      </div>
                      <div className="mt-2 text-[10px] text-pearl-500 flex items-center gap-1">
                        <Clock size={10} />
                        <span className="truncate">{formatTime(s.lastPullRoles)}</span>
                      </div>
                    </div>

                    {/* Usuarios */}
                    <div className="bg-ice-50/50 rounded-xl p-3 border border-pearl-100/50">
                      <p className="text-[10px] font-bold text-pearl-400 uppercase">Empleados</p>
                      <div className="flex items-baseline gap-1 mt-1.5">
                        <span className="text-lg font-black text-pearl-800">{s.usuariosCount}</span>
                        <span className="text-[10px] text-pearl-400">personal</span>
                      </div>
                      <div className="mt-2 text-[10px] text-pearl-500 flex items-center gap-1">
                        <Clock size={10} />
                        <span className="truncate">{formatTime(s.lastPullUsuarios)}</span>
                      </div>
                    </div>
                  </div>
                </div>

              </div>

              {/* Status Footer Badge */}
              <div className="flex items-center gap-1.5 justify-end mt-4 pt-3 border-t border-pearl-50 text-xs text-pearl-500">
                <CheckCircle2 size={14} className="text-success-500" />
                <span>Ecosistema en línea y sincronizado con Nube</span>
              </div>
            </div>
            );
          })()}
          {stats.length === 0 && !loading && (
            <div className="bg-white rounded-xl border border-pearl-100 p-8 text-center text-pearl-400 flex flex-col items-center justify-center">
              <Database size={40} className="mb-2 text-pearl-300" />
              <span>No se encontraron sucursales registradas para monitorear.</span>
            </div>
          )}

          {/* Panel de Logs */}
          <div className="bg-white rounded-2xl border border-pearl-100 p-6 shadow-card">
            <div className="flex items-center justify-between border-b border-pearl-100 pb-4 mb-4">
              <div className="flex items-center gap-2">
                <Activity size={18} className="text-brand-500" />
                <h4 className="text-sm font-bold text-pearl-900">Historial Reciente de Sincronización</h4>
              </div>
              <span className="text-[10px] bg-ice-50 text-brand-600 font-bold px-2.5 py-0.5 rounded-full uppercase tracking-wider">En vivo (10s)</span>
            </div>

            {loadingLogs && logs.length === 0 ? (
              <div className="flex justify-center py-8 text-pearl-400 text-xs">
                <RefreshCw size={16} className="animate-spin mr-2" />
                <span>Cargando historial de eventos...</span>
              </div>
            ) : logs.length === 0 ? (
              <div className="text-center py-8 text-pearl-400 text-xs">
                <FileText size={28} className="mx-auto mb-1 text-pearl-300" />
                <span>No hay eventos registrados aún en este ciclo de ejecución.</span>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="border-b border-pearl-100 text-[10px] font-bold text-pearl-400 uppercase tracking-wider">
                      <th className="pb-2">Hora</th>
                      <th className="pb-2">Sucursal</th>
                      <th className="pb-2">Tipo</th>
                      <th className="pb-2">Operación / Suceso</th>
                      <th className="pb-2 text-right">Estado</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-pearl-50 text-xs">
                    {logs.filter(log => log.sucursalId === selectedSucursalId).map(log => {
                      let typeBadge = "bg-pearl-100 text-pearl-700";
                      if (log.tipo === "PUSH") typeBadge = "bg-warning-50 text-warning-700 border border-warning-100";
                      else if (log.tipo === "PULL") typeBadge = "bg-success-50 text-success-700 border border-success-100";
                      else if (log.tipo === "CONFIG") typeBadge = "bg-brand-50 text-brand-700 border border-brand-100";
                      else if (log.tipo === "ERROR") typeBadge = "bg-error-50 text-error-700 border border-error-100";
                      else if (log.tipo === "HEARTBEAT") typeBadge = "bg-ice-50 text-pearl-600 border border-pearl-100";

                      return (
                        <tr key={log.id} className="hover:bg-pearl-50/30 transition-all">
                          <td className="py-2.5 text-pearl-500 whitespace-nowrap">
                            {new Date(log.timestamp).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                          </td>
                          <td className="py-2.5 font-semibold text-pearl-700">{log.sucursalNombre}</td>
                          <td className="py-2.5">
                            <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${typeBadge}`}>
                              {log.tipo}
                            </span>
                          </td>
                          <td className="py-2.5 text-pearl-600 font-medium break-words max-w-lg">{log.mensaje}</td>
                          <td className="py-2.5 text-right whitespace-nowrap">
                            {log.exitoso ? (
                              <span className="inline-flex items-center gap-1 text-[11px] font-bold text-success-600">
                                <CheckCircle2 size={12} />
                                OK
                              </span>
                            ) : (
                              <span className="inline-flex items-center gap-1 text-[11px] font-bold text-error-600">
                                <AlertCircle size={12} />
                                FALLÓ
                              </span>
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

        </div>
      )}
      {/* Modal de Sincronización en Vivo */}
      {activeSyncModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: 'rgba(0,0,0,0.6)', backdropFilter: 'blur(4px)' }}>
          <div className="bg-white rounded-2xl border border-pearl-100 w-full max-w-lg p-6 shadow-2xl space-y-5 flex flex-col">
            <div className="flex items-center justify-between border-b border-pearl-50 pb-3">
              <div className="flex items-center gap-2">
                <RefreshCw size={18} className={`text-brand-500 ${syncStatus === 'running' ? 'animate-spin' : ''}`} />
                <h4 className="text-sm font-bold text-pearl-900">Sincronización en Vivo: {activeSyncModal.sucursalNombre}</h4>
              </div>
              <button 
                onClick={closeSyncModal}
                className="p-1 rounded hover:bg-pearl-100 text-pearl-400 hover:text-pearl-700 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-4">
              <div className="flex items-center justify-between text-xs">
                <span className="font-semibold text-pearl-500 uppercase tracking-wider">Estado de la Conexión:</span>
                {syncStatus === 'waiting' && <span className="text-warning-600 font-bold bg-warning-50 px-2 py-0.5 rounded-full border border-warning-100">Esperando Terminal...</span>}
                {syncStatus === 'running' && <span className="text-brand-600 font-bold bg-brand-50 px-2 py-0.5 rounded-full border border-brand-100 animate-pulse">Sincronizando...</span>}
                {syncStatus === 'success' && <span className="text-success-600 font-bold bg-success-50 px-2 py-0.5 rounded-full border border-success-100">Sincronización Exitosa</span>}
                {syncStatus === 'error' && <span className="text-error-600 font-bold bg-error-50 px-2 py-0.5 rounded-full border border-error-100">Sincronización Incompleta / Timeout</span>}
              </div>

              <p className="text-xs text-pearl-400 leading-relaxed">
                {syncStatus === 'waiting' && 'La señal ha sido enviada. El Punto de Venta local iniciará el ciclo y reportará logs en breve.'}
                {syncStatus === 'running' && 'El POS está subiendo transacciones y descargando la última configuración de salones, productos y precios.'}
                {syncStatus === 'success' && 'Sincronización finalizada correctamente. Todos los datos están consolidados.'}
                {syncStatus === 'error' && (errorMessage ? `Error detectado: ${errorMessage}` : 'La terminal no respondió o se produjo un error durante la sincronización.')}
              </p>

              {/* Terminal Logs View */}
              <div className="w-full h-56 bg-pearl-950 rounded-xl p-3 overflow-y-auto text-left font-mono text-[10px] leading-relaxed space-y-1.5 scrollbar-thin text-pearl-300">
                {syncModalLogs.length === 0 ? (
                  <div className="text-pearl-500 italic animate-pulse">Esperando primer reporte del POS...</div>
                ) : (
                  syncModalLogs.map((log) => {
                    let colorClass = 'text-pearl-400';
                    if (log.tipo === 'ERROR') colorClass = 'text-error-400 font-bold';
                    else if (log.tipo === 'PUSH') colorClass = 'text-warning-400';
                    else if (log.tipo === 'PULL') colorClass = 'text-success-400';
                    else if (log.tipo === 'CONFIG') colorClass = 'text-brand-400';
                    
                    const timeStr = new Date(log.timestamp).toLocaleTimeString('es-AR', {
                      hour: '2-digit',
                      minute: '2-digit',
                      second: '2-digit'
                    });
                    
                    return (
                      <div key={log.id} className="flex gap-2 items-start hover:bg-pearl-900/60 py-0.5 rounded px-1">
                        <span className="text-pearl-500 font-medium shrink-0">{timeStr}</span>
                        <span className={`${colorClass} break-words`}>{log.mensaje}</span>
                      </div>
                    );
                  })
                )}
              </div>
            </div>

            <div className="border-t border-pearl-50 pt-3 flex justify-end">
              <button
                onClick={closeSyncModal}
                className={`px-5 h-9 rounded-lg text-xs font-bold text-white transition-all cursor-pointer shadow-sm active:scale-[0.98] ${
                  syncStatus === 'success' ? 'bg-success-600 hover:bg-success-700 shadow-success-100' :
                  syncStatus === 'error' ? 'bg-error-600 hover:bg-error-700 shadow-error-100' :
                  'bg-pearl-600 hover:bg-pearl-700 shadow-pearl-100'
                }`}
              >
                Cerrar Panel
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
