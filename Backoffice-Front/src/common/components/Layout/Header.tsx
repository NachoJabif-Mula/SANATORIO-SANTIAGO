import { useLocation } from 'react-router-dom';
import { Bell, Building2 } from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';
import { useSucursal } from '@/contexts/SucursalContext';

const pageTitles: Record<string, string> = {
  '/': 'Dashboard',
  '/catalogo': 'Catálogo de Productos',
  '/sucursales': 'Sucursales',
  '/activacion-pos': 'Activación POS',
  '/impresoras': 'Configuración de Impresoras',
};

export default function Header() {
  const location = useLocation();
  const title = pageTitles[location.pathname] || 'Dashboard';
  const { user } = useAuth();
  const { sucursales, selectedSucursalId, setSelectedSucursalId, isGlobal } = useSucursal();

  return (
    <header className="
      sticky top-0 z-30 flex items-center justify-between
      h-16 px-6 bg-white/70 backdrop-blur-xl
      border-b border-pearl-200/50 shadow-[var(--shadow-header)]
    ">
      {/* Page Title */}
      <div>
        <h2 className="text-lg font-bold text-pearl-900">{title}</h2>
        <p className="text-xs text-pearl-400 -mt-0.5">
          {new Date().toLocaleDateString('es-AR', {
            weekday: 'long',
            year: 'numeric',
            month: 'long',
            day: 'numeric',
          })}
        </p>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2">
        {/* Selector de Sucursal */}
        <div className="relative group hidden md:block">
          <Building2
            size={16}
            className="absolute left-3 top-1/2 -translate-y-1/2 text-pearl-400 group-focus-within:text-brand-500 transition-colors pointer-events-none"
          />
          {isGlobal ? (
            <select
              id="sucursal-selector"
              value={selectedSucursalId ?? ''}
              onChange={(e) => setSelectedSucursalId(e.target.value || null)}
              className="
                w-64 h-9 pl-9 pr-4 text-sm font-medium
                bg-ice-50 border border-pearl-200
                rounded-lg outline-none cursor-pointer
                focus:bg-white focus:border-brand-400 focus:ring-4 focus:ring-brand-50
                transition-all duration-200 shadow-sm
              "
            >
              {sucursales.map((s) => (
                <option key={s.id} value={s.id}>{s.nombre}</option>
              ))}
            </select>
          ) : (
            <div className="
              w-64 h-9 pl-9 pr-4 flex items-center text-sm font-medium
              bg-ice-50 border border-pearl-200 text-pearl-600
              rounded-lg truncate shadow-sm
            ">
              {user?.sucursal ?? 'Sucursal'}
            </div>
          )}
        </div>

        {/* Notifications */}
        <button
          id="notifications-btn"
          className="
            relative w-9 h-9 rounded-lg
            flex items-center justify-center
            text-pearl-500 hover:bg-ice-200 hover:text-pearl-700
            transition-colors duration-200 cursor-pointer
          "
          aria-label="Notificaciones"
        >
          <Bell size={18} />
          <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-danger-500 rounded-full animate-pulse-glow" />
        </button>
      </div>
    </header>
  );
}
