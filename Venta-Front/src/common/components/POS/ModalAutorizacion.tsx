import { useState } from 'react';
import { Lock } from 'lucide-react';
import api from '@/services/api';

interface ModalAutorizacionProps {
  onConfirmar: () => void;
  onCancelar: () => void;
}

export const ModalAutorizacion = ({ onConfirmar, onCancelar }: ModalAutorizacionProps) => {
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
      style={{ backgroundColor: 'rgba(0, 0, 0, 0.7)', backdropFilter: 'blur(10px)' }}>
      <div className="w-full max-w-sm mx-4 p-6 rounded-[var(--radius-card)] bg-slate-900 border border-border-strong shadow-[var(--shadow-modal)] animate-modal-content">
        <div className="flex flex-col items-center gap-3 mb-5 text-center">
          <div className="w-14 h-14 rounded-2xl bg-amber-500/10 border border-amber-500/25 flex items-center justify-center">
            <Lock className="w-6 h-6 text-amber-500" />
          </div>
          <div>
            <h2 className="text-base font-bold text-text-primary">Autorización Gerente</h2>
            <p className="text-sm text-text-secondary mt-1">Ingrese su PIN de gerente para autorizar esta operación</p>
          </div>
        </div>

        <div className="bg-slate-950/60 border border-border-default rounded-[var(--radius-input)] py-4 mb-4 flex items-center justify-center gap-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className={`w-3 h-3 rounded-full border transition-all duration-150 ${
              i < pin.length ? 'bg-amber-500 border-amber-500' : 'border-slate-700 bg-transparent'
            }`} />
          ))}
        </div>

        {error && (
          <div className="mb-4 py-2 px-3 bg-danger-500/5 border border-danger-500/20 rounded-[var(--radius-btn)] text-center animate-shake">
            <span className="text-xs font-semibold text-danger-400">{error}</span>
          </div>
        )}

        <div className="grid grid-cols-3 gap-2 mb-5">
          {['1', '2', '3', '4', '5', '6', '7', '8', '9', 'C', '0', '←'].map(k => (
            <button
              key={k}
              type="button"
              onClick={() => handleNumpadPress(k)}
              disabled={loading}
              className={`touch-btn h-12 rounded-[var(--radius-btn)] font-bold text-base select-none active:scale-95 transition-all border disabled:opacity-40 disabled:cursor-not-allowed ${
                k === 'C'
                  ? 'bg-danger-500/5 text-danger-400 border-danger-500/25 hover:bg-danger-500/10'
                  : k === '←'
                  ? 'bg-slate-900/60 text-text-secondary border-border-default/60 hover:bg-slate-850'
                  : 'bg-slate-900/60 text-text-primary border-border-default/80 hover:bg-slate-850 hover:border-border-strong'
              }`}
            >
              {k}
            </button>
          ))}
        </div>

        <div className="flex gap-2.5">
          <button type="button" onClick={onCancelar} disabled={loading}
            className="touch-btn flex-1 py-3 rounded-[var(--radius-btn)] bg-slate-900 border border-border-default text-sm font-bold text-text-secondary hover:text-text-primary hover:bg-slate-850 active:scale-[0.98] disabled:opacity-50 transition-all">
            Cancelar
          </button>
          <button type="button" onClick={handleConfirmar} disabled={loading || pin.length < 4}
            className="touch-btn flex-1 py-3 rounded-[var(--radius-btn)] bg-amber-500 text-sm font-bold text-slate-950 hover:bg-amber-400 active:scale-[0.98] disabled:opacity-40 disabled:cursor-not-allowed transition-all shadow-sm flex items-center justify-center gap-1.5">
            {loading && <span className="w-3.5 h-3.5 border-2 border-slate-950/60 border-t-transparent rounded-full animate-spin" />}
            {loading ? 'Validando...' : 'Confirmar'}
          </button>
        </div>
      </div>
    </div>
  );
};
