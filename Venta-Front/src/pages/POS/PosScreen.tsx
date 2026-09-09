import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { LogOut, User, LayoutGrid, Sun, Moon } from 'lucide-react';
import { useAuth, useComanda } from '@/contexts/AppContext';
import { useCaja } from '@/contexts/CajaContext';
import api from '@/services/api';
import CatalogoPanel from '@/common/components/POS/CatalogoPanel';
import ComandaPanel from '@/common/components/POS/ComandaPanel';
 
export default function PosScreen() {
  const { usuario, logout, theme, toggleTheme } = useAuth();
  const { limpiarComanda, setComandaDetails } = useComanda();
  const { turnoActivo } = useCaja();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const mesaId = searchParams.get('mesaId');
  const mesaName = searchParams.get('mesaName');
  const comandaIdParam = searchParams.get('comandaId');
  const clienteNombre = searchParams.get('clienteNombre');
 
  useEffect(() => {
    const loadOpenComanda = async () => {
      // 1. Cargar por comandaId directo si existe
      if (comandaIdParam) {
        try {
          const res = await api.get(`/comanda/${comandaIdParam}`);
          const openComanda = res.data;
          if (openComanda) {
            const mappedItems = (openComanda.items || []).map((i: any) => ({
              id: i.id,
              producto: i.producto,
              cantidad: i.cantidad,
              subtotal: i.cantidad * i.precioUnitario,
              cancelado: i.cancelado
            }));
            setComandaDetails(openComanda.id, mappedItems);
            return;
          }
        } catch (err) {
          console.error('Error al cargar comanda por ID:', err);
        }
      }
 
      // 2. Fallback: cargar por mesa
      if (!mesaId) {
        limpiarComanda();
        return;
      }
 
      try {
        const res = await api.get(`/comanda/mesa/${mesaId}`);
        const comandas = res.data || [];
        if (comandas.length > 0) {
          const openComanda = comandas[0];
          const mappedItems = (openComanda.items || []).map((i: any) => ({
            id: i.id,
            producto: i.producto,
            cantidad: i.cantidad,
            subtotal: i.cantidad * i.precioUnitario
          }));
          setComandaDetails(openComanda.id, mappedItems);
        } else {
          limpiarComanda();
        }
      } catch (err) {
        console.error('Error al cargar comanda abierta:', err);
        limpiarComanda();
      }
    };
 
    loadOpenComanda();
  }, [mesaId, comandaIdParam, setComandaDetails, limpiarComanda]);
 
  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };
 
  return (
    <div className="h-screen w-screen flex flex-col bg-slate-950 overflow-hidden">
      {/* Top Bar - Header Glass/Solid mix */}
      <header className="relative z-10 flex items-center justify-between px-6 py-3 bg-surface-base border-b border-border-default flex-shrink-0 shadow-sm">
        <div className="flex items-center gap-2.5">
          <h1 className="text-sm font-bold tracking-tight text-text-primary">
            Bares <span className="text-text-secondary font-semibold">Familia</span>
          </h1>
          <span className="text-[9px] font-semibold uppercase tracking-wider text-text-muted bg-slate-850 px-2 py-0.5 rounded-[3px] border border-border-default/50">
            POS
          </span>
          <span className="w-px h-4 bg-border-default mx-0.5" />
          {mesaName && (
            <span className="text-[10px] font-semibold uppercase tracking-wide text-text-secondary bg-slate-850 px-2.5 py-1 rounded-[3px] border border-border-default/60">
              Mesa {decodeURIComponent(mesaName)}
            </span>
          )}
          {clienteNombre && (
            <span className="text-[10px] font-semibold uppercase tracking-wide text-cyan-400 bg-cyan-500/5 px-2.5 py-1 rounded-[3px] border border-cyan-500/25">
              Cuenta: {decodeURIComponent(clienteNombre)}
            </span>
          )}
          {turnoActivo && (
            <span className="text-[10px] font-semibold uppercase tracking-wide text-text-secondary bg-slate-850 px-2.5 py-1 rounded-[3px] border border-border-default/60 flex items-center gap-1.5">
              <span className="w-1.5 h-1.5 rounded-full bg-success-500" />
              {new Date(turnoActivo.fechaContable).toLocaleDateString('es-AR')} · Turno {turnoActivo.turno}
            </span>
          )}
        </div>
 
        <div className="flex items-center gap-2">
          {/* Botón de cambio de Tema Claro/Oscuro */}
          <button 
            onClick={toggleTheme}
            className="touch-btn flex items-center justify-center w-[38px] h-[38px] rounded-[var(--radius-btn)] bg-slate-900/80 text-text-secondary hover:text-text-primary hover:bg-slate-800 border border-border-default/60 transition-all duration-150 shadow-sm"
            title={theme === 'dark' ? 'Modo Claro' : 'Modo Oscuro'}
          >
            {theme === 'dark' ? <Sun className="w-4 h-4 text-amber-500" /> : <Moon className="w-4 h-4 text-cyan-500" />}
          </button>

          <button 
            onClick={() => navigate('/')} 
            className="touch-btn flex items-center gap-1.5 px-4 py-2 rounded-[var(--radius-btn)] bg-slate-900/80 text-text-secondary hover:text-text-primary hover:bg-slate-800 border border-border-default transition-all duration-200 text-xs font-bold min-h-[38px] shadow-sm"
          >
            <LayoutGrid className="w-4 h-4 text-amber-500" />
            Volver al Salón
          </button>
 
          {usuario && (
            <div className="flex items-center gap-2 px-3 py-1.5 rounded-[var(--radius-btn)] bg-slate-900/40 border border-border-default/50 shadow-sm">
              <User className="w-4 h-4 text-text-muted" />
              <span className="text-xs font-semibold text-text-secondary">{usuario.nombre}</span>
              <span className="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider bg-info-500/5 text-info-400 border border-info-500/10">
                {usuario.rol}
              </span>
            </div>
          )}
          
          <button id="btn-logout" onClick={handleLogout}
            className="touch-btn flex items-center gap-1.5 px-4 py-2 rounded-[var(--radius-btn)] bg-slate-900/80 text-text-muted hover:text-danger-400 hover:bg-danger-500/5 border border-border-default transition-all duration-200 text-xs font-bold min-h-[38px] shadow-sm">
            <LogOut className="w-4 h-4" />
            Salir
          </button>
        </div>
      </header>

      {/* Content: 60/40 split */}
      <div className="flex flex-1 overflow-hidden">
        {/* Catálogo — 60% */}
        <main className="w-[60%] h-full overflow-hidden">
          <CatalogoPanel />
        </main>

        {/* Comanda — 40% */}
        <aside className="w-[40%] h-full overflow-hidden">
          <ComandaPanel />
        </aside>
      </div>
    </div>
  );
}
