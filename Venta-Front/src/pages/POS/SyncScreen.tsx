import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, RefreshCw, Trash2 } from 'lucide-react';
import api from '@/services/api';

interface SyncLog {
  id: string;
  timestamp: string;
  tipo: string; // PUSH, PULL, CONFIG, ERROR, HEARTBEAT
  mensaje: string;
  exitoso: boolean;
}

type Nivel = 'ok' | 'info' | 'warn' | 'err';
type FiltroKey = 'todos' | 'info' | 'warn' | 'err';

const FILTROS: { key: FiltroKey; label: string }[] = [
  { key: 'todos', label: 'Todos' },
  { key: 'info', label: 'Info' },
  { key: 'warn', label: 'Avisos' },
  { key: 'err', label: 'Errores' }
];

function nivelDe(log: SyncLog): Nivel {
  if (log.tipo === 'ERROR') return 'err';
  if (!log.exitoso) return 'warn';
  if (log.tipo === 'PUSH' || log.tipo === 'PULL') return 'ok';
  return 'info';
}

export default function SyncScreen() {
  const navigate = useNavigate();
  const [logs, setLogs] = useState<SyncLog[]>([]);
  const [loadingLogs, setLoadingLogs] = useState(true);
  const [syncing, setSyncing] = useState(false);
  const [filtro, setFiltro] = useState<FiltroKey>('todos');
  const [hiddenBefore, setHiddenBefore] = useState<string | null>(null);
  const [pollInterval, setPollInterval] = useState<ReturnType<typeof setInterval> | null>(null);

  const fetchLogs = useCallback(async () => {
    try {
      const res = await api.get('/sync/logs');
      setLogs(res.data || []);
    } catch (err) {
      console.error('Error al obtener logs de sincronización:', err);
    } finally {
      setLoadingLogs(false);
    }
  }, []);

  useEffect(() => {
    fetchLogs();
    return () => {
      if (pollInterval) clearInterval(pollInterval);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const forzarSync = async () => {
    if (syncing) return;
    setSyncing(true);
    try {
      await api.post('/sync/run');
      const interval = setInterval(fetchLogs, 1500);
      setPollInterval(interval);
      setTimeout(() => {
        clearInterval(interval);
        setSyncing(false);
        fetchLogs();
      }, 9000);
    } catch (err) {
      console.error('Error al forzar sincronización:', err);
      setSyncing(false);
    }
  };

  const visibleLogs = logs.filter(l => !hiddenBefore || new Date(l.timestamp) > new Date(hiddenBefore));
  const filtrados = filtro === 'todos' ? visibleLogs : visibleLogs.filter(l => nivelDe(l) === filtro || (filtro === 'info' && nivelDe(l) === 'ok'));
  const counts = {
    todos: visibleLogs.length,
    info: visibleLogs.filter(l => nivelDe(l) === 'info' || nivelDe(l) === 'ok').length,
    warn: visibleLogs.filter(l => nivelDe(l) === 'warn').length,
    err: visibleLogs.filter(l => nivelDe(l) === 'err').length
  };

  const erroresHoy = visibleLogs.filter(l => nivelDe(l) === 'err').length;
  const ultimaSync = logs[0] ? new Date(logs[0].timestamp).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }) : '—';
  const estadoGeneral = syncing ? 'En curso' : erroresHoy > 0 ? 'Con errores' : 'Al día';

  const kpiBox = (accent: string) => ({ borderTop: `2px solid ${accent}` });

  const NIVEL_TAG: Record<Nivel, { label: string; color: string }> = {
    ok: { label: 'OK', color: 'var(--success-500)' },
    info: { label: 'INFO', color: 'var(--slate-500)' },
    warn: { label: 'WARN', color: 'var(--warning-500)' },
    err: { label: 'ERROR', color: 'var(--danger-500)' }
  };

  return (
    <div className="h-screen w-screen flex flex-col bg-slate-950 text-text-primary overflow-hidden">
      <main className="flex-1 flex flex-col min-h-0">
        {/* Header */}
        <div className="flex-shrink-0 flex items-center flex-wrap gap-3 px-4.5 py-3 border-b border-border-default bg-surface-base">
          <button
            onClick={() => navigate('/')}
            className="touch-btn h-12 px-3 flex items-center gap-1.5 rounded-[var(--radius-btn)] bg-surface-base text-text-secondary hover:border-amber-500 hover:text-amber-500 border border-border-default text-[13.5px] font-medium min-h-0"
          >
            <ArrowLeft className="w-4 h-4" />
            Mapa
          </button>
          <div className="flex flex-col gap-0.5 flex-1 min-w-[180px]">
            <span className="text-[15px] font-semibold">Sincronización</span>
            <span className="font-mono text-[11px] text-text-muted">Última sincronización {ultimaSync} · empuje y descarga con el servidor central</span>
          </div>
          <button
            onClick={() => setHiddenBefore(new Date().toISOString())}
            className="touch-btn h-12 px-4 flex items-center gap-1.5 rounded-[var(--radius-btn)] bg-surface-base text-text-secondary hover:border-danger-500 hover:text-danger-500 border border-border-default text-[13.5px] font-semibold min-h-0"
          >
            <Trash2 className="w-4 h-4" />
            Limpiar consola
          </button>
          <button
            onClick={forzarSync}
            disabled={syncing}
            className={`touch-btn h-12 px-5 flex items-center gap-1.5 rounded-[var(--radius-btn)] text-[13.5px] font-semibold min-h-0 border ${
              syncing ? 'bg-surface-overlay border-border-default text-text-muted cursor-progress' : 'bg-amber-500 border-amber-500 text-white hover:bg-amber-600'
            }`}
          >
            <RefreshCw className={`w-4 h-4 ${syncing ? 'animate-spin' : ''}`} />
            {syncing ? 'Sincronizando…' : 'Forzar sincronización'}
          </button>
        </div>

        {/* KPIs */}
        <div className="flex-shrink-0 px-4.5 pt-3.5 grid grid-cols-2 md:grid-cols-4 gap-2.5">
          <div className="flex flex-col gap-0.5 p-3 rounded-[var(--radius-card)] bg-surface-base border border-border-default" style={kpiBox(erroresHoy > 0 ? 'var(--danger-500)' : 'var(--success-500)')}>
            <span className="mono-label">Estado</span>
            <span className="font-mono text-[21px] font-semibold leading-tight tracking-tight">{estadoGeneral}</span>
            <span className="text-[11px] text-text-muted">Última sync {ultimaSync}</span>
          </div>
          <div className="flex flex-col gap-0.5 p-3 rounded-[var(--radius-card)] bg-surface-base border border-border-default" style={kpiBox('var(--danger-500)')}>
            <span className="mono-label">Errores registrados</span>
            <span className="font-mono text-[21px] font-semibold leading-tight tracking-tight">{erroresHoy}</span>
            <span className="text-[11px] text-text-muted">Requieren reintento</span>
          </div>
          <div className="flex flex-col gap-0.5 p-3 rounded-[var(--radius-card)] bg-surface-base border border-border-default" style={kpiBox('var(--acc)')}>
            <span className="mono-label">Eventos</span>
            <span className="font-mono text-[21px] font-semibold leading-tight tracking-tight">{visibleLogs.length}</span>
            <span className="text-[11px] text-text-muted">Registros en consola</span>
          </div>
          <div className="flex flex-col gap-0.5 p-3 rounded-[var(--radius-card)] bg-surface-base border border-border-default" style={kpiBox('var(--slate-500)')}>
            <span className="mono-label">Automática</span>
            <span className="font-mono text-[21px] font-semibold leading-tight tracking-tight">Activada</span>
            <span className="text-[11px] text-text-muted">Corre en segundo plano</span>
          </div>
        </div>

        <div className="flex-1 flex min-h-0 mt-3">
          <div className="flex-1 min-h-0 flex flex-col px-4.5 pb-4.5">
            {/* Filtros */}
            <div className="flex-shrink-0 flex items-center gap-2 pb-2.5 flex-wrap">
              <span className="mono-label mr-1">Consola</span>
              {FILTROS.map(f => {
                const on = filtro === f.key;
                return (
                  <button
                    key={f.key}
                    onClick={() => setFiltro(f.key)}
                    className={`touch-btn h-10 px-3.5 rounded-[var(--radius-btn)] text-[12.5px] font-semibold min-h-0 border ${
                      on ? 'bg-amber-500 border-amber-500 text-white' : 'bg-surface-base border-border-default text-text-secondary'
                    }`}
                  >
                    {f.label} <span className="opacity-70">{counts[f.key]}</span>
                  </button>
                );
              })}
            </div>

            {/* Consola de logs */}
            <div className="flex-1 min-h-[120px] overflow-y-auto border border-border-default rounded-[var(--radius-card)] bg-surface-base p-3 flex flex-col gap-0.5">
              {loadingLogs ? (
                <div className="flex-1 flex items-center justify-center py-10">
                  <div className="w-7 h-7 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
                </div>
              ) : filtrados.length === 0 ? (
                <div className="flex-1 flex items-center justify-center py-10 text-[12.5px] text-text-muted text-center">
                  Sin registros para este filtro.
                </div>
              ) : (
                filtrados.map(log => {
                  const nivel = nivelDe(log);
                  const tag = NIVEL_TAG[nivel];
                  return (
                    <div key={log.id} className="flex items-start gap-2.5 py-1.5 px-2 rounded-[8px]">
                      <span className="font-mono text-[12px] text-text-muted flex-shrink-0">
                        {new Date(log.timestamp).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                      </span>
                      <span className="font-mono text-[10px] font-bold tracking-wider w-[52px] flex-shrink-0 pt-0.5" style={{ color: tag.color }}>
                        {tag.label}
                      </span>
                      <span className="font-mono text-[12.5px] leading-relaxed min-w-0 break-words">{log.mensaje}</span>
                    </div>
                  );
                })
              )}
            </div>
          </div>

          {/* Panel lateral informativo */}
          <aside className="flex-shrink-0 w-[300px] min-w-[260px] border-l border-border-default bg-surface-base flex flex-col min-h-0">
            <div className="flex-shrink-0 p-4 border-b border-border-default flex flex-col gap-0.5">
              <span className="mono-label">Acerca de la sincronización</span>
              <span className="text-[15px] font-semibold">Cómo funciona</span>
            </div>
            <div className="flex-1 overflow-y-auto p-4 flex flex-col gap-3 text-[12.5px] text-text-secondary leading-relaxed">
              <p>La terminal envía en segundo plano las ventas, cobros y movimientos de caja pendientes al servidor central, y descarga catálogo de productos, precios, mesas y usuarios actualizados.</p>
              <p>Usá <strong className="text-text-primary">Forzar sincronización</strong> para ejecutar un ciclo inmediato sin esperar al próximo intervalo automático.</p>
              <p className="text-text-muted">La consola muestra los últimos eventos registrados por el motor de sincronización local.</p>
            </div>
          </aside>
        </div>
      </main>
    </div>
  );
}
