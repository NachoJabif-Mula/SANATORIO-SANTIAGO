import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '@/contexts/AppContext';
import { useActivationStatus } from '@/common/hooks/useActivationStatus';

interface PrivateRouteProps {
  children: ReactNode;
  /** Permiso requerido para acceder. Si no se especifica, cualquier usuario autenticado puede acceder. */
  requiredPermission?: string;
}

const PrivateRoute: React.FC<PrivateRouteProps> = ({ children, requiredPermission }) => {
  const { autenticado, tienePermiso, usuario } = useAuth();
  const location = useLocation();

  // Polling de estado de activación cada 30s — si el dispositivo fue revocado
  // desde el backoffice, redirige automáticamente a /activar-pos
  useActivationStatus(30000, autenticado);

  if (!autenticado) {
    // Si no está autenticado, redirige a la pantalla de login
    return <Navigate to="/login" replace />;
  }

  // Si el usuario es mozo, solo puede ver el mapa de mesas (/) y cargar pedidos (/pos)
  const esMozo = usuario && (usuario.rol === 'mozo' || usuario.rol === 'moso');
  if (esMozo && location.pathname !== '/' && location.pathname !== '/pos') {
    return <Navigate to="/" replace />;
  }

  // Verificamos si la ruta tiene restricción de permisos
  if (requiredPermission && !tienePermiso(requiredPermission)) {
    // Si el usuario no tiene el permiso requerido, lo redirige al home/ventas
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
};

export default PrivateRoute;
