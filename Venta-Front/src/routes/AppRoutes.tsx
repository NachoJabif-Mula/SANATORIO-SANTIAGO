import { Routes, Route } from 'react-router-dom';
import PrivateRoute from '@/common/components/PrivateRoute';
import { ComandaProvider } from '@/contexts/AppContext';
import LoginScreen from '@/pages/Login/LoginScreen';
import ActivarPosScreen from '@/pages/POS/ActivarPosScreen';
import PosScreen from '@/pages/POS/PosScreen';
import PlanoSalonesScreen from '@/pages/POS/PlanoSalonesScreen';
import EgresosScreen from '@/pages/POS/EgresosScreen';
import CierreCajaScreen from '@/pages/POS/CierreCajaScreen';
import PosAdminScreen from '@/pages/POS/PosAdminScreen';
import HistorialVentasScreen from '@/pages/POS/HistorialVentasScreen';
import CuentasCorrientesScreen from '@/pages/POS/CuentasCorrientesScreen';

const AppRoutes: React.FC = () => {
  return (
    <Routes>
      {/* Ruta pública — Login por PIN */}
      <Route path="/login" element={<LoginScreen />} />
      <Route path="/activar-pos" element={<ActivarPosScreen />} />

      {/* Rutas protegidas — Solo accesibles si el usuario está autenticado */}
      <Route
        path="/"
        element={
          <PrivateRoute requiredPermission="pos.vender">
            <PlanoSalonesScreen />
          </PrivateRoute>
        }
      />

      <Route
        path="/pos"
        element={
          <PrivateRoute requiredPermission="pos.vender">
            <ComandaProvider>
              <PosScreen />
            </ComandaProvider>
          </PrivateRoute>
        }
      />

      {/* Rutas exclusivas por permiso */}
      <Route path="/egresos" element={
        <PrivateRoute requiredPermission="pos.egresos">
          <EgresosScreen />
        </PrivateRoute>
      } />

      <Route path="/cierre-caja" element={
        <PrivateRoute requiredPermission="pos.cierre_caja">
          <CierreCajaScreen />
        </PrivateRoute>
      } />

      <Route path="/pos-admin" element={
        <PrivateRoute requiredPermission="pos.vender">
          <PosAdminScreen />
        </PrivateRoute>
      } />

      <Route path="/historial-ventas" element={
        <PrivateRoute requiredPermission="pos.vender">
          <HistorialVentasScreen />
        </PrivateRoute>
      } />

      <Route path="/cuentas-corrientes" element={
        <PrivateRoute requiredPermission="pos.cuentas_corrientes">
          <CuentasCorrientesScreen />
        </PrivateRoute>
      } />
    </Routes>
  );
};

export default AppRoutes;
