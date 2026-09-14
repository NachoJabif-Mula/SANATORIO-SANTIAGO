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
  const origen = searchParams.get('origen');
 
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
              producto: {
                id: i.producto?.id,
                nombre: i.producto?.nombre,
                categoriaId: i.producto?.categoriaId,
                disponible: true,
                precio: i.precioUnitario
              },
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
            producto: {
              id: i.producto?.id,
              nombre: i.producto?.nombre,
              categoriaId: i.producto?.categoriaId,
              disponible: true,
              precio: i.precioUnitario
            },
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
      {/* Top Bar */}
      <header className="flex items-center flex-wrap gap-y-2 gap-x-3 px-3.5 py-2 min-h-[56px] bg-surface-base border-b border-border-default flex-shrink-0">
        <div className="flex items-center gap-2.5 min-w-0">
          <div className="w-4 h-4 border-2 border-amber-500 rounded-[5px] flex-shrink-0" />
          <span className="text-sm font-semibold tracking-tight text-text-primary truncate">Bares Familia</span>
        </div>

        {mesaName && (
          <span className="text-[10.5px] font-semibold uppercase tracking-wide text-text-secondary bg-surface-overlay px-2.5 py-1 rounded-[8px] border border-border-default font-mono">
            Mesa {decodeURIComponent(mesaName)}
          </span>
        )}
        {clienteNombre && (
          <span className="text-[10.5px] font-semibold uppercase tracking-wide text-cyan-500 bg-surface-overlay px-2.5 py-1 rounded-[8px] border border-cyan-500/40 font-mono">
            Cuenta: {decodeURIComponent(clienteNombre)}
          </span>
        )}
        {!mesaName && !clienteNombre && origen === 'rapida' && (
          <span className="text-[10.5px] font-semibold uppercase tracking-wide text-text-secondary bg-surface-overlay px-2.5 py-1 rounded-[8px] border border-border-default font-mono">
            Orden rápida
          </span>
        )}
        {!mesaName && !clienteNombre && origen === 'delivery' && (
          <span className="text-[10.5px] font-semibold uppercase tracking-wide text-amber-500 bg-surface-overlay px-2.5 py-1 rounded-[8px] border border-amber-500/40 font-mono">
            Delivery
          </span>
        )}
        {turnoActivo && (
          <span className="text-[10.5px] font-semibold uppercase tracking-wide text-text-secondary bg-surface-overlay px-2.5 py-1 rounded-[8px] border border-border-default flex items-center gap-1.5 font-mono">
            <span className="w-1.5 h-1.5 rounded-full bg-success-500" />
            {new Date(turnoActivo.fechaContable).toLocaleDateString('es-AR')} · Turno {turnoActivo.turno}
          </span>
        )}

        <div className="flex-1" />

        <div className="flex items-center gap-2">
          <button
            onClick={() => navigate('/')}
            className="touch-btn flex items-center gap-1.5 h-12 px-3 rounded-[var(--radius-btn)] bg-surface-base text-text-primary hover:border-amber-500 hover:text-amber-500 border border-border-default text-[12.5px] font-medium min-h-0"
          >
            <LayoutGrid className="w-4 h-4" />
            Volver al Salón
          </button>

          {usuario && (
            <div className="flex items-center gap-2 px-2.5 py-1 rounded-[var(--radius-btn)] bg-surface-overlay border border-border-default">
              <User className="w-3.5 h-3.5 text-text-muted" />
              <span className="text-xs font-semibold text-text-secondary">{usuario.nombre}</span>
              <span className="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider text-info-500 border border-info-500/30 font-mono">
                {usuario.rol}
              </span>
            </div>
          )}

          <button
            onClick={toggleTheme}
            className="touch-btn w-12 h-12 flex items-center justify-center rounded-[var(--radius-btn)] bg-surface-base border border-border-default text-text-muted hover:border-amber-500 hover:text-amber-500 min-h-0"
            title={theme === 'dark' ? 'Modo Claro' : 'Modo Oscuro'}
          >
            {theme === 'dark' ? <Sun className="w-4 h-4" /> : <Moon className="w-4 h-4" />}
          </button>

          <button id="btn-logout" onClick={handleLogout}
            className="touch-btn flex items-center gap-1.5 h-12 px-3 rounded-[var(--radius-btn)] bg-surface-base text-text-muted hover:border-danger-500 hover:text-danger-500 border border-border-default text-[12.5px] font-medium min-h-0">
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
