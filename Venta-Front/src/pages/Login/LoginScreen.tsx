import { useState, useCallback, useEffect } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ShieldCheck, Lock, Loader2, Users } from 'lucide-react';
import Numpad from '@/common/components/POS/Numpad';
import { useAuth } from '@/contexts/AppContext';
import { useActivationStatus } from '@/common/hooks/useActivationStatus';
import api from '@/services/api';

export default function LoginScreen() {
  const { login, autenticado } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [checkingUsers, setCheckingUsers] = useState(true);
  const [usersAvailable, setUsersAvailable] = useState(false);

  // Polling de activación remota (cada 30s)
  useActivationStatus(30000, true);

  // Verificar si hay usuarios disponibles al cargar
  useEffect(() => {
    let cancelled = false;
    let pollInterval: ReturnType<typeof setInterval> | null = null;

    const checkUsers = async () => {
      try {
        const res = await api.get('/usuario/disponibles');
        if (!cancelled) {
          if (res.data.disponibles) {
            setUsersAvailable(true);
            setCheckingUsers(false);
            if (pollInterval) clearInterval(pollInterval);
          } else {
            setUsersAvailable(false);
            setCheckingUsers(true);
          }
        }
      } catch {
        if (!cancelled) {
          setCheckingUsers(true);
        }
      }
    };

    // Check inmediato
    checkUsers();

    // Polling cada 3s hasta que haya usuarios
    pollInterval = setInterval(checkUsers, 3000);

    return () => {
      cancelled = true;
      if (pollInterval) clearInterval(pollInterval);
    };
  }, []);

  // Limpiar error después de la animación
  useEffect(() => {
    if (error) {
      const timer = setTimeout(() => {
        setError(false);
        setErrorMsg('');
      }, 1500);
      return () => clearTimeout(timer);
    }
  }, [error]);

  const handleLogin = useCallback(
    async (pin: string) => {
      const success = await login(pin);
      if (success) {
        navigate('/', { replace: true });
      } else {
        setError(true);
        setErrorMsg('PIN incorrecto. Intente nuevamente.');
      }
    },
    [login, navigate]
  );

  // Si no está activado el dispositivo, forzar activación
  if (!localStorage.getItem('bf_pos_activado')) {
    return <Navigate to="/activar-pos" replace />;
  }

  // Si ya está autenticado, redirigir al POS
  if (autenticado) {
    return <Navigate to="/" replace />;
  }

  return (
    <div className="h-screen w-screen flex items-center justify-center bg-slate-950 pos-grid-bg p-6">
      {/* Card: dos paneles como el mockup — branding+PIN a la izquierda, teclado a la derecha */}
      <div className="relative z-10 w-full max-w-[880px] grid grid-cols-1 sm:grid-cols-[minmax(0,1fr)_320px] bg-surface-base border border-border-default rounded-[14px] overflow-hidden shadow-modal animate-scale-in">

        {/* Panel izquierdo: branding + estado de PIN */}
        <div className="p-9 flex flex-col justify-between gap-8 min-w-0">
          {/* Branding */}
          <div className="flex flex-col gap-1.5">
            <div className="flex items-center gap-2.5">
              <div className="w-[22px] h-[22px] border-2 border-amber-500 rounded-full flex-shrink-0" />
              <span className="text-[19px] font-semibold tracking-tight text-text-primary">
                Bares Familia
              </span>
            </div>
            <div className="font-mono text-[11px] tracking-[0.12em] uppercase text-text-muted pl-[32px]">
              Terminal de venta
            </div>
          </div>

          {checkingUsers && !usersAvailable ? (
            /* Estado de sincronización — esperando empleados */
            <div className="flex flex-col items-center gap-4 py-6">
              <div className="w-12 h-12 rounded-[var(--radius-btn)] bg-amber-500/5 border border-amber-500/20 flex items-center justify-center">
                <Users className="w-6 h-6 text-amber-500" />
              </div>
              <div className="text-center space-y-1">
                <p className="text-text-secondary text-sm font-semibold">Sincronizando empleados...</p>
                <p className="text-text-muted text-xs max-w-[280px]">El sistema está descargando la base de datos local desde la Nube.</p>
              </div>
              <div className="flex items-center gap-2 mt-1">
                <Loader2 className="w-4 h-4 text-amber-500 animate-spin" />
                <span className="text-[11px] font-medium text-text-muted font-mono">Espere por favor...</span>
              </div>
            </div>
          ) : (
            <div className="flex flex-col gap-[18px]">
              <div className="text-sm text-text-secondary">Ingrese su PIN de operador</div>
              <div className="min-h-[18px] text-[12.5px] font-medium text-danger-500">
                {errorMsg}
              </div>
            </div>
          )}

          {/* Info del dispositivo */}
          <div className="flex flex-col gap-2">
            <div className="font-mono text-[10px] tracking-[0.14em] uppercase text-text-muted">
              Acceso seguro
            </div>
            <div className="flex items-center gap-2 text-text-muted text-[11px]">
              <ShieldCheck className="w-3.5 h-3.5" />
              <span className="font-mono">
                Terminal {localStorage.getItem('bf_pos_id')?.slice(0, 8) || 'LOCAL-POS'} · v1.1.0
              </span>
              <Lock className="w-3 h-3 opacity-60" />
            </div>
          </div>
        </div>

        {/* Panel derecho: teclado numérico */}
        <div className="bg-surface-overlay border-t sm:border-t-0 sm:border-l border-border-default p-6 flex items-center justify-center">
          {checkingUsers && !usersAvailable ? (
            <div className="text-center text-xs text-text-muted font-mono">Esperando datos…</div>
          ) : (
            <Numpad onSubmit={handleLogin} error={error} submitLabel="Ingresar" />
          )}
        </div>
      </div>
    </div>
  );
}
