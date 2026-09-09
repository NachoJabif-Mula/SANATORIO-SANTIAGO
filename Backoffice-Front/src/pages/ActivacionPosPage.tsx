import { useState, useEffect } from 'react';
import { Smartphone, Copy, CheckCircle2, Clock, AlertTriangle, Building2, Plus, Shield } from 'lucide-react';
import api from '@/services/api';

interface Dispositivo {
  id: string;
  sucursal: string;
  codigo: string;
  descripcion: string;
  isActivado: boolean;
  activadoEn: string | null;
  expiraEn: string;
}

export default function ActivacionPosPage() {
  const [dispositivos, setDispositivos] = useState<Dispositivo[]>([]);
  const [sucursales, setSucursales] = useState<{ id: string; nombre: string }[]>([]);
  const [showModal, setShowModal] = useState(false);
  const [newDesc, setNewDesc] = useState('');
  const [newSucursalId, setNewSucursalId] = useState('');
  const [generatedCode, setGeneratedCode] = useState<string | null>(null);
  const [copied, setCopied] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // --- Cargar datos ---
  const loadData = async () => {
    try {
      const [disposRes, sucsRes] = await Promise.all([
        api.get('/dispositivos'),
        api.get('/sucursal')
      ]);

      const mapped = disposRes.data.map((d: any) => ({
        id: d.id,
        sucursal: d.sucursal,
        codigo: d.codigo,
        descripcion: d.descripcion,
        isActivado: d.isActivado,
        activadoEn: d.activadoEn,
        expiraEn: d.expiraEn
      }));

      setDispositivos(mapped);
      setSucursales(sucsRes.data);
    } catch (err) {
      console.error('Error al cargar datos de activación:', err);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  async function handleGenerate() {
    if (!newSucursalId || !newDesc) return;
    setLoading(true);
    try {
      const res = await api.post('/dispositivos/generar-codigo', {
        sucursalId: newSucursalId,
        nombreDispositivo: newDesc
      });
      
      setGeneratedCode(res.data.codigoActivacion);
      await loadData();
    } catch (err) {
      console.error('Error generando código:', err);
      alert('Error al generar código en el servidor');
    } finally {
      setLoading(false);
    }
  }

  async function handleRevoke(id: string) {
    if (confirm('¿Estás seguro de que deseas revocar este dispositivo? Perderá la conexión al servidor de inmediato.')) {
      try {
        await api.post(`/dispositivos/${id}/revocar`);
        await loadData();
      } catch (err) {
        console.error('Error al revocar dispositivo:', err);
        alert('Error al revocar en el servidor');
      }
    }
  }

  function handleCopy(code: string) {
    navigator.clipboard.writeText(code);
    setCopied(code);
    setTimeout(() => setCopied(null), 2000);
  }

  function closeModal() {
    setShowModal(false);
    setNewDesc('');
    setNewSucursalId('');
    setGeneratedCode(null);
  }

  const activados = dispositivos.filter(d => d.isActivado).length;
  const pendientes = dispositivos.filter(d => !d.isActivado).length;

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Módulo de Activación POS</h3>
          <p className="text-xs text-pearl-400">{activados} activados · {pendientes} pendientes de activación</p>
        </div>
        <button id="generate-code-btn" onClick={() => setShowModal(true)} className="flex items-center gap-1.5 h-9 px-4 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all duration-200 cursor-pointer">
          <Plus size={16} /> Generar Código
        </button>
      </div>

      {/* Info Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-white rounded-xl border border-pearl-100 p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-success-50 text-success-600 flex items-center justify-center"><CheckCircle2 size={20} /></div>
          <div><p className="text-[10px] text-pearl-400 uppercase tracking-wider">Activados</p><p className="text-xl font-bold text-pearl-900">{activados}</p></div>
        </div>
        <div className="bg-white rounded-xl border border-pearl-100 p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-warning-50 text-warning-600 flex items-center justify-center"><Clock size={20} /></div>
          <div><p className="text-[10px] text-pearl-400 uppercase tracking-wider">Pendientes</p><p className="text-xl font-bold text-pearl-900">{pendientes}</p></div>
        </div>
        <div className="bg-white rounded-xl border border-pearl-100 p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-brand-50 text-brand-600 flex items-center justify-center"><Shield size={20} /></div>
          <div><p className="text-[10px] text-pearl-400 uppercase tracking-wider">JWT M2M</p><p className="text-xl font-bold text-pearl-900">365 días</p></div>
        </div>
      </div>

      {/* Dispositivos Table */}
      <div className="bg-white rounded-xl border border-pearl-100 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-ice-50 border-b border-pearl-100">
                <th className="px-4 py-3 text-left text-[10px] font-semibold text-pearl-500 uppercase tracking-wider">Sucursal</th>
                <th className="px-4 py-3 text-left text-[10px] font-semibold text-pearl-500 uppercase tracking-wider">Descripción</th>
                <th className="px-4 py-3 text-left text-[10px] font-semibold text-pearl-500 uppercase tracking-wider">Código</th>
                <th className="px-4 py-3 text-left text-[10px] font-semibold text-pearl-500 uppercase tracking-wider">Estado</th>
                <th className="px-4 py-3 text-left text-[10px] font-semibold text-pearl-500 uppercase tracking-wider">Expiración</th>
                <th className="px-4 py-3 text-left text-[10px] font-semibold text-pearl-500 uppercase tracking-wider">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-pearl-50">
              {dispositivos.map(d => (
                <tr key={d.id} className="hover:bg-brand-50/30 transition-colors duration-150">
                  <td className="px-4 py-3 text-xs font-medium text-pearl-800 flex items-center gap-2"><Building2 size={14} className="text-pearl-400" />{d.sucursal}</td>
                  <td className="px-4 py-3 text-xs text-pearl-600">{d.descripcion}</td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <code className="text-xs font-mono font-bold text-brand-700 bg-brand-50 px-2 py-1 rounded">{d.codigo}</code>
                      <button onClick={() => handleCopy(d.codigo)} className="text-pearl-400 hover:text-brand-600 transition-colors cursor-pointer" title="Copiar código">
                        {copied === d.codigo ? <CheckCircle2 size={14} className="text-success-500" /> : <Copy size={14} />}
                      </button>
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    {d.isActivado
                      ? <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-success-50 text-success-600 border border-success-500/20">Activado</span>
                      : <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-warning-50 text-warning-600 border border-warning-500/20 flex items-center gap-1 w-fit"><AlertTriangle size={10} /> Pendiente</span>
                    }
                  </td>
                  <td className="px-4 py-3 text-xs text-pearl-600">{d.isActivado ? new Date(d.expiraEn).toLocaleDateString('es-AR') : <span className="text-warning-600 font-medium">{new Date(d.expiraEn).toLocaleString('es-AR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}</span>}</td>
                  <td className="px-4 py-3"><button onClick={() => handleRevoke(d.id)} className="text-xs text-pearl-500 hover:text-danger-600 font-medium transition-colors cursor-pointer">Revocar</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={closeModal}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-md mx-4 p-6 animate-fade-in" onClick={e => e.stopPropagation()}>
            <div className="flex items-center gap-3 mb-5">
              <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center"><Smartphone size={20} /></div>
              <div>
                <h4 className="text-sm font-bold text-pearl-900">Generar Código de Emparejamiento</h4>
                <p className="text-[11px] text-pearl-400">El código expira en 24 horas</p>
              </div>
            </div>

            {!generatedCode ? (
              <div className="space-y-4">
                <div>
                  <label htmlFor="modal-sucursal" className="block text-xs font-medium text-pearl-600 mb-1.5">Sucursal</label>
                  <select id="modal-sucursal" value={newSucursalId} onChange={e => setNewSucursalId(e.target.value)} className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 cursor-pointer appearance-none">
                    <option value="">Seleccionar sucursal...</option>
                    {sucursales.map(s => (
                      <option key={s.id} value={s.id}>{s.nombre}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label htmlFor="modal-desc" className="block text-xs font-medium text-pearl-600 mb-1.5">Descripción del dispositivo</label>
                  <input id="modal-desc" type="text" value={newDesc} onChange={e => setNewDesc(e.target.value)} placeholder="Ej: POS Caja Principal" className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400" />
                </div>
                <div className="flex gap-2 pt-2">
                  <button onClick={closeModal} className="flex-1 h-9 text-sm font-medium text-pearl-600 bg-pearl-100 rounded-lg hover:bg-pearl-200 transition-colors cursor-pointer">Cancelar</button>
                  <button id="confirm-generate-btn" onClick={handleGenerate} disabled={!newSucursalId || !newDesc || loading} className="flex-1 h-9 text-sm font-medium text-white bg-brand-600 rounded-lg hover:bg-brand-700 disabled:opacity-40 disabled:cursor-not-allowed transition-all cursor-pointer">
                    {loading ? 'Generando...' : 'Generar'}
                  </button>
                </div>
              </div>
            ) : (
              <div className="text-center space-y-4">
                <div className="bg-ice-50 rounded-xl p-6 border border-pearl-100">
                  <p className="text-[10px] text-pearl-400 uppercase tracking-wider mb-2">Código de emparejamiento</p>
                  <code className="text-2xl font-mono font-black text-brand-700 tracking-widest">{generatedCode}</code>
                </div>
                <p className="text-xs text-pearl-500">Ingresá este código en el dispositivo POS del local <strong>{sucursales.find(s => s.id === newSucursalId)?.nombre}</strong> para completar la activación.</p>
                <div className="flex gap-2">
                  <button onClick={() => handleCopy(generatedCode)} className="flex-1 h-9 text-sm font-medium text-brand-600 bg-brand-50 rounded-lg hover:bg-brand-100 transition-colors cursor-pointer flex items-center justify-center gap-1.5">
                    {copied === generatedCode ? <><CheckCircle2 size={14} /> Copiado!</> : <><Copy size={14} /> Copiar</>}
                  </button>
                  <button onClick={closeModal} className="flex-1 h-9 text-sm font-medium text-white bg-brand-600 rounded-lg hover:bg-brand-700 transition-colors cursor-pointer">Listo</button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
