import { useState } from 'react';
import { KeyRound, LogOut, Check } from 'lucide-react';
import { useCaja } from '@/contexts/CajaContext';
import { useAuth } from '@/contexts/AppContext';

interface ModalAperturaTurnoProps {
  onAperturaExitosa: () => void;
}

export default function ModalAperturaTurno({ onAperturaExitosa }: ModalAperturaTurnoProps) {
  const { abrirTurno } = useCaja();
  const { logout } = useAuth();
  const [montoText, setMontoText] = useState('0');
  const [errorMsg, setErrorMsg] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const handleKeyPress = (val: string) => {
    setErrorMsg('');
    if (val === 'C') {
      setMontoText('0');
    } else if (val === '←') {
      setMontoText(prev => (prev.length > 1 ? prev.slice(0, -1) : '0'));
    } else {
      setMontoText(prev => {
        if (prev === '0') return val;
        // Limitar a 7 dígitos para evitar desbordamiento visual ($9.999.999)
        if (prev.length >= 7) return prev;
        return prev + val;
      });
    }
  };

  const handleAddAmount = (amount: number) => {
    setErrorMsg('');
    setMontoText(prev => {
      const current = parseInt(prev) || 0;
      return String(current + amount);
    });
  };

  const handleSubmit = async (e?: React.FormEvent) => {
    e?.preventDefault();
    const val = parseInt(montoText) || 0;
    if (val < 0) {
      setErrorMsg('El fondo inicial no puede ser negativo.');
      return;
    }

    setSubmitting(true);
    try {
      const success = await abrirTurno(val);
      if (success) {
        onAperturaExitosa();
      } else {
        setErrorMsg('Error al abrir la caja. Verifique su conexión.');
      }
    } catch {
      setErrorMsg('Ocurrió un error inesperado al abrir la caja.');
    } finally {
      setSubmitting(false);
    }
  };

  const formatCurrency = (valStr: string) => {
    const val = parseInt(valStr) || 0;
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: 'ARS',
      minimumFractionDigits: 0
    }).format(val);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center animate-modal-backdrop"
      style={{ backgroundColor: 'rgba(0, 0, 0, 0.85)', backdropFilter: 'blur(10px)' }}>
      <div className="w-full max-w-md mx-4 p-8 rounded-[var(--radius-card)] bg-slate-900 border border-border-strong shadow-[var(--shadow-modal)] animate-modal-content">

        {/* Header */}
        <div className="flex flex-col items-center gap-4 mb-6 text-center">
          <div className="w-12 h-12 rounded-[var(--radius-btn)] bg-slate-850 border border-border-default flex items-center justify-center">
            <KeyRound className="w-5 h-5 text-amber-500" />
          </div>
          <div>
            <h3 className="text-lg font-bold text-text-primary">Apertura de Turno</h3>
            <p className="text-sm text-text-secondary mt-1">
              Ingrese el fondo inicial para registrar en caja
            </p>
          </div>
        </div>

        {/* Monto Display */}
        <div className="bg-slate-950 border border-border-default rounded-[var(--radius-input)] p-4 mb-6 text-center">
          <span className="text-[10px] font-semibold text-text-muted uppercase tracking-wider block">Fondo Inicial de Caja</span>
          <span className="text-3xl font-bold text-text-primary tracking-tight block mt-1">
            {formatCurrency(montoText)}
          </span>
        </div>

        {/* Predefined Shortcuts */}
        <div className="grid grid-cols-3 gap-2 mb-6">
          {[1000, 5000, 10000].map(amt => (
            <button
              key={amt}
              type="button"
              onClick={() => handleAddAmount(amt)}
              className="touch-btn py-2.5 px-3 rounded-[var(--radius-btn)] bg-slate-900 text-xs font-semibold text-text-secondary border border-border-default hover:bg-slate-850 hover:text-text-primary active:scale-95 transition-all cursor-pointer"
            >
              +${amt.toLocaleString('es-AR')}
            </button>
          ))}
        </div>

        {/* Numpad */}
        <div className="grid grid-cols-3 gap-2 mb-6">
          {['1', '2', '3', '4', '5', '6', '7', '8', '9', 'C', '0', '←'].map(key => (
            <button
              key={key}
              type="button"
              onClick={() => handleKeyPress(key)}
              className={`touch-btn py-3.5 rounded-[var(--radius-btn)] text-lg font-semibold select-none cursor-pointer active:scale-95 transition-all border ${
                key === 'C'
                  ? 'bg-danger-500/5 text-danger-400 border-danger-500/25 hover:bg-danger-500/10'
                  : key === '←'
                  ? 'bg-slate-900/60 text-text-secondary border-border-default hover:bg-slate-850'
                  : 'bg-slate-900/60 text-text-primary border-border-default hover:bg-slate-850'
              }`}
            >
              {key}
            </button>
          ))}
        </div>

        {/* Error message */}
        {errorMsg && (
          <div className="mb-4 text-center py-2 px-3 bg-danger-500/10 border border-danger-500/20 rounded-lg animate-fade-in">
            <span className="text-xs font-semibold text-danger-400">{errorMsg}</span>
          </div>
        )}

        {/* Actions */}
        <div className="flex gap-3">
          <button
            type="button"
            onClick={logout}
            className="touch-btn flex-1 py-3 px-4 rounded-[var(--radius-btn)] bg-slate-900 text-sm font-semibold text-text-secondary border border-border-default hover:bg-slate-850 hover:text-text-primary active:scale-95 transition-all flex items-center justify-center gap-2 cursor-pointer"
          >
            <LogOut className="w-4 h-4" />
            Cerrar Sesión
          </button>
          <button
            type="button"
            disabled={submitting}
            onClick={() => handleSubmit()}
            className="touch-btn flex-2 py-3 px-4 rounded-[var(--radius-btn)] bg-amber-500 text-sm font-bold text-slate-950 hover:bg-amber-400 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed transition-all flex items-center justify-center gap-1.5 cursor-pointer shadow-sm"
          >
            <Check className="w-4 h-4 stroke-[3]" />
            {submitting ? 'Abriendo...' : 'Confirmar'}
          </button>
        </div>

      </div>
    </div>
  );
}
