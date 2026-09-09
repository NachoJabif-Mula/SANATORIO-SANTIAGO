import axios from 'axios';

/**
 * Instancia de Axios pre-configurada para la API Nube de BARES FAMILIA.
 * Incluye interceptor de request que inyecta el JWT del Administrador
 * automáticamente en cada petición saliente.
 *
 * El token se busca primero en localStorage (sesión persistente)
 * y luego en sessionStorage (sesión temporal).
 */
const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5280/api',
  timeout: 15000,
  headers: {
    'Content-Type': 'application/json',
  },
});

// ========================================
// REQUEST INTERCEPTOR — Inyección JWT
// ========================================

api.interceptors.request.use(
  (config) => {
    // Buscar token en ambos storages (localStorage tiene prioridad)
    const token = localStorage.getItem('bf_admin_token') || sessionStorage.getItem('bf_admin_token');

    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// ========================================
// RESPONSE INTERCEPTOR — Manejo de errores
// ========================================

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response) {
      const { status } = error.response;

      // Token expirado o inválido
      if (status === 401) {
        localStorage.removeItem('bf_admin_token');
        sessionStorage.removeItem('bf_admin_token');
        localStorage.removeItem('bf_storage_type');
        // Redirigir a login si no estamos ya ahí
        if (window.location.pathname !== '/login') {
          window.location.href = '/login';
        }
      }

      // Forbidden
      if (status === 403) {
        console.error('[API] Acceso denegado — permisos insuficientes');
      }
    }

    return Promise.reject(error);
  }
);

export default api;
