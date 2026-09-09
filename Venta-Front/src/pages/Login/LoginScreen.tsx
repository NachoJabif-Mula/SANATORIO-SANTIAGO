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
    <div className="h-screen w-screen flex items-center justify-center bg-slate-950 relative overflow-hidden">
      {/* Background pattern (Very subtle) */}
      <div className="absolute inset-0 opacity-[0.03]">
        <div
          className="absolute inset-0"
          style={{
            backgroundImage: `radial-gradient(circle at 1px 1px, rgba(255,255,255,0.15) 1px, transparent 0)`,
            backgroundSize: '32px 32px',
          }}
        />
      </div>

      {/* Gradient orbs (Extremely muted & slow pulse feel) */}
      <div className="absolute top-[-25%] right-[-15%] w-[600px] h-[600px] rounded-full bg-amber-500/[0.02] blur-[150px] pointer-events-none" />
      <div className="absolute bottom-[-25%] left-[-15%] w-[500px] h-[500px] rounded-full bg-cyan-500/[0.02] blur-[150px] pointer-events-none" />

      {/* Login Card Container - Glassmorphic design */}
      <div className="relative z-10 w-full max-w-[420px] mx-4 p-8 rounded-2xl bg-slate-900/50 backdrop-blur-2xl border border-border-default shadow-modal flex flex-col items-center gap-6 animate-scale-in">
        
        {/* Logo & Branding */}
        <div className="flex flex-col items-center gap-3">
          <div className="relative">
            <div className="w-16 h-16 rounded-xl bg-slate-850 border border-amber-500/20 flex items-center justify-center shadow-card">
              <ShieldCheck className="w-8 h-8 text-amber-500" />
            </div>
            <div className="absolute -bottom-1 -right-1 w-6 h-6 rounded-md bg-slate-950 border border-border-strong flex items-center justify-center">
              <Lock className="w-3.5 h-3.5 text-amber-500/70" />
            </div>
          </div>

          <div className="text-center mt-1">
            <h1 className="text-xl font-bold tracking-tight text-text-primary">
              Bares <span className="text-text-secondary font-semibold">Familia</span>
            </h1>
            <p className="text-[10px] text-text-muted mt-0.5 tracking-widest uppercase font-bold">
              Terminal Punto de Venta
            </p>
          </div>
        </div>

        {/* Separator - Sleek thin line */}
        <div className="w-full h-[1px] bg-border-default/60" />

        {checkingUsers && !usersAvailable ? (
          /* Estado de sincronización — esperando empleados */
          <div className="flex flex-col items-center gap-4 py-6">
            <div className="w-12 h-12 rounded-xl bg-amber-500/5 border border-amber-500/10 flex items-center justify-center">
              <Users className="w-6 h-6 text-amber-500/80" />
            </div>
            <div className="text-center space-y-1">
              <p className="text-text-secondary text-sm font-semibold">Sincronizando empleados...</p>
              <p className="text-text-muted text-xs max-w-[280px]">El sistema está descargando la base de datos local desde la Nube.</p>
            </div>
            <div className="flex items-center gap-2 mt-2">
              <Loader2 className="w-4 h-4 text-amber-500 animate-spin" />
              <span className="text-[11px] font-medium text-text-muted">Espere por favor...</span>
            </div>
          </div>
        ) : (
          <>
            {/* Instrucción */}
            <div className="text-center">
              <p className="text-text-secondary text-sm font-medium">Ingrese su PIN personal</p>
            </div>

            {/* Numpad */}
            <Numpad onSubmit={handleLogin} error={error} />

            {/* Mensaje de error */}
            <div className="h-4 flex items-center justify-center">
              {errorMsg && (
                <p className="text-danger-400 text-xs font-semibold animate-fade-in">{errorMsg}</p>
              )}
            </div>
          </>
        )}

        {/* Separator - Sleek thin line */}
        <div className="w-full h-[1px] bg-border-default/30" />

        {/* Pie con info */}
        <div className="text-text-muted text-[10px] text-center space-y-0.5">
          <p className="font-medium">Acceso seguro encriptado localmente</p>
          <p className="text-text-muted/65">Terminal ID: {localStorage.getItem('bf_pos_id')?.slice(0,8) || 'LOCAL-POS'} — v1.1.0</p>
        </div>
      </div>
    </div>
  );
}
