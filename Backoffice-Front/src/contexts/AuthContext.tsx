import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import api from '@/services/api';

// ════════════════════════════════════════
// Tipos
// ════════════════════════════════════════

interface User {
  id: string;
  nombre: string;
  email: string;
  rol: string;
  sucursal: string;
  sucursalId: string;
  esGlobal: boolean;
}

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string, rememberMe: boolean) => Promise<string | null>;
  logout: () => void;
}

// ════════════════════════════════════════
// Context
// ════════════════════════════════════════

const AuthContext = createContext<AuthContextType | null>(null);

// ════════════════════════════════════════
// Helpers de storage
// ════════════════════════════════════════

const TOKEN_KEY = 'bf_admin_token';
const STORAGE_TYPE_KEY = 'bf_storage_type';

/** Obtiene el token de donde esté guardado */
function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY) || sessionStorage.getItem(TOKEN_KEY);
}

/** Guarda el token en el storage indicado */
function storeToken(token: string, persistent: boolean): void {
  // Limpiar ambos primero
  localStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(STORAGE_TYPE_KEY);

  if (persistent) {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(STORAGE_TYPE_KEY, 'local');
  } else {
    sessionStorage.setItem(TOKEN_KEY, token);
  }
}

/** Elimina el token de ambos storages */
function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(STORAGE_TYPE_KEY);
}

// ════════════════════════════════════════
// Provider
// ════════════════════════════════════════

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Al montar, verificar si existe un token válido
  useEffect(() => {
    const token = getStoredToken();
    if (!token) {
      setIsLoading(false);
      return;
    }

    api.get('/auth/me')
      .then((res) => setUser(res.data))
      .catch(() => {
        clearToken();
        setUser(null);
      })
      .finally(() => setIsLoading(false));
  }, []);

  const login = useCallback(async (email: string, password: string, rememberMe: boolean): Promise<string | null> => {
    try {
      const res = await api.post('/auth/login', { email, password, rememberMe });
      const { token, usuario } = res.data;

      storeToken(token, rememberMe);
      setUser(usuario);
      return null; // null = éxito (sin error)
    } catch (err: any) {
      const message = err.response?.data?.message || 'Error de conexión con el servidor.';
      return message;
    }
  }, []);

  const logout = useCallback(() => {
    clearToken();
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: !!user, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

// ════════════════════════════════════════
// Hook
// ════════════════════════════════════════

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthProvider>');
  return ctx;
}
