import { createContext, useContext, useState, useCallback, type ReactNode } from 'react';
import type { UsuarioPOS, ItemComanda, Producto } from '@/common/types';
import { isGuid } from '@/common/utils/guid';
import api from '@/services/api';

/* ============================================
   Auth Context — Manejo de sesión por PIN
   ============================================ */

interface AuthContextType {
  usuario: UsuarioPOS | null;
  autenticado: boolean;
  login: (pin: string) => Promise<boolean>;
  logout: () => void;
  validarPinGerente: (pin: string) => Promise<boolean>;
  tienePermiso: (permiso: string) => boolean;
  theme: 'dark' | 'light';
  toggleTheme: () => void;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<UsuarioPOS | null>(null);
  const [theme, setTheme] = useState<'dark' | 'light'>(() => {
    const saved = localStorage.getItem('pos-theme');
    if (saved === 'light' || saved === 'dark') return saved;
    return 'dark';
  });

  const toggleTheme = useCallback(() => {
    setTheme(prev => {
      const next = prev === 'dark' ? 'light' : 'dark';
      localStorage.setItem('pos-theme', next);
      if (next === 'light') {
        document.documentElement.classList.add('light');
      } else {
        document.documentElement.classList.remove('light');
      }
      return next;
    });
  }, []);

  // Sync theme class on mount/initial load
  useState(() => {
    const saved = localStorage.getItem('pos-theme');
    if (saved === 'light') {
      document.documentElement.classList.add('light');
    } else {
      document.documentElement.classList.remove('light');
    }
  });

  const login = useCallback(async (pin: string): Promise<boolean> => {
    try {
      const res = await api.post('/usuario/login', { pin });
      if (res.data) {
        setUsuario(res.data);
        return true;
      }
      return false;
    } catch (err) {
      console.error('Error al iniciar sesión:', err);
      return false;
    }
  }, []);

  const logout = useCallback(() => {
    setUsuario(null);
  }, []);

  const validarPinGerente = useCallback(async (pin: string): Promise<boolean> => {
    try {
      const res = await api.post('/usuario/validar-gerente', { pin });
      return !!res.data.valid;
    } catch {
      return false;
    }
  }, []);

  const tienePermiso = useCallback((permiso: string): boolean => {
    if (!usuario) return false;
    // Administradores y gerentes tienen acceso total por defecto
    if (usuario.rol === 'gerente' || usuario.rol === 'administrador') return true;
    return usuario.permisos?.includes(permiso) || false;
  }, [usuario]);

  return (
    <AuthContext.Provider value={{ usuario, autenticado: !!usuario, login, logout, validarPinGerente, tienePermiso, theme, toggleTheme }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider');
  return ctx;
}

/* ============================================
   Comanda Context — Manejo de pedido activo
   ============================================ */

interface ComandaContextType {
  items: ItemComanda[];
  comandaId: string | null;
  total: number;
  cantidadItems: number;
  /** true si hubo alguna modificación (alta, incremento, quita) desde que se cargó/envió la comanda. */
  huboModificaciones: boolean;
  agregarItem: (producto: Producto) => void;
  quitarItem: (itemId: string) => void;
  incrementar: (itemId: string) => void;
  decrementar: (itemId: string) => void;
  limpiarComanda: () => void;
  setComandaDetails: (comandaId: string | null, items: ItemComanda[]) => void;
  marcarItemCancelado: (itemId: string) => void;
}

const ComandaContext = createContext<ComandaContextType | null>(null);

export function ComandaProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ItemComanda[]>([]);
  const [comandaId, setComandaId] = useState<string | null>(null);
  const [huboModificaciones, setHuboModificaciones] = useState(false);

  const agregarItem = useCallback((producto: Producto) => {
    setItems(prev => {
      // Solo se agrupa (se suma cantidad) con una fila todavía no comandada: una
      // fila ya persistida en base de datos (id GUID real, dentro de una comanda
      // existente) nunca debe mezclarse — un alta nueva sobre un producto ya
      // comandado tiene que aparecer como una fila aparte en el ticket.
      const existente = prev.find(
        i => i.producto.id === producto.id && !(isGuid(i.id) && !!comandaId)
      );
      if (existente) {
        return prev.map(i =>
          i.id === existente.id
            ? { ...i, cantidad: i.cantidad + 1, subtotal: (i.cantidad + 1) * i.producto.precio }
            : i
        );
      }
      return [
        ...prev,
        {
          id: `item-${producto.id}-${Date.now()}`,
          producto,
          cantidad: 1,
          subtotal: producto.precio,
        },
      ];
    });
    setHuboModificaciones(true);
  }, [comandaId]);

  const quitarItem = useCallback((itemId: string) => {
    setItems(prev => prev.filter(i => i.id !== itemId));
    setHuboModificaciones(true);
  }, []);

  const incrementar = useCallback((itemId: string) => {
    setItems(prev =>
      prev.map(i =>
        i.id === itemId && !i.cancelado
          ? { ...i, cantidad: i.cantidad + 1, subtotal: (i.cantidad + 1) * i.producto.precio }
          : i
      )
    );
    setHuboModificaciones(true);
  }, []);

  const decrementar = useCallback((itemId: string) => {
    setItems(prev =>
      prev
        .map(i =>
          i.id === itemId && !i.cancelado
            ? { ...i, cantidad: i.cantidad - 1, subtotal: (i.cantidad - 1) * i.producto.precio }
            : i
        )
        .filter(i => i.cancelado || i.cantidad > 0)
    );
    setHuboModificaciones(true);
  }, []);

  const marcarItemCancelado = useCallback((itemId: string) => {
    setItems(prev =>
      prev.map(i => (i.id === itemId ? { ...i, cancelado: true } : i))
    );
  }, []);

  const limpiarComanda = useCallback(() => {
    setItems([]);
    setComandaId(null);
    setHuboModificaciones(false);
  }, []);

  const setComandaDetails = useCallback((id: string | null, newItems: ItemComanda[]) => {
    setComandaId(id);
    setItems(newItems);
    setHuboModificaciones(false);
  }, []);

  const total = items.filter(i => !i.cancelado).reduce((sum, i) => sum + i.subtotal, 0);
  const cantidadItems = items.filter(i => !i.cancelado).reduce((sum, i) => sum + i.cantidad, 0);

  return (
    <ComandaContext.Provider
      value={{
        items,
        comandaId,
        total,
        cantidadItems,
        huboModificaciones,
        agregarItem,
        quitarItem,
        incrementar,
        decrementar,
        limpiarComanda,
        setComandaDetails,
        marcarItemCancelado
      }}
    >
      {children}
    </ComandaContext.Provider>
  );
}

export function useComanda() {
  const ctx = useContext(ComandaContext);
  if (!ctx) throw new Error('useComanda debe usarse dentro de ComandaProvider');
  return ctx;
}
