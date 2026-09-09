import { useState, useEffect } from 'react';
import { ShieldCheck, Check, Plus, Edit2, Trash2, X, RefreshCw, Globe } from 'lucide-react';
import api from '@/services/api';
import type { Rol } from '@/common/types';
import { useAuth } from '@/contexts/AuthContext';

const AVAILABLE_PERMISOS = [
  { value: 'pos.vender', label: 'Vender (POS)', description: 'Acceso a la pantalla del POS y creación de comandas.' },
  { value: 'pos.egresos', label: 'Registrar Egresos', description: 'Acceso al módulo de egresos y retiros de caja.' },
  { value: 'pos.cuentas_corrientes', label: 'Cuentas Corrientes', description: 'Acceso al módulo de cobranza y liquidación de saldos de clientes.' },
  { value: 'pos.cierre_caja', label: 'Cierre de Caja', description: 'Permite realizar y ver cierres de caja.' },
  { value: 'pos.descuentos', label: 'Aplicar Descuentos', description: 'Permite aplicar descuentos o bonificaciones en comandas.' },
  { value: 'pos.anular', label: 'Anular Comandas', description: 'Permite anular comandas o ítems de la venta.' },
  { value: 'gerente.override', label: 'Autorización de Gerente', description: 'Permite autorizar acciones críticas mediante PIN.' }
];

