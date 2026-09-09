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
    <div className="h-screen w-screen flex items-center justify-center bg-slate-950 text-text-primary overflow-hidden font-sans">
      <div className="absolute inset-0 opacity-[0.03] pointer-events-none" style={{
        backgroundImage: 'radial-gradient(circle at 1px 1px, rgba(255,255,255,0.15) 1px, transparent 0)',
        backgroundSize: '32px 32px',
      }} />

      <div className="relative z-10 w-full max-w-md mx-4 bg-slate-900/60 backdrop-blur-xl border border-border-default rounded-[var(--radius-card)] p-8 shadow-[var(--shadow-modal)]">

        {success ? (
          <div className="text-center py-6 space-y-5 animate-fade-in">
            <div className="w-14 h-14 bg-success-500/10 text-success-500 border border-success-500/25 rounded-full flex items-center justify-center mx-auto">
              <ShieldCheck className="w-7 h-7" />
            </div>
            <div className="space-y-2">
              <h3 className="text-lg font-bold tracking-tight">POS Activado</h3>
              <p className="text-xs text-text-muted max-w-xs mx-auto">Vínculo seguro establecido con la Nube. Sincronización habilitada.</p>
            </div>
            <p className="text-[10px] font-semibold uppercase tracking-wider text-text-muted">Redirigiendo a pantalla de Login...</p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="flex items-center gap-3.5 mb-2">
              <div className="w-11 h-11 bg-slate-850 text-amber-500 border border-border-default rounded-[var(--radius-btn)] flex items-center justify-center">
                <Smartphone className="w-5 h-5" />
              </div>
              <div>
                <h3 className="text-base font-bold tracking-tight">Activar Terminal</h3>
                <p className="text-[11px] text-text-muted">Vincula este dispositivo local con el sistema general</p>
              </div>
            </div>

            {error && (
              <div className="bg-danger-500/5 border border-danger-500/20 text-danger-400 text-xs px-4 py-3 rounded-[var(--radius-btn)] flex items-start gap-2.5 animate-shake">
                <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
                <span>{error}</span>
              </div>
            )}

            <div className="space-y-2">
              <label htmlFor="code-input" className="text-[10px] font-bold uppercase tracking-widest text-text-muted flex items-center gap-1.5">
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
                className="w-full h-12 bg-slate-950/80 border border-border-strong rounded-[var(--radius-input)] px-4 text-center font-mono text-lg font-bold tracking-widest text-text-primary placeholder:text-text-muted/50 outline-none focus:border-amber-500/50 focus:ring-1 focus:ring-amber-500/20 transition-all uppercase"
              />
              <p className="text-[10px] text-text-muted text-center">Genera este código desde la sección "Activación POS" del Backoffice Administrador.</p>
            </div>

            <button
              type="submit"
              disabled={loading || !code.trim()}
              className="touch-btn w-full h-12 rounded-[var(--radius-btn)] bg-amber-500 text-slate-950 font-bold text-sm tracking-wide hover:bg-amber-400 disabled:opacity-40 disabled:hover:bg-amber-500 active:scale-[0.98] transition-all duration-200 shadow-sm cursor-pointer flex items-center justify-center gap-2"
            >
              {loading ? (
                <div className="w-4 h-4 border-2 border-slate-950/60 border-t-transparent rounded-full animate-spin" />
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
