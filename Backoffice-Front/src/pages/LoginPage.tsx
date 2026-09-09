import { useState, useCallback, type FormEvent } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { Building2, Mail, Lock, Eye, EyeOff, LogIn, AlertCircle, Loader2 } from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';

export default function LoginPage() {
  const { login, isAuthenticated, isLoading: authLoading } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = useCallback(async (e: FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !password.trim()) return;

    setLoading(true);
    setError(null);

    const errorMsg = await login(email.trim(), password, rememberMe);
    if (errorMsg) {
      setError(errorMsg);
      setLoading(false);
    } else {
      navigate('/', { replace: true });
    }
  }, [email, password, rememberMe, login, navigate]);

  // Si ya está autenticado, redirigir al dashboard
  if (!authLoading && isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  return (
    <div className="min-h-screen w-full flex items-center justify-center bg-gradient-to-br from-brand-900 via-brand-800 to-pearl-900 relative overflow-hidden">
      {/* Background decorativo */}
      <div className="absolute inset-0 pointer-events-none">
        <div className="absolute top-0 left-0 w-[600px] h-[600px] bg-brand-500/10 rounded-full blur-[160px] -translate-x-1/2 -translate-y-1/2" />
        <div className="absolute bottom-0 right-0 w-[500px] h-[500px] bg-brand-400/8 rounded-full blur-[140px] translate-x-1/3 translate-y-1/3" />
        <div
          className="absolute inset-0 opacity-[0.03]"
          style={{
            backgroundImage: 'radial-gradient(circle at 1px 1px, rgba(255,255,255,0.3) 1px, transparent 0)',
            backgroundSize: '32px 32px',
          }}
        />
      </div>

      {/* Login Card */}
      <div className="relative z-10 w-full max-w-md mx-4">
        <div className="bg-white/[0.06] backdrop-blur-2xl border border-white/10 rounded-3xl p-8 shadow-2xl shadow-black/30">

          {/* Branding */}
          <div className="flex flex-col items-center gap-4 mb-8">
            <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-600 flex items-center justify-center shadow-lg shadow-brand-500/20">
              <Building2 className="w-8 h-8 text-white" />
            </div>
            <div className="text-center">
              <h1 className="text-2xl font-extrabold tracking-tight text-white">
                BARES <span className="text-brand-300">FAMILIA</span>
              </h1>
              <p className="text-sm text-white/40 mt-1 tracking-wide font-medium">
                Backoffice Administrador
              </p>
            </div>
          </div>

          {/* Separador */}
          <div className="w-12 h-[2px] bg-gradient-to-r from-transparent via-brand-400/40 to-transparent mx-auto mb-6" />

          {/* Formulario */}
          <form onSubmit={handleSubmit} className="space-y-5">

            {/* Error */}
            {error && (
              <div className="flex items-start gap-2.5 bg-red-500/10 border border-red-500/20 text-red-300 text-sm px-4 py-3 rounded-xl animate-fade-in">
                <AlertCircle className="w-4 h-4 mt-0.5 flex-shrink-0" />
                <span>{error}</span>
              </div>
            )}

            {/* Email */}
            <div className="space-y-1.5">
              <label htmlFor="login-email" className="text-xs font-semibold text-white/50 uppercase tracking-wider flex items-center gap-1.5">
                <Mail className="w-3.5 h-3.5" />
                Correo electrónico
              </label>
              <input
                id="login-email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="admin@baresfamilia.com"
                disabled={loading}
                autoComplete="email"
                className="w-full h-12 bg-white/5 border border-white/10 rounded-xl px-4 text-white placeholder:text-white/20 outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-400/15 transition-all disabled:opacity-50"
              />
            </div>

            {/* Password */}
            <div className="space-y-1.5">
              <label htmlFor="login-password" className="text-xs font-semibold text-white/50 uppercase tracking-wider flex items-center gap-1.5">
                <Lock className="w-3.5 h-3.5" />
                Contraseña
              </label>
              <div className="relative">
                <input
                  id="login-password"
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  disabled={loading}
                  autoComplete="current-password"
                  className="w-full h-12 bg-white/5 border border-white/10 rounded-xl px-4 pr-12 text-white placeholder:text-white/20 outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-400/15 transition-all disabled:opacity-50"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-white/30 hover:text-white/60 transition-colors cursor-pointer"
                  tabIndex={-1}
                >
                  {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                </button>
              </div>
            </div>

            {/* Mantener sesión */}
            <label htmlFor="login-remember" className="flex items-center gap-3 cursor-pointer group py-1">
              <div className="relative">
                <input
                  id="login-remember"
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                  className="sr-only peer"
                />
                <div className="w-5 h-5 rounded-md border border-white/15 bg-white/5 peer-checked:bg-brand-500 peer-checked:border-brand-500 transition-all flex items-center justify-center group-hover:border-white/25">
                  {rememberMe && (
                    <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                    </svg>
                  )}
                </div>
              </div>
              <div>
                <span className="text-sm text-white/60 group-hover:text-white/80 transition-colors">Mantener sesión iniciada</span>
                <p className="text-[10px] text-white/30 mt-0.5">Vigencia máxima: 30 días</p>
              </div>
            </label>

            {/* Submit */}
            <button
              type="submit"
              disabled={loading || !email.trim() || !password.trim()}
              className="w-full h-12 rounded-xl bg-brand-500 text-white font-bold text-sm tracking-wide hover:bg-brand-400 disabled:opacity-40 disabled:hover:bg-brand-500 active:scale-[0.98] transition-all duration-200 shadow-lg shadow-brand-500/20 cursor-pointer flex items-center justify-center gap-2"
            >
              {loading ? (
                <Loader2 className="w-5 h-5 animate-spin" />
              ) : (
                <>
                  <LogIn className="w-4 h-4" />
                  Iniciar Sesión
                </>
              )}
            </button>
          </form>

          {/* Pie */}
          <div className="text-center mt-6 space-y-1">
            <p className="text-white/20 text-xs">Sistema protegido por autenticación JWT</p>
            <p className="text-white/15 text-[10px]">v1.0.0 — Backoffice Central</p>
          </div>
        </div>
      </div>
    </div>
  );
}
