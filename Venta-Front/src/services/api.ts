import axios from 'axios';

/**
 * Instancia de Axios para conectar Venta-Front con la API Local.
 */
const api = axios.create({
  baseURL: import.meta.env.VITE_LOCAL_API_URL || 'http://localhost:5044/api',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
});

export default api;
