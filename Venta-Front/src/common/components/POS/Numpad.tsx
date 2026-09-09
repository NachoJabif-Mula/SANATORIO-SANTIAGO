import { useState, useCallback } from 'react';
import { Delete, CornerDownLeft } from 'lucide-react';

interface NumpadProps {
  /** Longitud máxima del PIN */
  maxLength?: number;
  /** Callback al completar/confirmar el PIN */
  onSubmit: (pin: string) => void;
  /** Muestra animación de error */
  error?: boolean;
  /** Texto del botón de confirmar */
  submitLabel?: string;
}

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'DEL', '0', 'OK'];

export default function Numpad({ maxLength = 4, onSubmit, error = false, submitLabel = 'Ingresar' }: NumpadProps) {
  const [value, setValue] = useState('');

  const handleKey = useCallback(
    (key: string) => {
      if (key === 'DEL') {
        setValue(prev => prev.slice(0, -1));
      } else if (key === 'OK') {
        if (value.length > 0) {
          onSubmit(value);
          setValue('');
        }
      } else {
        setValue(prev => (prev.length < maxLength ? prev + key : prev));
      }
    },
    [value, maxLength, onSubmit]
  );

  return (
    <div className="flex flex-col items-center gap-5 w-full max-w-[280px]">
      {/* Indicadores de PIN */}
      <div className={`flex gap-4 mb-2 ${error ? 'animate-shake' : ''}`}>
        {Array.from({ length: maxLength }).map((_, i) => (
          <div
            key={i}
            className={`w-3.5 h-3.5 rounded-full border transition-all duration-150 ${
              i < value.length
                ? 'bg-amber-500 border-amber-500'
                : 'border-slate-700 bg-transparent'
            }`}
          />
        ))}
      </div>

      {/* Teclado numérico */}
      <div className="grid grid-cols-3 gap-2.5 w-full">
        {KEYS.map(key => {
          const isDel = key === 'DEL';
          const isOk = key === 'OK';
          const isDisabled = isOk && value.length === 0;

          return (
            <button
              key={key}
              id={`numpad-key-${key.toLowerCase()}`}
              onClick={() => handleKey(key)}
              disabled={isDisabled}
              className={`
                touch-btn flex items-center justify-center rounded-[var(--radius-numpad)]
                transition-all duration-100 font-medium select-none
                active:scale-[0.96] active:bg-slate-800/80
                ${
                  isOk
                    ? 'bg-amber-500 text-slate-950 font-bold hover:bg-amber-400 border border-amber-500/20 disabled:opacity-20 disabled:bg-slate-900/30 disabled:text-text-muted/40 disabled:border-border-default/40 disabled:cursor-not-allowed shadow-sm'
                    : isDel
                    ? 'bg-slate-900/30 text-text-secondary hover:bg-slate-850 border border-border-default/60 hover:text-text-primary'
                    : 'bg-slate-900/60 text-text-primary hover:bg-slate-850 border border-border-default/80 hover:border-border-strong'
                }
                h-14 text-xl
              `}
            >
              {isDel ? (
                <Delete className="w-5 h-5 stroke-[1.5]" />
              ) : isOk ? (
                <span className="flex items-center gap-1.5 text-sm font-bold tracking-wide">
                  <CornerDownLeft className="w-4 h-4 stroke-[2]" />
                  {submitLabel}
                </span>
              ) : (
                key
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
}
