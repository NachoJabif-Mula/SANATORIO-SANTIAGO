import { useState, useEffect } from 'react';
import { 
  ListTodo, 
  RefreshCw, 
  Trash2, 
  Search, 
  Filter, 
  Eye, 
  Printer, 
  CheckCircle2, 
  XCircle, 
  Clock, 
  AlertCircle,
  X
} from 'lucide-react';
import api from '@/services/api';

interface PrintJobItem {
  id: string;
  sucursalId: string;
  impresoraId: string;
  impresoraNombre?: string;
  sucursalNombre?: string;
  estado: 'Pendiente' | 'Enviado' | 'Impreso' | 'Fallo';
  tipoDocumento: string;
  payloadJson: string;
  resultadoJson?: string;
  intentos: number;
  createdAt: string;
}

export default function ColaImpresionPage() {
  const [jobs, setJobs] = useState<PrintJobItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [autoRefresh, setAutoRefresh] = useState(true);

  // Filtros
  const [filtroEstado, setFiltroEstado] = useState<string>('todos');
  const [searchTerm, setSearchTerm] = useState<string>('');

  // Modal Detalle
  const [selectedJob, setSelectedJob] = useState<PrintJobItem | null>(null);

  const loadData = async () => {
    setLoading(true);
    try {
      const res = await api.get('/print-job');
      setJobs(res.data || []);
    } catch (err) {
      console.error('Error al cargar cola de impresión:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    let interval: NodeJS.Timeout | null = null;
    if (autoRefresh) {
      interval = setInterval(() => {
        loadData();
      }, 10000);
    }
    return () => {
      if (interval) clearInterval(interval);
    };
  }, [autoRefresh]);

  const handleRetryJob = async (jobId: string) => {
    try {
      await api.post(`/print-job/${jobId}/reintentar`);
      await loadData();
    } catch (err) {
      console.error('Error al reintentar impresión:', err);
      alert('Error al enviar reintento.');
    }
  };

  const handleClearFinished = async () => {
    if (confirm('¿Deseas eliminar todos los trabajos impresos o finalizados de la lista?')) {
      try {
        await api.delete('/print-job/limpiar-finalizados');
        await loadData();
      } catch (err) {
        console.error('Error al limpiar la cola:', err);
      }
    }
  };

  const filteredJobs = jobs.filter(j => {
    const matchesEstado = filtroEstado === 'todos' || j.estado.toLowerCase() === filtroEstado.toLowerCase();
    const matchesSearch = !searchTerm.trim() || 
      j.tipoDocumento.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (j.impresoraNombre && j.impresoraNombre.toLowerCase().includes(searchTerm.toLowerCase()));
    return matchesEstado && matchesSearch;
  });

  const getEstadoBadge = (estado: string) => {
    switch (estado) {
      case 'Impreso':
        return <span className="inline-flex items-center gap-1 text-[10px] font-bold bg-success-50 text-success-700 border border-success-200 px-2.5 py-0.5 rounded-full"><CheckCircle2 size={12} /> Impreso</span>;
      case 'Enviado':
        return <span className="inline-flex items-center gap-1 text-[10px] font-bold bg-blue-50 text-blue-700 border border-blue-200 px-2.5 py-0.5 rounded-full"><Clock size={12} /> Enviado</span>;
      case 'Fallo':
        return <span className="inline-flex items-center gap-1 text-[10px] font-bold bg-danger-50 text-danger-700 border border-danger-200 px-2.5 py-0.5 rounded-full"><XCircle size={12} /> Falló</span>;
      default:
        return <span className="inline-flex items-center gap-1 text-[10px] font-bold bg-amber-50 text-amber-700 border border-amber-200 px-2.5 py-0.5 rounded-full"><AlertCircle size={12} /> Pendiente</span>;
    }
  };

  return (
    <div className="space-y-6 animate-fade-in pb-8">
      {/* Title */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
            <ListTodo size={22} />
          </div>
          <div>
            <h3 className="text-base font-bold text-pearl-900">Monitor de Cola de Impresión</h3>
            <p className="text-xs text-pearl-400">Seguimiento en tiempo real de tickets comandera y comprobantes fiscales</p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <label className="flex items-center gap-2 cursor-pointer text-xs text-pearl-600 font-medium select-none bg-white px-3 py-1.5 rounded-lg border border-pearl-200">
            <input
              type="checkbox"
              checked={autoRefresh}
              onChange={e => setAutoRefresh(e.target.checked)}
              className="w-3.5 h-3.5 rounded text-brand-600 border-pearl-300"
            />
            <span>Auto-refresh (10s)</span>
          </label>

          <button
            onClick={loadData}
            className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 rounded-lg hover:bg-ice-50 transition-colors cursor-pointer"
            title="Refrescar"
          >
            <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
          </button>

          <button
            onClick={handleClearFinished}
            className="flex items-center gap-1.5 h-9 px-3 text-xs font-semibold text-pearl-600 bg-white border border-pearl-200 rounded-lg hover:bg-pearl-50 transition-all cursor-pointer"
          >
            <Trash2 size={14} className="text-pearl-400" /> Limpiar Finalizados
          </button>
        </div>
      </div>

      {/* Filter Toolbar */}
      <div className="bg-white p-4 rounded-xl border border-pearl-100 shadow-sm flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Filter size={16} className="text-pearl-400" />
          <div className="flex gap-1 bg-ice-50 p-1 rounded-lg border border-pearl-200">
            {['todos', 'pendiente', 'enviado', 'impreso', 'fallo'].map(st => (
              <button
                key={st}
                onClick={() => setFiltroEstado(st)}
                className={`px-3 py-1 text-xs font-semibold rounded-md capitalize transition-all cursor-pointer ${
                  filtroEstado === st ? 'bg-white text-brand-600 shadow-xs' : 'text-pearl-500 hover:text-pearl-800'
                }`}
              >
                {st}
              </button>
            ))}
          </div>
        </div>

        <div className="relative w-64">
          <Search size={14} className="absolute left-3 top-2.5 text-pearl-400" />
          <input
            type="text"
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
            placeholder="Buscar por documento..."
            className="w-full h-8 pl-8 pr-3 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
          />
        </div>
      </div>

      {/* Table */}
      <div className="bg-white rounded-xl border border-pearl-100 overflow-hidden shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-sm text-left">
            <thead>
              <tr className="bg-ice-50 border-b border-pearl-100 text-pearl-500 text-[10px] font-semibold uppercase tracking-wider">
                <th className="px-4 py-3">Estado</th>
                <th className="px-4 py-3">Tipo Documento</th>
                <th className="px-4 py-3">Impresora</th>
                <th className="px-4 py-3 text-center">Intentos</th>
                <th className="px-4 py-3">Fecha / Hora</th>
                <th className="px-4 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-pearl-50">
              {filteredJobs.map(job => (
                <tr key={job.id} className="hover:bg-ice-50/50 transition-colors">
                  <td className="px-4 py-3">{getEstadoBadge(job.estado)}</td>
                  <td className="px-4 py-3 font-semibold text-pearl-800 flex items-center gap-2">
                    <Printer size={14} className="text-brand-500 shrink-0" />
                    {job.tipoDocumento}
                  </td>
                  <td className="px-4 py-3 text-xs text-pearl-700">
                    {job.impresoraNombre || job.impresoraId || 'Por defecto'}
                  </td>
                  <td className="px-4 py-3 text-center font-mono text-xs text-pearl-600">
                    {job.intentos}
                  </td>
                  <td className="px-4 py-3 text-xs text-pearl-500 font-mono">
                    {new Date(job.createdAt).toLocaleString()}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <div className="flex items-center justify-end gap-2">
                      {job.estado === 'Fallo' && (
                        <button
                          onClick={() => handleRetryJob(job.id)}
                          className="text-xs text-brand-600 hover:text-brand-800 font-semibold bg-brand-50 hover:bg-brand-100 px-2.5 py-1 rounded-lg transition-colors cursor-pointer flex items-center gap-1"
                        >
                          <RefreshCw size={12} /> Reintentar
                        </button>
                      )}
                      <button
                        onClick={() => setSelectedJob(job)}
                        className="text-pearl-500 hover:text-brand-600 p-1.5 rounded hover:bg-ice-50 cursor-pointer"
                        title="Ver payload JSON"
                      >
                        <Eye size={15} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {filteredJobs.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center text-pearl-400 italic">
                    {loading ? 'Cargando cola de impresión...' : 'No hay trabajos de impresión registrados en la cola.'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal Payload JSON */}
      {selectedJob && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={() => setSelectedJob(null)}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-xl mx-4 p-6 animate-fade-in" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-4 pb-3 border-b border-pearl-100">
              <div className="flex items-center gap-2">
                <Printer size={18} className="text-brand-600" />
                <h4 className="text-sm font-bold text-pearl-900">Detalle del Trabajo: {selectedJob.tipoDocumento}</h4>
              </div>
              <button onClick={() => setSelectedJob(null)} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            <div className="space-y-3 text-xs">
              <div className="grid grid-cols-2 gap-2 bg-ice-50 p-3 rounded-lg">
                <div><span className="text-pearl-400">ID:</span> <span className="font-mono text-pearl-700">{selectedJob.id}</span></div>
                <div><span className="text-pearl-400">Estado:</span> {selectedJob.estado}</div>
                <div><span className="text-pearl-400">Intentos:</span> {selectedJob.intentos}</div>
                <div><span className="text-pearl-400">Fecha:</span> {new Date(selectedJob.createdAt).toLocaleString()}</div>
              </div>

              <div>
                <label className="block font-semibold text-pearl-700 mb-1">Payload JSON de Entrada:</label>
                <pre className="bg-pearl-900 text-pearl-100 p-3 rounded-lg font-mono text-[11px] max-h-48 overflow-y-auto whitespace-pre-wrap">
                  {selectedJob.payloadJson}
                </pre>
              </div>

              {selectedJob.resultadoJson && (
                <div>
                  <label className="block font-semibold text-pearl-700 mb-1">Resultado CLI stdout/stderr:</label>
                  <pre className="bg-pearl-900 text-emerald-300 p-3 rounded-lg font-mono text-[11px] max-h-36 overflow-y-auto whitespace-pre-wrap">
                    {selectedJob.resultadoJson}
                  </pre>
                </div>
              )}
            </div>

            <div className="flex justify-end pt-4 border-t border-pearl-100 mt-4">
              <button
                onClick={() => setSelectedJob(null)}
                className="px-4 h-8.5 text-xs font-semibold bg-pearl-100 text-pearl-700 rounded-lg hover:bg-pearl-200 transition-colors cursor-pointer"
              >
                Cerrar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
