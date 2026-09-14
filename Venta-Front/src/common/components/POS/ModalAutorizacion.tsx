import { useState } from 'react';
import { Lock } from 'lucide-react';
import api from '@/services/api';

interface ModalAutorizacionProps {
  onConfirmar: () => void;
  onCancelar: () => void;
  mensaje?: string;
}

export const ModalAutorizacion = ({ onConfirmar, onCancelar, mensaje }: ModalAutorizacionProps) => {
  const [pin, setPin] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleNumpadPress = (val: string) => {
    setError('');
    if (val === 'C') {
      setPin('');
    } else if (val === '←') {
      setPin(prev => prev.slice(0, -1));
    } else if (pin.length < 6) {
      setPin(prev => prev + val);
    }
  };

  const handleConfirmar = async () => {
    if (pin.length < 4) {
      setError('El PIN debe tener al menos 4 dígitos');
      return;
    }

    try {
      setLoading(true);
      setError('');
      await api.post('/usuario/validar-gerente', { pin });
      onConfirmar();
    } catch (err: any) {
      setError(err.response?.data?.message || 'PIN inválido o no autorizado');
      setPin('');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[51] flex items-center justify-center animate-modal-backdrop"
      style={{ backgroundColor: 'rgba(8, 9, 11, 0.55)' }}>
      <div className="w-full max-w-sm mx-4 rounded-[14px] bg-surface-base border border-border-default shadow-modal animate-modal-content overflow-hidden">
        <div className="flex flex-col items-center gap-2 p-5 pb-0 text-center">
          <Lock className="w-5 h-5 text-amber-500" />
          <div>
            <h2 className="text-[14.5px] font-semibold text-text-primary">Autorización Gerente</h2>
            <p className="text-sm text-text-secondary mt-1">{mensaje || 'Ingrese su PIN de gerente para autorizar esta operación'}</p>
          </div>
        </div>

        <div className="p-5">
          <div className="bg-surface-overlay border border-border-default rounded-[var(--radius-input)] py-4 mb-4 flex items-center justify-center gap-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className={`w-3 h-3 rounded-full border ${
                i < pin.length ? 'bg-amber-500 border-amber-500' : 'border-border-default bg-transparent'
              }`} />
            ))}
          </div>

          {error && (
            <div className="mb-4 py-2 px-3 border border-danger-500/40 rounded-[var(--radius-btn)] text-center animate-shake">
              <span className="text-xs font-semibold text-danger-500">{error}</span>
            </div>
          )}

          <div className="grid grid-cols-3 gap-2 font-mono">
            {['1', '2', '3', '4', '5', '6', '7', '8', '9', 'C', '0', '←'].map(k => (
              <button
                key={k}
                type="button"
                onClick={() => handleNumpadPress(k)}
                disabled={loading}
                className={`touch-btn h-12 rounded-[var(--radius-btn)] font-semibold text-base select-none active:scale-95 border disabled:opacity-40 disabled:cursor-not-allowed ${
                  k === 'C'
                    ? 'bg-surface-base text-danger-500 border-border-default hover:border-danger-500'
                    : k === '←'
                    ? 'bg-surface-base text-text-secondary border-border-default'
                    : 'bg-surface-base text-text-primary border-border-default hover:border-amber-500'
                }`}
              >
                {k}
              </button>
            ))}
          </div>
        </div>

        <div className="flex justify-end gap-2 p-4 border-t border-border-default">
          <button type="button" onClick={onCancelar} disabled={loading}
            className="touch-btn h-14 px-5 rounded-[var(--radius-btn)] bg-surface-base border border-border-default text-[13px] font-medium text-text-secondary hover:text-text-primary min-h-0 disabled:opacity-50">
            Cancelar
          </button>
          <button type="button" onClick={handleConfirmar} disabled={loading || pin.length < 4}
            className="touch-btn h-14 px-6 rounded-[var(--radius-btn)] bg-amber-500 border border-amber-500 text-[13px] font-semibold text-white hover:bg-amber-600 min-h-0 disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center gap-1.5">
            {loading && <span className="w-3.5 h-3.5 border-2 border-white/60 border-t-transparent rounded-full animate-spin" />}
            {loading ? 'Validando...' : 'Confirmar'}
          </button>
        </div>
      </div>
    </div>
  );
};
