// ============================================
// BARES FAMILIA — TypeScript Type Definitions
// ============================================

// --- Catálogo ---

export interface Sucursal {
  id: string;
  nombre: string;
  direccion: string;
  cuit?: string;
  razonSocial?: string;
  domicilioFiscal?: string;
  condicionIva?: number;
  puntoDeVenta?: number;
  numeroIIBB?: string;
  fechaInicioActividades?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Categoria {
  id: string;
  nombre: string;
  ordenVisual: number;
  isActive: boolean;
}

export interface Producto {
  id: string;
  categoriaId: string;
  nombre: string;
  colorUi: string;
  requiereCocina: boolean;
  alicuotaIva?: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  categoria?: Categoria;
}

// --- Transaccional ---

export type ComandaEstado = 'Abierta' | 'Cobrada' | 'Anulada';
export type SyncEstado = 'Pendiente' | 'Sincronizado' | 'Conflicto';

export interface Comanda {
  id: string;
  sucursalId: string;
  mesaId: string | null;
  usuarioId: string;
  tipoVentaId: string;
  numero: number;
  estado: ComandaEstado;
  total: number;
  descuento: number;
  syncEstado: SyncEstado;
  createdAt: string;
  updatedAt: string;
  items?: ComandaItem[];
  pagos?: Pago[];
}

export interface ComandaItem {
  id: string;
  comandaId: string;
  productoId: string;
  cantidad: number;
  precioUnitario: number;
  notas: string | null;
  producto?: Producto;
}

export interface Pago {
  id: string;
  comandaId: string;
  metodoPagoId: string;
  monto: number;
  createdAt: string;
}

// --- Seguridad ---

export interface Rol {
  id: string;
  nombre: string;
  permisos: string[];
  esGlobal: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Empleado {
  id: string;
  nombre: string;
  email: string;
  pinAcceso: string;
  rolId: string;
  rolNombre: string;
  sucursalId: string;
  sucursalNombre: string;
  isActive: boolean;
}

export interface DispositivoActivacion {
  id: string;
  sucursalId: string;
  codigoActivacion: string;
  descripcion: string;
  isActivado: boolean;
  activadoEn: string | null;
  expiraEn: string;
  createdAt: string;
  sucursal?: Sucursal;
}

// --- Reportes ---

export interface VentaReporte {
  id: string;
  fecha: string;
  sucursal: string;
  comandaNumero: number;
  tipoVenta: string;
  items: number;
  subtotal: number;
  descuento: number;
  total: number;
  metodoPago: string;
  estado: ComandaEstado;
  syncEstado: SyncEstado;
}

// --- Sidebar ---

export interface NavItem {
  label: string;
  path: string;
  icon: React.ComponentType<{ size?: number; className?: string }>;
  badge?: number;
}