export default function RolesPage() {
  const { user } = useAuth();
  const [roles, setRoles] = useState<Rol[]>([]);
  const [loading, setLoading] = useState(true);

  // Modal States
  const [showModal, setShowModal] = useState(false);
  const [editingRol, setEditingRol] = useState<Rol | null>(null);
  const [nombre, setNombre] = useState('');
  const [selectedPermisos, setSelectedPermisos] = useState<string[]>([]);
  const [esGlobal, setEsGlobal] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const loadRoles = async () => {
    setLoading(true);
    try {
      const res = await api.get('/rol');
      setRoles(res.data || []);
    } catch (err) {
      console.error('Error al cargar roles:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRoles();
  }, []);

  function handleCheckboxChange(value: string) {
    if (selectedPermisos.includes(value)) {
      setSelectedPermisos(selectedPermisos.filter(p => p !== value));
    } else {
      setSelectedPermisos([...selectedPermisos, value]);
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!nombre.trim()) return;
    setSubmitting(true);

    try {
      if (editingRol) {
        await api.put(`/rol/${editingRol.id}`, { nombre: nombre.trim(), permisos: selectedPermisos, esGlobal });
      } else {
        await api.post('/rol', { nombre: nombre.trim(), permisos: selectedPermisos, esGlobal });
      }
      closeModal();
      await loadRoles();
    } catch (err) {
      console.error('Error al guardar el rol:', err);
      alert('Error al guardar el rol en el servidor.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(id: string) {
    if (confirm('¿Estás seguro de que deseas eliminar este rol?')) {
      try {
        await api.delete(`/rol/${id}`);
        await loadRoles();
      } catch (err) {
        console.error('Error al eliminar rol:', err);
        alert('Error al desactivar/eliminar el rol.');
      }
    }
  }

  function openCreateModal() {
    setEditingRol(null);
    setNombre('');
    setSelectedPermisos([]);
    setEsGlobal(false);
    setShowModal(true);
  }

  function openEditModal(rol: Rol) {
    setEditingRol(rol);
    setNombre(rol.nombre);
    setSelectedPermisos(rol.permisos || []);
    setEsGlobal(rol.esGlobal || false);
    setShowModal(true);
  }

  function closeModal() {
    setShowModal(false);
    setEditingRol(null);
    setNombre('');
    setSelectedPermisos([]);
    setEsGlobal(false);
  }

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Roles de Acceso</h3>
          <p className="text-xs text-pearl-400">
            {roles.filter(r => r.isActive).length} roles activos para el personal del POS
          </p>
        </div>
        <div className="flex gap-2">
          <button 
            onClick={loadRoles}
            className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 rounded-lg hover:bg-ice-50 active:scale-[0.98] transition-all cursor-pointer"
            title="Refrescar"
          >
            <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
          </button>
          <button 
            id="add-rol-btn" 
            onClick={openCreateModal}
            className="flex items-center gap-1.5 h-9 px-4 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all duration-200 cursor-pointer"
          >
            <Plus size={16} /> Nuevo Rol
          </button>
        </div>
      </div>

      {loading && roles.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <ShieldCheck size={40} className="animate-pulse mb-3" />
          <p className="text-sm">Cargando roles...</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 stagger-children">
          {roles.map(r => (
            <div key={r.id} className={`bg-white rounded-xl border p-5 transition-all duration-300 hover:shadow-lg hover:-translate-y-0.5 group ${r.isActive ? 'border-pearl-100' : 'border-pearl-200 opacity-60'}`}>
              <div className="flex items-start justify-between mb-4">
                <div className="flex items-center gap-3">
                  <div className={`w-11 h-11 rounded-xl flex items-center justify-center ${r.isActive ? 'bg-brand-50 text-brand-600' : 'bg-pearl-100 text-pearl-400'} transition-transform group-hover:scale-110`}>
                    <ShieldCheck size={20} />
                  </div>
                  <div>
                    <div className="flex items-center gap-1.5">
                      <h4 className="text-sm font-bold text-pearl-900">{r.nombre}</h4>
                      {r.esGlobal && (
                        <span className="inline-flex items-center gap-1 text-[10px] font-semibold bg-brand-50 text-brand-700 px-1.5 py-0.5 rounded" title="Alcance global: todas las sucursales">
                          <Globe size={10} /> Global
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-pearl-400 mt-0.5">
                      {r.permisos?.length || 0} permisos asignados
                    </p>
                  </div>
                </div>
                <span className={`text-[10px] font-semibold px-2.5 py-1 rounded-full ${r.isActive ? 'bg-success-50 text-success-600' : 'bg-pearl-100 text-pearl-500'}`}>
                  {r.isActive ? 'Activo' : 'Inactivo'}
                </span>
              </div>

              {/* Permisos como badges */}
              <div className="flex flex-wrap gap-1.5 min-h-[40px] mb-4">
                {r.permisos && r.permisos.length > 0 ? (
                  r.permisos.map(p => {
                    const info = AVAILABLE_PERMISOS.find(ap => ap.value === p);
                    return (
                      <span 
                        key={p} 
                        className="text-[10px] font-medium bg-ice-100 text-pearl-700 px-2 py-0.5 rounded"
                        title={info?.description}
                      >
                        {info?.label || p}
                      </span>
                    );
                  })
                ) : (
                  <span className="text-[11px] text-pearl-400 italic">Sin permisos asignados (sólo ver comandos públicos)</span>
                )}
              </div>

              <div className="flex items-center justify-end gap-3 pt-3 border-t border-pearl-50">
                <button 
                  onClick={() => openEditModal(r)}
                  className="text-xs text-pearl-500 hover:text-brand-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                >
                  <Edit2 size={12} /> Editar
                </button>
                {r.isActive && r.nombre !== 'Administrador' && (
                  <button 
                    onClick={() => handleDelete(r.id)}
                    className="text-xs text-pearl-500 hover:text-danger-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                  >
                    <Trash2 size={12} /> Desactivar
                  </button>
                )}
              </div>
            </div>
          ))}
          {roles.length === 0 && !loading && (
            <div className="col-span-2 bg-white rounded-xl border border-pearl-100 p-8 text-center text-pearl-400">
              No hay roles de acceso creados. Presiona "Nuevo Rol" para empezar.
            </div>
          )}
        </div>
      )}

      {/* Modal Crear / Editar */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={closeModal}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-lg mx-4 p-6 animate-fade-in max-h-[90vh] flex flex-col" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-5 shrink-0">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
                  <ShieldCheck size={20} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-pearl-900">
                    {editingRol ? 'Editar Rol de Acceso' : 'Nuevo Rol de Acceso'}
                  </h4>
                  <p className="text-[11px] text-pearl-400">Asigna permisos y nombre al rol</p>
                </div>
              </div>
              <button onClick={closeModal} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="flex-1 flex flex-col overflow-hidden">
              <div className="flex-1 overflow-y-auto space-y-4 pr-1 mb-4">
                <div>
                  <label htmlFor="modal-nombre-rol" className="block text-xs font-medium text-pearl-600 mb-1.5">Nombre del Rol</label>
                  <input 
                    id="modal-nombre-rol" 
                    type="text" 
                    value={nombre} 
                    onChange={e => setNombre(e.target.value)} 
                    placeholder="Ej: Mozo de Salón, Cajero, Encargado" 
                    required
                    className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400" 
                  />
                </div>

                {user?.esGlobal && (
                  <div
                    onClick={() => setEsGlobal(!esGlobal)}
                    className={`flex items-start gap-3 p-2.5 rounded-lg border cursor-pointer transition-all ${
                      esGlobal
                        ? 'bg-brand-50/40 border-brand-200'
                        : 'bg-white border-pearl-100 hover:bg-ice-50'
                    }`}
                  >
                    <div className={`mt-0.5 w-4.5 h-4.5 rounded border flex items-center justify-center shrink-0 transition-all ${
                      esGlobal
                        ? 'bg-brand-600 border-brand-600 text-white'
                        : 'bg-ice-50 border-pearl-200'
                    }`}>
                      {esGlobal && <Check size={12} strokeWidth={3} />}
                    </div>
                    <div>
                      <p className="text-xs font-semibold text-pearl-800 flex items-center gap-1"><Globe size={12} /> Rol con alcance global</p>
                      <p className="text-[10px] text-pearl-400 mt-0.5">Los usuarios con este rol pueden operar y ver datos de todas las sucursales, no solo la propia.</p>
                    </div>
                  </div>
                )}

                <div>
                  <label className="block text-xs font-medium text-pearl-600 mb-2">Permisos Disponibles</label>
                  <div className="space-y-2.5">
                    {AVAILABLE_PERMISOS.map(ap => {
                      const isChecked = selectedPermisos.includes(ap.value);
                      return (
                        <div 
                          key={ap.value}
                          onClick={() => handleCheckboxChange(ap.value)}
                          className={`flex items-start gap-3 p-2.5 rounded-lg border cursor-pointer transition-all ${
                            isChecked 
                              ? 'bg-brand-50/40 border-brand-200' 
                              : 'bg-white border-pearl-100 hover:bg-ice-50'
                          }`}
                        >
                          <div className={`mt-0.5 w-4.5 h-4.5 rounded border flex items-center justify-center shrink-0 transition-all ${
                            isChecked 
                              ? 'bg-brand-600 border-brand-600 text-white' 
                              : 'bg-ice-50 border-pearl-200'
                          }`}>
                            {isChecked && <Check size={12} strokeWidth={3} />}
                          </div>
                          <div>
                            <p className="text-xs font-semibold text-pearl-800">{ap.label}</p>
                            <p className="text-[10px] text-pearl-400 mt-0.5">{ap.description}</p>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              </div>

              <div className="flex gap-2 pt-2 border-t border-pearl-100 shrink-0">
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
