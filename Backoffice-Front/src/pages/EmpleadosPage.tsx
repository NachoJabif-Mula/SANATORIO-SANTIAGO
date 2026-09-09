import { useState, useEffect } from 'react';
import { Users, Eye, EyeOff, Plus, Edit2, Trash2, X, RefreshCw, Key, ShieldCheck, Building } from 'lucide-react';
import api from '@/services/api';
import type { Empleado, Rol, Sucursal } from '@/common/types';
import { useAuth } from '@/contexts/AuthContext';
import { useSucursal } from '@/contexts/SucursalContext';

export default function EmpleadosPage() {
  const { user } = useAuth();
  const { selectedSucursalId, isGlobal } = useSucursal();
  const [empleados, setEmpleados] = useState<Empleado[]>([]);
  const [roles, setRoles] = useState<Rol[]>([]);
  const [sucursales, setSucursales] = useState<Sucursal[]>([]);
  const [loading, setLoading] = useState(true);

  // Visibility states for PINs
  const [visiblePins, setVisiblePins] = useState<Record<string, boolean>>({});

  // Modal States
  const [showModal, setShowModal] = useState(false);
  const [editingEmpleado, setEditingEmpleado] = useState<Empleado | null>(null);
  const [nombre, setNombre] = useState('');
  const [email, setEmail] = useState('');
  const [pinAcceso, setPinAcceso] = useState('');
  const [rolId, setRolId] = useState('');
  const [sucursalId, setSucursalId] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const loadData = async () => {
    setLoading(true);
    try {
      const empleadoParams = isGlobal && selectedSucursalId ? { sucursalId: selectedSucursalId } : {};
      const [empRes, rolRes, sucRes] = await Promise.all([
        api.get('/empleado', { params: empleadoParams }),
        api.get('/rol'),
        api.get('/sucursal')
      ]);
      setEmpleados(empRes.data || []);
      setRoles((rolRes.data || []).filter((r: Rol) => r.isActive));
      setSucursales((sucRes.data || []).filter((s: Sucursal) => s.isActive));
    } catch (err) {
      console.error('Error al cargar datos:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [selectedSucursalId, isGlobal]);

  function togglePinVisibility(id: string) {
    setVisiblePins(prev => ({
      ...prev,
      [id]: !prev[id]
    }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!nombre.trim() || !pinAcceso.trim() || !rolId || !sucursalId) {
      alert('Por favor complete los campos obligatorios.');
      return;
    }
    setSubmitting(true);

    const payload = {
      nombre: nombre.trim(),
      email: email.trim() || null,
      pinAcceso: pinAcceso.trim(),
      rolId,
      sucursalId,
      password: password.trim() || null
    };

    try {
      if (editingEmpleado) {
        await api.put(`/empleado/${editingEmpleado.id}`, payload);
      } else {
        await api.post('/empleado', payload);
      }
      closeModal();
      await loadData();
    } catch (err: any) {
      console.error('Error al guardar empleado:', err);
      const msg = err.response?.data?.message || 'Error al guardar el empleado en el servidor.';
      alert(msg);
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(id: string) {
    if (confirm('¿Estás seguro de que deseas desactivar este empleado?')) {
      try {
        await api.delete(`/empleado/${id}`);
        await loadData();
      } catch (err: any) {
        console.error('Error al desactivar empleado:', err);
        const msg = err.response?.data?.message || 'Error al desactivar el empleado.';
        alert(msg);
      }
    }
  }

  function openCreateModal() {
    setEditingEmpleado(null);
    setNombre('');
    setEmail('');
    setPinAcceso('');
    setRolId(roles[0]?.id || '');
    setSucursalId(isGlobal ? (sucursales[0]?.id || '') : (user?.sucursalId || ''));
    setPassword('');
    setShowModal(true);
  }

  function openEditModal(empleado: Empleado) {
    setEditingEmpleado(empleado);
    setNombre(empleado.nombre);
    setEmail(empleado.email || '');
    setPinAcceso(empleado.pinAcceso);
    setRolId(empleado.rolId);
    setSucursalId(empleado.sucursalId);
    setPassword('');
    setShowModal(true);
  }

  function closeModal() {
    setShowModal(false);
    setEditingEmpleado(null);
    setNombre('');
    setEmail('');
    setPinAcceso('');
    setRolId('');
    setSucursalId('');
    setPassword('');
  }

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Gestión de Empleados</h3>
          <p className="text-xs text-pearl-400">
            {empleados.filter(e => e.isActive).length} empleados activos en todas las sucursales
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
            id="add-empleado-btn" 
            onClick={openCreateModal}
            disabled={roles.length === 0 || sucursales.length === 0}
            className="flex items-center gap-1.5 h-9 px-4 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
            title={roles.length === 0 || sucursales.length === 0 ? 'Debes crear al menos un Rol y una Sucursal primero' : ''}
          >
            <Plus size={16} /> Nuevo Empleado
          </button>
        </div>
      </div>

      {loading && empleados.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <Users size={40} className="animate-pulse mb-3" />
          <p className="text-sm">Cargando empleados...</p>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-pearl-100 overflow-hidden shadow-card">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-ice-50/50 border-b border-pearl-100 text-[11px] font-bold text-pearl-500 uppercase tracking-wider">
                  <th className="px-6 py-4">Nombre / Email</th>
                  <th className="px-6 py-4">Sucursal</th>
                  <th className="px-6 py-4">Rol</th>
                  <th className="px-6 py-4">PIN Acceso</th>
                  <th className="px-6 py-4 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-pearl-50 text-sm">
                {empleados.map(e => {
                  const isPinVisible = visiblePins[e.id] || false;
                  return (
                    <tr key={e.id} className="hover:bg-ice-25/50 transition-colors">
                      <td className="px-6 py-4">
                        <div className="font-semibold text-pearl-900">{e.nombre}</div>
                        <div className="text-xs text-pearl-400">{e.email || 'Sin correo asociado'}</div>
                      </td>
                      <td className="px-6 py-4 text-pearl-600">
                        <div className="flex items-center gap-1.5">
                          <Building size={14} className="text-pearl-400" />
                          {e.sucursalNombre}
                        </div>
                      </td>
                      <td className="px-6 py-4 text-pearl-700">
                        <span className="inline-flex items-center gap-1 bg-brand-50 text-brand-700 text-xs font-semibold px-2.5 py-0.5 rounded-full">
                          <ShieldCheck size={12} />
                          {e.rolNombre}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2">
                          <span className="font-mono text-pearl-800 tracking-wider">
                            {isPinVisible ? e.pinAcceso : '••••'}
                          </span>
                          <button 
                            type="button"
                            onClick={() => togglePinVisibility(e.id)} 
                            className="text-pearl-400 hover:text-pearl-600 cursor-pointer"
                          >
                            {isPinVisible ? <EyeOff size={14} /> : <Eye size={14} />}
                          </button>
                        </div>
                      </td>
                      <td className="px-6 py-4 text-right">
                        <div className="flex items-center justify-end gap-3">
                          <button 
                            onClick={() => openEditModal(e)}
                            className="text-xs text-pearl-500 hover:text-brand-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                          >
                            <Edit2 size={12} /> Editar
                          </button>
                          {e.isActive && e.email !== 'admin@baresfamilia.com' && (
                            <button 
                              onClick={() => handleDelete(e.id)}
                              className="text-xs text-pearl-500 hover:text-danger-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                            >
                              <Trash2 size={12} /> Desactivar
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
                {empleados.length === 0 && !loading && (
                  <tr>
                    <td colSpan={5} className="px-6 py-12 text-center text-pearl-400 italic">
                      No hay empleados registrados. Presiona "Nuevo Empleado" para crearlo.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Modal Crear / Editar */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={closeModal}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-md mx-4 p-6 animate-fade-in max-h-[90vh] flex flex-col" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-5 shrink-0">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
                  <Users size={20} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-pearl-900">
                    {editingEmpleado ? 'Editar Empleado' : 'Nuevo Empleado'}
                  </h4>
                  <p className="text-[11px] text-pearl-400">Datos personales, PIN y permisos</p>
                </div>
              </div>
              <button onClick={closeModal} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="flex-1 flex flex-col overflow-hidden">
              <div className="flex-1 overflow-y-auto space-y-4 pr-1 mb-4">
                <div>
                  <label htmlFor="modal-nombre-emp" className="block text-xs font-medium text-pearl-600 mb-1.5">Nombre Completo *</label>
                  <input 
                    id="modal-nombre-emp" 
                    type="text" 
                    value={nombre} 
                    onChange={e => setNombre(e.target.value)} 
                    placeholder="Ej: Sofía Gómez" 
                    required
                    className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400" 
                  />
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label htmlFor="modal-rol" className="block text-xs font-medium text-pearl-600 mb-1.5">Rol *</label>
                    <select
                      id="modal-rol"
                      value={rolId}
                      onChange={e => setRolId(e.target.value)}
                      required
                      className="w-full h-9 px-2 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 text-pearl-800"
                    >
                      {roles.map(r => (
                        <option key={r.id} value={r.id}>{r.nombre}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label htmlFor="modal-sucursal" className="block text-xs font-medium text-pearl-600 mb-1.5">Sucursal *</label>
                    {isGlobal ? (
                      <select
                        id="modal-sucursal"
                        value={sucursalId}
                        onChange={e => setSucursalId(e.target.value)}
                        required
                        className="w-full h-9 px-2 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 text-pearl-800"
                      >
                        {sucursales.map(s => (
                          <option key={s.id} value={s.id}>{s.nombre}</option>
                        ))}
                      </select>
                    ) : (
                      <div className="w-full h-9 px-3 flex items-center text-sm bg-ice-100 border border-pearl-200 rounded-lg text-pearl-500">
                        {user?.sucursal}
                      </div>
                    )}
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label htmlFor="modal-pin" className="block text-xs font-medium text-pearl-600 mb-1.5">PIN POS (4 dígitos) *</label>
                    <input 
                      id="modal-pin" 
                      type="text" 
                      pattern="[0-9]{4}"
                      maxLength={4}
                      value={pinAcceso} 
                      onChange={e => setPinAcceso(e.target.value.replace(/\D/g, ''))} 
                      placeholder="Ej: 1234" 
                      required
                      className="w-full h-9 px-3 text-sm font-mono tracking-widest bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400 text-center" 
                    />
                  </div>
                  <div>
                    <label htmlFor="modal-email" className="block text-xs font-medium text-pearl-600 mb-1.5">Email (Opcional)</label>
                    <input 
                      id="modal-email" 
                      type="email" 
                      value={email} 
                      onChange={e => setEmail(e.target.value)} 
                      placeholder="sofia@baresfamilia.com" 
                      className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400" 
                    />
                  </div>
                </div>

                <div className="border-t border-pearl-100 pt-3">
                  <div className="flex items-center gap-1.5 mb-2 text-pearl-700">
                    <Key size={14} className="text-pearl-500" />
                    <span className="text-xs font-bold">Acceso a Backoffice</span>
                  </div>
                  <div>
                    <label htmlFor="modal-pass" className="block text-xs font-medium text-pearl-600 mb-1">
                      {editingEmpleado ? 'Nueva Contraseña (Dejar vacío para no cambiar)' : 'Contraseña de Backoffice'}
                    </label>
                    <input 
                      id="modal-pass" 
                      type="password" 
                      value={password} 
                      onChange={e => setPassword(e.target.value)} 
                      placeholder={editingEmpleado ? 'Vacío = Sin cambios' : 'Mínimo 6 caracteres'} 
                      minLength={editingEmpleado ? undefined : 6}
                      className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400" 
                    />
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
                  disabled={submitting || !nombre.trim() || !pinAcceso.trim() || pinAcceso.length < 4 || !rolId || !sucursalId}
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
