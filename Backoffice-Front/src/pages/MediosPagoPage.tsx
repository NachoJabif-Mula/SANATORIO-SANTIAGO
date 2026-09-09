import { useState, useEffect } from 'react';
import { CreditCard, CheckCircle2, XCircle, RefreshCw, Plus, Edit2, Trash2, X, Percent } from 'lucide-react';
import api from '@/services/api';

interface MetodoPago {
  id: string;
  nombre: string;
  comisionPorcentaje: number;
  requiereFacturaAfip: boolean;
  isActive: boolean;
}

export default function MediosPagoPage() {
  const [metodos, setMetodos] = useState<MetodoPago[]>([]);
  const [loading, setLoading] = useState(true);

  // Modal States
  const [showModal, setShowModal] = useState(false);
  const [editingMetodo, setEditingMetodo] = useState<MetodoPago | null>(null);
  const [nombre, setNombre] = useState('');
  const [comisionPorcentaje, setComisionPorcentaje] = useState<number>(0);
  const [requiereFacturaAfip, setRequiereFacturaAfip] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const loadData = async () => {
    setLoading(true);
    try {
      const res = await api.get('/metodopago?includeInactive=false');
      setMetodos(res.data);
    } catch (err) {
      console.error('Error al cargar métodos de pago:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!nombre.trim()) return;
    setSubmitting(true);

    try {
      const payload = {
        nombre: nombre.trim(),
        comisionPorcentaje: Number(comisionPorcentaje),
        requiereFacturaAfip
      };

      if (editingMetodo) {
        await api.put(`/metodopago/${editingMetodo.id}`, payload);
      } else {
        await api.post('/metodopago', payload);
      }
      closeModal();
      await loadData();
    } catch (err) {
      console.error('Error al guardar método de pago:', err);
      alert('Error al guardar el método de pago en el servidor.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(id: string) {
    if (confirm('¿Estás seguro de que deseas desactivar este método de pago?')) {
      try {
        await api.delete(`/metodopago/${id}`);
        await loadData();
      } catch (err) {
        console.error('Error al eliminar método de pago:', err);
        alert('Error al desactivar el método de pago.');
      }
    }
  }

  function openCreateModal() {
    setEditingMetodo(null);
    setNombre('');
    setComisionPorcentaje(0);
    setRequiereFacturaAfip(false);
    setShowModal(true);
  }

  function openEditModal(metodo: MetodoPago) {
    setEditingMetodo(metodo);
    setNombre(metodo.nombre);
    setComisionPorcentaje(metodo.comisionPorcentaje);
    setRequiereFacturaAfip(metodo.requiereFacturaAfip);
    setShowModal(true);
  }

  function closeModal() {
    setShowModal(false);
    setEditingMetodo(null);
    setNombre('');
    setComisionPorcentaje(0);
    setRequiereFacturaAfip(false);
  }

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Medios de Pago</h3>
          <p className="text-xs text-pearl-400">
            {metodos.filter(m => m.isActive).length} activos de {metodos.length} registrados
          </p>
        </div>
        <div className="flex gap-2">
          <button 
            onClick={loadData}
            className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 rounded-lg hover:bg-ice-50 active:scale-[0.98] transition-all cursor-pointer"
            title="Refrescar"
          >
            <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
          </button>
          <button 
            id="add-metodopago-btn" 
            onClick={openCreateModal}
            className="flex items-center gap-1.5 h-9 px-4 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all duration-200 cursor-pointer"
          >
            <Plus size={16} /> Nuevo Medio de Pago
          </button>
        </div>
      </div>

      {loading && metodos.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <CreditCard size={40} className="animate-pulse mb-3" />
          <p className="text-sm">Cargando métodos de pago...</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 stagger-children">
          {metodos.map(m => (
            <div key={m.id} className={`bg-white rounded-xl border p-5 transition-all duration-300 hover:shadow-lg hover:-translate-y-0.5 group ${m.isActive ? 'border-pearl-100' : 'border-pearl-200 opacity-60'}`}>
              <div className="flex items-start justify-between mb-4">
                <div className="flex items-center gap-3">
                  <div className={`w-11 h-11 rounded-xl flex items-center justify-center ${m.isActive ? 'bg-brand-50 text-brand-600' : 'bg-pearl-100 text-pearl-400'} transition-transform group-hover:scale-110`}>
                    <CreditCard size={20} />
                  </div>
                  <div>
                    <h4 className="text-sm font-bold text-pearl-900">{m.nombre}</h4>
                    <div className="flex items-center gap-1 text-xs text-pearl-400 mt-0.5">
                      <Percent size={12} />
                      <span>Comisión: {m.comisionPorcentaje}%</span>
                    </div>
                  </div>
                </div>
                <span className={`text-[10px] font-semibold px-2.5 py-1 rounded-full ${m.isActive ? 'bg-success-50 text-success-600' : 'bg-pearl-100 text-pearl-500'}`}>
                  {m.isActive ? 'Activo' : 'Inactivo'}
                </span>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="bg-ice-50 rounded-lg p-3 text-center">
                  <p className="text-[10px] text-pearl-400 uppercase tracking-wider">Factura AFIP</p>
                  <div className="flex items-center justify-center gap-1 mt-1">
                    {m.requiereFacturaAfip ? (
                      <>
                        <CheckCircle2 size={14} className="text-success-500" />
                        <span className="text-xs font-semibold text-success-600">Requerido</span>
                      </>
                    ) : (
                      <>
                        <XCircle size={14} className="text-pearl-400" />
                        <span className="text-xs font-semibold text-pearl-500">Exento</span>
                      </>
                    )}
                  </div>
                </div>
                <div className="bg-ice-50 rounded-lg p-3 text-center">
                  <p className="text-[10px] text-pearl-400 uppercase tracking-wider">Costo Transacción</p>
                  <p className="text-xs font-semibold text-pearl-700 mt-1">
                    {m.comisionPorcentaje > 0 ? `+${m.comisionPorcentaje}%` : 'Gratis'}
                  </p>
                </div>
              </div>

              <div className="flex items-center justify-end gap-3 mt-4 pt-3 border-t border-pearl-50">
                <button 
                  onClick={() => openEditModal(m)}
                  className="text-xs text-pearl-500 hover:text-brand-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                >
                  <Edit2 size={12} /> Editar
                </button>
                {m.isActive && (
                  <button 
                    onClick={() => handleDelete(m.id)}
                    className="text-xs text-pearl-500 hover:text-danger-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                  >
                    <Trash2 size={12} /> Desactivar
                  </button>
                )}
              </div>
            </div>
          ))}
          {metodos.length === 0 && !loading && (
            <div className="col-span-full bg-white rounded-xl border border-pearl-100 p-8 text-center text-pearl-400">
              No hay métodos de pago registrados. Presiona "Nuevo Medio de Pago" para empezar.
            </div>
          )}
        </div>
      )}

      {/* Modal Crear / Editar */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={closeModal}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-md mx-4 p-6 animate-fade-in" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-5">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
                  <CreditCard size={20} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-pearl-900">
                    {editingMetodo ? 'Editar Medio de Pago' : 'Nuevo Medio de Pago'}
                  </h4>
                  <p className="text-[11px] text-pearl-400">Completa los datos de facturación y comisión</p>
                </div>
              </div>
              <button onClick={closeModal} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label htmlFor="modal-nombre" className="block text-xs font-medium text-pearl-600 mb-1.5">Nombre del Medio de Pago</label>
                <input 
                  id="modal-nombre" 
                  type="text" 
                  value={nombre} 
                  onChange={e => setNombre(e.target.value)} 
                  placeholder="Ej: Mercado Pago QR" 
                  required
                  className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400" 
                />
              </div>
              <div>
                <label htmlFor="modal-comision" className="block text-xs font-medium text-pearl-600 mb-1.5">Comisión del procesador (%)</label>
                <input 
                  id="modal-comision" 
                  type="number" 
                  step="0.01"
                  min="0"
                  max="100"
                  value={comisionPorcentaje} 
                  onChange={e => setComisionPorcentaje(Number(e.target.value))} 
                  placeholder="Ej: 3.5" 
                  required
                  className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400" 
                />
              </div>
              <div className="flex items-center gap-3 py-2">
                <input 
                  id="modal-afip" 
                  type="checkbox" 
                  checked={requiereFacturaAfip} 
                  onChange={e => setRequiereFacturaAfip(e.target.checked)} 
                  className="w-4 h-4 text-brand-600 border-pearl-300 rounded focus:ring-brand-500 cursor-pointer"
                />
                <label htmlFor="modal-afip" className="text-xs font-medium text-pearl-700 cursor-pointer select-none">
                  Requiere emisión de Factura Electrónica AFIP
                </label>
              </div>
              <div className="flex gap-2 pt-2">
                <button 
                  type="button"
                  onClick={closeModal} 
                  className="flex-1 h-9 text-sm font-medium text-pearl-600 bg-pearl-100 rounded-lg hover:bg-pearl-200 transition-colors cursor-pointer"
                >
                  Cancelar
                </button>
                <button 
                  type="submit"
                  disabled={submitting || !nombre.trim()}
                  className="flex-1 h-9 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed transition-all cursor-pointer"
                >
                  {submitting ? 'Guardando...' : 'Guardar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
