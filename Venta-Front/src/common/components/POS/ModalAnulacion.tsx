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
      style={{ backgroundColor: 'rgba(8, 9, 11, 0.55)' }}>
      <div className="w-full max-w-sm mx-4 rounded-[14px] bg-surface-base border border-border-default shadow-modal animate-modal-content overflow-hidden">
        <div className="flex items-center gap-3 p-5 border-b border-border-default">
          <Ban className="w-4 h-4 text-danger-500 flex-shrink-0" />
          <h2 className="text-[14.5px] font-semibold text-text-primary">{titulo}</h2>
        </div>
        <div className="p-5">
          {descripcion && <p className="text-sm text-text-secondary mb-4 leading-relaxed">{descripcion}</p>}

          <label className="mono-label block mb-2">
            Motivo de anulación <span className="text-danger-500">*</span>
          </label>
          <textarea
            value={motivo}
            onChange={e => { setMotivo(e.target.value); setError(''); }}
            disabled={loading}
            rows={3}
            placeholder="Ej: pedido duplicado, error de carga, cliente se retiró..."
            className="w-full bg-surface-overlay border border-border-default rounded-[var(--radius-input)] p-3 text-sm text-text-primary placeholder:text-text-muted mb-4 resize-none
              focus:outline-none focus:border-amber-500 disabled:opacity-50"
          />

          {error && (
            <div className="mb-4 py-2 px-3 border border-danger-500/40 rounded-[var(--radius-btn)] text-center animate-fade-in">
              <span className="text-xs font-semibold text-danger-500">{error}</span>
            </div>
          )}
        </div>

        <div className="flex justify-end gap-2 p-4 border-t border-border-default">
          <button type="button" onClick={onCancelar} disabled={loading}
            className="touch-btn h-14 px-5 rounded-[var(--radius-btn)] bg-surface-base border border-border-default text-[13px] font-medium text-text-secondary hover:text-text-primary min-h-0 disabled:opacity-50 disabled:cursor-not-allowed">
            Cancelar
          </button>
          <button type="button" onClick={handleConfirmar} disabled={loading || motivo.trim().length === 0}
            className="touch-btn h-14 px-6 rounded-[var(--radius-btn)] bg-danger-500 border border-danger-500 text-[13px] font-semibold text-white hover:bg-danger-600 min-h-0 disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center gap-1.5">
            {loading && <span className="w-3.5 h-3.5 border-2 border-white/60 border-t-transparent rounded-full animate-spin" />}
            {loading ? 'Anulando...' : 'Confirmar Anulación'}
          </button>
        </div>
      </div>
    </div>
  );
};
