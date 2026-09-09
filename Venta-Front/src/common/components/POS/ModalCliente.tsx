import { useState, useMemo } from 'react';
import { X, Search, Plus, ArrowLeft, UserRound } from 'lucide-react';
import type { Cliente } from '@/common/types';
import api from '@/services/api';

interface ModalClienteProps {
  onSeleccionar: (cliente: Cliente) => void;
  onCancelar: () => void;
}

function formatARS(monto: number): string {
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(monto);
}

export const ModalCliente = ({ onSeleccionar, onCancelar }: ModalClienteProps) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [loading, setLoading] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({ nombre: '', apellido: '', telefono: '', email: '', limiteCredito: 0 });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  // Cargar clientes al montar
  useState(() => {
    (async () => {
      try {
        setLoading(true);
        const res = await api.get('/cliente');
        setClientes(res.data);
      } catch (err) {
        setError('Error al cargar clientes');
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const filtered = useMemo(() => {
    if (!searchTerm.trim()) return clientes;
    const q = searchTerm.toLowerCase();
    return clientes.filter(c =>
      c.nombre.toLowerCase().includes(q) ||
      c.apellido.toLowerCase().includes(q)
    );
  }, [searchTerm, clientes]);

  const handleCrear = async () => {
    if (!form.nombre.trim() || !form.apellido.trim()) {
      setError('Nombre y apellido obligatorios');
      return;
    }
    if (!form.telefono.trim() && !form.email.trim()) {
      setError('Indicar teléfono o email');
      return;
    }

    try {
      setSubmitting(true);
      setError('');
      const res = await api.post('/cliente', form);
      const nuevoCliente: Cliente = res.data;
      setClientes([...clientes, nuevoCliente]);
      onSeleccionar(nuevoCliente);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Error al crear cliente');
    } finally {
      setSubmitting(false);
    }
  };

  const inputClass = "w-full px-3.5 h-11 bg-slate-950/60 border border-border-default rounded-[var(--radius-input)] text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-amber-500/40 focus:ring-1 focus:ring-amber-500/20 transition-all duration-150";

  return (
    <div className="fixed inset-0 z-[52] flex items-center justify-center animate-modal-backdrop"
      style={{ backgroundColor: 'rgba(0, 0, 0, 0.7)', backdropFilter: 'blur(10px)' }}>
      <div className="w-full max-w-lg mx-4 max-h-[85vh] rounded-[var(--radius-card)] bg-slate-900 border border-border-strong shadow-[var(--shadow-modal)] animate-modal-content flex flex-col overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b border-border-default flex-shrink-0">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-[var(--radius-btn)] bg-cyan-500/10 border border-cyan-500/25 flex items-center justify-center">
              <UserRound className="w-4 h-4 text-cyan-500" />
            </div>
            <h2 className="text-base font-bold text-text-primary">
              {showForm ? 'Nuevo Cliente' : 'Seleccionar Cliente'}
            </h2>
          </div>
          <button onClick={onCancelar}
            className="touch-btn p-1.5 rounded-[var(--radius-btn)] bg-slate-950/60 text-text-muted hover:text-text-primary hover:bg-slate-850 border border-border-default/60 transition-all">
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Contenido */}
        {!showForm ? (
          <>
            {/* Búsqueda */}
            <div className="p-4 border-b border-border-default flex-shrink-0">
              <div className="relative">
                <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-text-muted" />
                <input
                  type="text"
                  placeholder="Buscar por nombre..."
                  value={searchTerm}
                  onChange={e => setSearchTerm(e.target.value)}
                  className={`${inputClass} pl-10`}
                />
              </div>
            </div>

            {/* Lista */}
            <div className="flex-1 overflow-y-auto p-3 min-h-[180px]">
              {loading ? (
                <div className="h-full flex flex-col items-center justify-center gap-2 py-8 text-text-muted">
                  <div className="w-6 h-6 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
                  <span className="text-xs font-semibold">Cargando clientes...</span>
                </div>
              ) : error && !showForm ? (
                <p className="text-danger-400 text-center py-8 text-sm font-semibold">{error}</p>
              ) : filtered.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-8 text-text-muted">
                  <UserRound className="w-8 h-8 mb-2 opacity-30" />
                  <p className="text-sm font-semibold text-text-secondary">No hay clientes</p>
                </div>
              ) : (
                <div className="space-y-2">
                  {filtered.map(c => (
                    <button
                      key={c.id}
                      onClick={() => onSeleccionar(c)}
                      className="touch-btn w-full p-3 bg-slate-950/50 hover:bg-slate-850 border border-border-default/60 hover:border-border-strong rounded-[var(--radius-btn)] text-left transition-all active:scale-[0.99]"
                    >
                      <div className="font-semibold text-sm text-text-primary">{c.nombre} {c.apellido}</div>
                      <div className="text-xs text-text-muted mt-0.5">Saldo: {formatARS(c.saldoActual)}</div>
                    </button>
                  ))}
                </div>
              )}
            </div>

            {/* Botón crear */}
            <div className="p-3 border-t border-border-default flex-shrink-0">
              <button
                onClick={() => { setShowForm(true); setError(''); }}
                className="touch-btn w-full flex items-center justify-center gap-2 h-11 rounded-[var(--radius-btn)] bg-cyan-600 hover:bg-cyan-500 text-white text-sm font-bold transition-all active:scale-[0.98] shadow-sm"
              >
                <Plus className="w-4 h-4" /> Nuevo Cliente
              </button>
            </div>
          </>
        ) : (
          <>
            {/* Formulario */}
            <div className="flex-1 overflow-y-auto p-4 space-y-3">
              {error && (
                <div className="py-2 px-3 bg-danger-500/5 border border-danger-500/20 rounded-[var(--radius-btn)] text-center animate-fade-in">
                  <span className="text-xs font-semibold text-danger-400">{error}</span>
                </div>
              )}
              <input
                type="text"
                placeholder="Nombre"
                value={form.nombre}
                onChange={e => setForm({ ...form, nombre: e.target.value })}
                className={inputClass}
              />
              <input
                type="text"
                placeholder="Apellido"
                value={form.apellido}
                onChange={e => setForm({ ...form, apellido: e.target.value })}
                className={inputClass}
              />
              <input
                type="tel"
                placeholder="Teléfono (opcional)"
                value={form.telefono}
                onChange={e => setForm({ ...form, telefono: e.target.value })}
                className={inputClass}
              />
              <input
                type="email"
                placeholder="Email (opcional)"
                value={form.email}
                onChange={e => setForm({ ...form, email: e.target.value })}
                className={inputClass}
              />
            </div>

            {/* Botones */}
            <div className="p-3 border-t border-border-default flex gap-2 flex-shrink-0">
              <button
                onClick={() => { setShowForm(false); setError(''); }}
                disabled={submitting}
                className="touch-btn flex-1 h-11 rounded-[var(--radius-btn)] bg-slate-900 border border-border-default text-text-secondary hover:text-text-primary hover:bg-slate-850 text-sm font-bold transition-all active:scale-[0.98] disabled:opacity-50 flex items-center justify-center gap-1.5"
              >
                <ArrowLeft className="w-4 h-4" /> Volver
              </button>
              <button
                onClick={handleCrear}
                disabled={submitting}
                className="touch-btn flex-1 h-11 rounded-[var(--radius-btn)] bg-cyan-600 hover:bg-cyan-500 text-white text-sm font-bold transition-all active:scale-[0.98] disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center gap-1.5 shadow-sm"
              >
                {submitting && <span className="w-3.5 h-3.5 border-2 border-white/60 border-t-transparent rounded-full animate-spin" />}
                {submitting ? 'Creando...' : 'Crear'}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
};
