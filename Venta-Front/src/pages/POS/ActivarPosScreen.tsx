import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Smartphone, AlertTriangle, ShieldCheck, Key } from 'lucide-react';
import api from '@/services/api';

export default function ActivarPosScreen() {
  const navigate = useNavigate();
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [checkingActivation, setCheckingActivation] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  // Verificar si ya está activado en el backend (ej: tras limpieza de localStorage)
  useEffect(() => {
    const checkLocalActivation = async () => {
      try {
        const res = await api.get('/dispositivo/estado');
        if (res.data.activado) {
          localStorage.setItem('bf_pos_activado', 'true');
          navigate('/login', { replace: true });
        } else {
          setCheckingActivation(false);
        }
      } catch (err) {
        // En caso de error de conexión local, permitir que el usuario intente activarlo manualmente
        setCheckingActivation(false);
      }
    };
    checkLocalActivation();
  }, [navigate]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim()) return;

    setLoading(true);
    setError(null);

    try {
      await api.post('/dispositivo/activar', {
        codigoActivacion: code.trim().toUpperCase()
      });

      localStorage.setItem('bf_pos_activado', 'true');
      setSuccess(true);

      // Redirigir al login después de mostrar el éxito.
      // La pantalla de login se encarga de esperar la sincronización de empleados.
      setTimeout(() => {
        navigate('/login', { replace: true });
      }, 2500);
    } catch (err: any) {
      console.error('Error al activar:', err);
      const msg = err.response?.data?.message || 'Error de conexión con la API Local. Verifica que el servidor local esté encendido.';
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  // Autocompletado del guion BAR-XXXX-XXXX
  const handleCodeChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    let value = e.target.value.toUpperCase().replace(/[^A-Z0-9-]/g, '');

    // Autocompletar guiones
    if (value.startsWith('BAR')) {
      if (value.length === 7 && !value.endsWith('-') && e.nativeEvent.type !== 'input') {
        value = value.slice(0, 7) + '-' + value.slice(7);
      }
    }
    setCode(value);
  };

  if (checkingActivation) {
    return (
      <div className="h-screen w-screen flex items-center justify-center bg-slate-950 text-text-primary overflow-hidden font-sans">
        <div className="flex flex-col items-center gap-3">
          <div className="w-9 h-9 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
          <p className="text-xs text-text-muted">Verificando estado de la terminal...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="h-screen w-screen flex items-center justify-center bg-slate-950 pos-grid-bg text-text-primary overflow-hidden font-sans">
      <div className="relative z-10 w-full max-w-md mx-4 bg-surface-base border border-border-default rounded-[14px] p-8 shadow-modal">

        {success ? (
          <div className="text-center py-6 space-y-5 animate-fade-in">
            <ShieldCheck className="w-6 h-6 text-success-500 mx-auto" />
            <div className="space-y-2">
              <h3 className="text-[15px] font-semibold tracking-tight">POS Activado</h3>
              <p className="text-xs text-text-muted max-w-xs mx-auto">Vínculo seguro establecido con la Nube. Sincronización habilitada.</p>
            </div>
            <p className="mono-label">Redirigiendo a pantalla de Login...</p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="flex items-center gap-2.5 mb-2">
              <Smartphone className="w-4 h-4 text-amber-500" />
              <div>
                <h3 className="text-[15px] font-semibold tracking-tight">Activar Terminal</h3>
                <p className="text-[11px] text-text-muted">Vincula este dispositivo local con el sistema general</p>
              </div>
            </div>

            {error && (
              <div className="border border-danger-500/40 text-danger-500 text-xs px-4 py-3 rounded-[var(--radius-btn)] flex items-start gap-2.5 animate-shake">
                <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
                <span>{error}</span>
              </div>
            )}

            <div className="space-y-2">
              <label htmlFor="code-input" className="mono-label flex items-center gap-1.5">
                <Key className="w-3.5 h-3.5 text-amber-500" />
                Código de Emparejamiento
              </label>
              <input
                id="code-input"
                type="text"
                value={code}
                onChange={handleCodeChange}
                placeholder="BAR-XXXX-XXXX"
                maxLength={13}
                disabled={loading}
                className="w-full h-12 bg-surface-overlay border border-border-default rounded-[var(--radius-input)] px-4 text-center font-mono text-lg font-semibold tracking-widest text-text-primary placeholder:text-text-muted/50 outline-none focus:border-amber-500 uppercase"
              />
              <p className="text-[10px] text-text-muted text-center">Genera este código desde la sección "Activación POS" del Backoffice Administrador.</p>
            </div>

            <button
              type="submit"
              disabled={loading || !code.trim()}
              className="touch-btn w-full h-12 rounded-[var(--radius-btn)] bg-amber-500 border border-amber-500 text-white font-semibold text-sm tracking-wide hover:bg-amber-600 disabled:opacity-40 active:scale-[0.98] cursor-pointer flex items-center justify-center gap-2"
            >
              {loading ? (
                <div className="w-4 h-4 border-2 border-white/60 border-t-transparent rounded-full animate-spin" />
              ) : (
                'Confirmar Activación'
              )}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}
