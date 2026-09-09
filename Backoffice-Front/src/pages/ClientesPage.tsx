import { useState, useEffect } from 'react';
import { Users2, Phone, Mail, RefreshCw, Plus, Edit2, Trash2, X, Wallet } from 'lucide-react';
import api from '@/services/api';

interface Cliente {
  id: string;
  nombre: string;
  apellido: string;
  telefono?: string | null;
  email?: string | null;
  limiteCredito: number;
  saldoActual: number;
  isActive: boolean;
}

export default function ClientesPage() {
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [loading, setLoading] = useState(true);

  const [showModal, setShowModal] = useState(false);
  const [editingCliente, setEditingCliente] = useState<Cliente | null>(null);
  const [nombre, setNombre] = useState('');
  const [apellido, setApellido] = useState('');
  const [telefono, setTelefono] = useState('');
  const [email, setEmail] = useState('');
  const [limiteCredito, setLimiteCredito] = useState<number>(0);
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  const loadData = async () => {
    setLoading(true);
    try {
      const res = await api.get('/cliente');
      setClientes(res.data);
    } catch (err) {
      console.error('Error al cargar clientes:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const formatCurrency = (val: number) =>
    new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(val);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!nombre.trim() || !apellido.trim()) return;
    if (!telefono.trim() && !email.trim()) {
      setErrorMsg('Debe indicar al menos un teléfono o un email de contacto.');
      return;
    }

    setSubmitting(true);
    setErrorMsg('');
    try {
      const payload = {
        nombre: nombre.trim(),
        apellido: apellido.trim(),
        telefono: telefono.trim() || null,
        email: email.trim() || null,
        limiteCredito: Number(limiteCredito)
      };

      if (editingCliente) {
        await api.put(`/cliente/${editingCliente.id}`, payload);
      } else {
        await api.post('/cliente', payload);
      }
      closeModal();
      await loadData();
    } catch (err: any) {
      console.error('Error al guardar cliente:', err);
      setErrorMsg(err?.response?.data?.message || 'Error al guardar el cliente en el servidor.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(id: string) {
    if (confirm('¿Estás seguro de que deseas desactivar este cliente?')) {
      try {
        await api.delete(`/cliente/${id}`);
        await loadData();
      } catch (err) {
        console.error('Error al eliminar cliente:', err);
        alert('Error al desactivar el cliente.');
      }
    }
  }

  function openCreateModal() {
    setEditingCliente(null);
    setNombre('');
    setApellido('');
    setTelefono('');
    setEmail('');
    setLimiteCredito(0);
    setErrorMsg('');
    setShowModal(true);
  }

  function openEditModal(cliente: Cliente) {
    setEditingCliente(cliente);
    setNombre(cliente.nombre);
    setApellido(cliente.apellido);
    setTelefono(cliente.telefono || '');
    setEmail(cliente.email || '');
    setLimiteCredito(cliente.limiteCredito);
    setErrorMsg('');
    setShowModal(true);
  }

  function closeModal() {
    setShowModal(false);
    setEditingCliente(null);
    setErrorMsg('');
  }

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Clientes</h3>
          <p className="text-xs text-pearl-400">
            {clientes.filter(c => c.isActive).length} activos de {clientes.length} registrados
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
            onClick={openCreateModal}
            className="flex items-center gap-1.5 h-9 px-4 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all duration-200 cursor-pointer"
          >
            <Plus size={16} /> Nuevo Cliente
          </button>
        </div>
      </div>

      {loading && clientes.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <Users2 size={40} className="animate-pulse mb-3" />
          <p className="text-sm">Cargando clientes...</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 stagger-children">
          {clientes.map(c => (
            <div key={c.id} className={`bg-white rounded-xl border p-5 transition-all duration-300 hover:shadow-lg hover:-translate-y-0.5 group ${c.isActive ? 'border-pearl-100' : 'border-pearl-200 opacity-60'}`}>
              <div className="flex items-start justify-between mb-4">
                <div className="flex items-center gap-3">
                  <div className={`w-11 h-11 rounded-xl flex items-center justify-center ${c.isActive ? 'bg-brand-50 text-brand-600' : 'bg-pearl-100 text-pearl-400'} transition-transform group-hover:scale-110`}>
                    <Users2 size={20} />
                  </div>
                  <div>
                    <h4 className="text-sm font-bold text-pearl-900">{c.nombre} {c.apellido}</h4>
                    <div className="flex flex-col gap-0.5 mt-0.5">
                      {c.telefono && (
                        <div className="flex items-center gap-1 text-xs text-pearl-400">
                          <Phone size={11} /> {c.telefono}
                        </div>
                      )}
                      {c.email && (
                        <div className="flex items-center gap-1 text-xs text-pearl-400">
                          <Mail size={11} /> {c.email}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
                <span className={`text-[10px] font-semibold px-2.5 py-1 rounded-full ${c.isActive ? 'bg-success-50 text-success-600' : 'bg-pearl-100 text-pearl-500'}`}>
                  {c.isActive ? 'Activo' : 'Inactivo'}
                </span>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="bg-ice-50 rounded-lg p-3 text-center">
                  <p className="text-[10px] text-pearl-400 uppercase tracking-wider">Saldo Actual</p>
                  <p className={`text-sm font-bold mt-1 ${c.saldoActual > 0 ? 'text-danger-600' : 'text-pearl-700'}`}>
                    {formatCurrency(c.saldoActual)}
                  </p>
                </div>
                <div className="bg-ice-50 rounded-lg p-3 text-center">
                  <p className="text-[10px] text-pearl-400 uppercase tracking-wider">Límite Crédito</p>
                  <p className="text-sm font-semibold text-pearl-700 mt-1">
                    {c.limiteCredito > 0 ? formatCurrency(c.limiteCredito) : 'Sin límite'}
                  </p>
                </div>
              </div>

              <div className="flex items-center justify-end gap-3 mt-4 pt-3 border-t border-pearl-50">
                <button
                  onClick={() => openEditModal(c)}
                  className="text-xs text-pearl-500 hover:text-brand-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                >
                  <Edit2 size={12} /> Editar
                </button>
                {c.isActive && (
                  <button
                    onClick={() => handleDelete(c.id)}
                    className="text-xs text-pearl-500 hover:text-danger-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                  >
                    <Trash2 size={12} /> Desactivar
                  </button>
                )}
              </div>
            </div>
          ))}
          {clientes.length === 0 && !loading && (
            <div className="col-span-full bg-white rounded-xl border border-pearl-100 p-8 text-center text-pearl-400">
              No hay clientes registrados. Presiona "Nuevo Cliente" para empezar.
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
                  <Wallet size={20} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-pearl-900">
                    {editingCliente ? 'Editar Cliente' : 'Nuevo Cliente'}
                  </h4>
                  <p className="text-[11px] text-pearl-400">Datos de contacto y límite de crédito</p>
                </div>
              </div>
              <button onClick={closeModal} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label htmlFor="modal-nombre" className="block text-xs font-medium text-pearl-600 mb-1.5">Nombre</label>
                  <input
                    id="modal-nombre"
                    type="text"
                    value={nombre}
                    onChange={e => setNombre(e.target.value)}
                    placeholder="Ej: Juan"
                    required
                    className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400"
                  />
                </div>
                <div>
                  <label htmlFor="modal-apellido" className="block text-xs font-medium text-pearl-600 mb-1.5">Apellido</label>
                  <input
                    id="modal-apellido"
                    type="text"
                    value={apellido}
                    onChange={e => setApellido(e.target.value)}
                    placeholder="Ej: Pérez"
                    required
                    className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400"
                  />
                </div>
              </div>
              <div>
                <label htmlFor="modal-telefono" className="block text-xs font-medium text-pearl-600 mb-1.5">Teléfono (opcional)</label>
                <input
                  id="modal-telefono"
                  type="tel"
                  value={telefono}
                  onChange={e => setTelefono(e.target.value)}
                  placeholder="Ej: 381-1234567"
                  className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400"
                />
              </div>
              <div>
                <label htmlFor="modal-email" className="block text-xs font-medium text-pearl-600 mb-1.5">Email (opcional)</label>
                <input
                  id="modal-email"
                  type="email"
                  value={email}
                  onChange={e => setEmail(e.target.value)}
                  placeholder="Ej: juan@correo.com"
                  className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400"
                />
              </div>
              <div>
                <label htmlFor="modal-limite" className="block text-xs font-medium text-pearl-600 mb-1.5">Límite de Crédito ($)</label>
                <input
                  id="modal-limite"
                  type="number"
                  step="0.01"
                  min="0"
                  value={limiteCredito}
                  onChange={e => setLimiteCredito(Number(e.target.value))}
                  placeholder="0 = sin límite"
                  className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400"
                />
              </div>

              {errorMsg && (
                <div className="py-2 px-3 bg-danger-50 border border-danger-200 rounded-lg text-xs text-danger-600 font-medium">
                  {errorMsg}
                </div>
              )}

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
                  disabled={submitting || !nombre.trim() || !apellido.trim()}
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
