import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from '@/contexts/AuthContext';
import { SucursalProvider } from '@/contexts/SucursalContext';
import PrivateRoute from '@/common/components/PrivateRoute';
import AppLayout from '@/common/components/Layout/AppLayout';
import LoginPage from '@/pages/LoginPage';
import DashboardPage from '@/pages/DashboardPage';
import CatalogoPage from '@/pages/CatalogoPage';
import SucursalesPage from '@/pages/SucursalesPage';
import ActivacionPosPage from '@/pages/ActivacionPosPage';
import LayoutBuilderPage from '@/pages/LayoutBuilderPage';
import InventarioPage from '@/pages/InventarioPage';
import ImpresorasPage from '@/pages/ImpresorasPage';
import RolesPage from '@/pages/RolesPage';
import EmpleadosPage from '@/pages/EmpleadosPage';
import SyncPage from '@/pages/SyncPage';
import MediosPagoPage from '@/pages/MediosPagoPage';
import CierresDiariosPage from '@/pages/CierresDiariosPage';
import AnulacionesItemsPage from '@/pages/AnulacionesItemsPage';
import TransaccionesAnuladasPage from '@/pages/TransaccionesAnuladasPage';
import ClientesPage from '@/pages/ClientesPage';
import CuentasCorrientesPage from '@/pages/CuentasCorrientesPage';
import ConfiguracionFiscalPage from '@/pages/ConfiguracionFiscalPage';
import ColaImpresionPage from '@/pages/ColaImpresionPage';

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <SucursalProvider>
        <Routes>
          {/* Ruta pública — Login */}
          <Route path="/login" element={<LoginPage />} />

          {/* Rutas protegidas — Requieren autenticación */}
          <Route
            element={
              <PrivateRoute>
                <AppLayout />
              </PrivateRoute>
            }
          >
            <Route path="/" element={<DashboardPage />} />
            <Route path="/layout-builder" element={<LayoutBuilderPage />} />
            <Route path="/inventario" element={<InventarioPage />} />
            <Route path="/catalogo" element={<CatalogoPage />} />
            <Route path="/sucursales" element={<SucursalesPage />} />
            <Route path="/medios-de-pago" element={<MediosPagoPage />} />
            <Route path="/cierres-diarios" element={<CierresDiariosPage />} />
            <Route path="/anulaciones-items" element={<AnulacionesItemsPage />} />
            <Route path="/transacciones-anuladas" element={<TransaccionesAnuladasPage />} />
            <Route path="/clientes" element={<ClientesPage />} />
            <Route path="/cuentas-corrientes" element={<CuentasCorrientesPage />} />
            <Route path="/roles" element={<RolesPage />} />
            <Route path="/empleados" element={<EmpleadosPage />} />
            <Route path="/sync" element={<SyncPage />} />
            <Route path="/activacion-pos" element={<ActivacionPosPage />} />
            <Route path="/impresoras" element={<ImpresorasPage />} />
            <Route path="/configuracion-fiscal" element={<ConfiguracionFiscalPage />} />
            <Route path="/cola-impresion" element={<ColaImpresionPage />} />
          </Route>
        </Routes>
        </SucursalProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
