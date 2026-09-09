import { useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '@/services/api';

/**
 * Hook que verifica periódicamente el estado de activación del dispositivo POS.
 * Si detecta que el dispositivo fue desactivado (ej: revocado desde el Backoffice),
 * limpia el localStorage y redirige a la pantalla de activación.
 *
 * @param intervalMs - Intervalo de polling en milisegundos (default: 30000ms = 30s)
 * @param enabled - Si el polling está habilitado (default: true)
 */
export function useActivationStatus(intervalMs = 30000, enabled = true) {
  const navigate = useNavigate();
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const checkStatus = useCallback(async () => {
    try {
      const res = await api.get('/dispositivo/estado');
      if (!res.data.activado) {
        // Dispositivo desactivado remotamente — limpiar y redirigir
        localStorage.removeItem('bf_pos_activado');
        navigate('/activar-pos', { replace: true });
      }
    } catch {
      // Si la API local no responde, no hacer nada (puede estar reiniciándose)
    }
  }, [navigate]);

  useEffect(() => {
    if (!enabled) return;

    // Check inmediato al montar
    checkStatus();

    // Polling periódico
    intervalRef.current = setInterval(checkStatus, intervalMs);

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, [checkStatus, intervalMs, enabled]);

  return { checkStatus };
}
