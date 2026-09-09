import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import api from '@/services/api';
import { useAuth } from '@/contexts/AuthContext';
import type { Sucursal } from '@/common/types';

// ════════════════════════════════════════
// Tipos
// ════════════════════════════════════════

interface SucursalContextType {
  sucursales: Sucursal[];
  selectedSucursalId: string | null;
  setSelectedSucursalId: (id: string | null) => void;
  isGlobal: boolean;
  isLoading: boolean;
}

const SucursalContext = createContext<SucursalContextType | null>(null);

const SELECTED_KEY = 'bf_sucursal_id';

// ════════════════════════════════════════
// Provider
// ════════════════════════════════════════

export function SucursalProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const [sucursales, setSucursales] = useState<Sucursal[]>([]);
  const [selectedSucursalId, setSelectedSucursalIdState] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const isGlobal = !!user?.esGlobal;

  useEffect(() => {
    if (!user) {
      setSucursales([]);
      setSelectedSucursalIdState(null);
      setIsLoading(false);
      return;
    }

    if (!isGlobal) {
      // Usuario acotado a su propia sucursal: no hay selector, queda fija.
      setSelectedSucursalIdState(user.sucursalId);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    api.get('/sucursal')
      .then((res) => {
        const list: Sucursal[] = res.data || [];
        setSucursales(list);

        const stored = localStorage.getItem(SELECTED_KEY);
        const valid = stored && list.some(s => s.id === stored) ? stored : (list[0]?.id ?? null);
        setSelectedSucursalIdState(valid);
      })
      .catch((err) => console.error('Error al cargar sucursales:', err))
      .finally(() => setIsLoading(false));
  }, [user, isGlobal]);

  const setSelectedSucursalId = useCallback((id: string | null) => {
    if (!isGlobal) return; // usuario acotado no puede cambiar de sucursal
    setSelectedSucursalIdState(id);
    if (id) {
      localStorage.setItem(SELECTED_KEY, id);
    } else {
      localStorage.removeItem(SELECTED_KEY);
    }
  }, [isGlobal]);

  return (
    <SucursalContext.Provider value={{ sucursales, selectedSucursalId, setSelectedSucursalId, isGlobal, isLoading }}>
      {children}
    </SucursalContext.Provider>
  );
}

// ════════════════════════════════════════
// Hook
// ════════════════════════════════════════

export function useSucursal() {
  const ctx = useContext(SucursalContext);
  if (!ctx) throw new Error('useSucursal debe usarse dentro de <SucursalProvider>');
  return ctx;
}
