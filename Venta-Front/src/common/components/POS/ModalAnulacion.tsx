import { useState } from 'react';
import { Ban } from 'lucide-react';

interface ModalAnulacionProps {
  titulo: string;
  descripcion?: string;
  onConfirmar: (motivo: string) => Promise<void>;
  onCancelar: () => void;
}

export const ModalAnulacion = ({ titulo, descripcion, onConfirmar, onCancelar }: ModalAnulacionProps) => {
  const [motivo, setMotivo] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleConfirmar = async () => {
    if (motivo.trim().length === 0) {
      setError('El motivo de anulación es obligatorio.');
      return;
    }
    try {
      setLoading(true);
      setError('');
      await onConfirmar(motivo.trim());
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || 'No se pudo anular. Intente nuevamente.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[51] flex items-center justify-center animate-modal-backdrop"
      style={{ backgroundColor: 'rgba(0, 0, 0, 0.7)', backdropFilter: 'blur(10px)' }}>
      <div className="w-full max-w-sm mx-4 p-6 rounded-[var(--radius-card)] bg-slate-900 border border-border-strong shadow-[var(--shadow-modal)] animate-modal-content">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-[var(--radius-btn)] bg-danger-500/10 border border-danger-500/25 flex items-center justify-center flex-shrink-0">
            <Ban className="w-5 h-5 text-danger-400" />
          </div>
          <h2 className="text-base font-bold text-text-primary">{titulo}</h2>
        </div>
        {descripcion && <p className="text-sm text-text-secondary mb-4 leading-relaxed">{descripcion}</p>}

        <label className="block text-[10px] font-bold uppercase tracking-wider text-text-muted mb-2">
          Motivo de anulación <span className="text-danger-500">*</span>
        </label>
        <textarea
          value={motivo}
          onChange={e => { setMotivo(e.target.value); setError(''); }}
          disabled={loading}
          rows={3}
          placeholder="Ej: pedido duplicado, error de carga, cliente se retiró..."
          className="w-full bg-slate-950/60 border border-border-default rounded-[var(--radius-input)] p-3 text-sm text-text-primary placeholder:text-text-muted mb-4 resize-none
            focus:outline-none focus:border-amber-500/40 focus:ring-1 focus:ring-amber-500/20 transition-all duration-150 disabled:opacity-50"
        />

        {error && (
          <div className="mb-4 py-2 px-3 bg-danger-500/5 border border-danger-500/20 rounded-[var(--radius-btn)] text-center animate-fade-in">
            <span className="text-xs font-semibold text-danger-400">{error}</span>
          </div>
        )}

        <div className="flex gap-2.5">
          <button type="button" onClick={onCancelar} disabled={loading}
            className="touch-btn flex-1 py-3 rounded-[var(--radius-btn)] bg-slate-900 border border-border-default text-sm font-bold text-text-secondary hover:text-text-primary hover:bg-slate-850 active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed transition-all">
            Cancelar
          </button>
          <button type="button" onClick={handleConfirmar} disabled={loading || motivo.trim().length === 0}
            className="touch-btn flex-1 py-3 rounded-[var(--radius-btn)] bg-danger-600 text-sm font-bold text-white hover:bg-danger-500 active:scale-[0.98] disabled:opacity-40 disabled:cursor-not-allowed transition-all shadow-sm flex items-center justify-center gap-1.5">
            {loading && <span className="w-3.5 h-3.5 border-2 border-white/60 border-t-transparent rounded-full animate-spin" />}
            {loading ? 'Anulando...' : 'Confirmar Anulación'}
          </button>
        </div>
      </div>
    </div>
  );
};
