/* ============================================
   BARES FAMILIA — POS Type Definitions
   ============================================ */

/** Roles del sistema POS */
export type RolUsuario = string;

/** Usuario autenticado por PIN */
export interface UsuarioPOS {
  id: string | number;
  nombre: string;
  pin: string;
  rol: RolUsuario;
  permisos: string[];
}

/** Categoría de productos */
export interface Categoria {
  id: number;
  nombre: string;
  icono: string;
  color: string;
}

/** Producto del catálogo */
export interface Producto {
  id: number;
  nombre: string;
  precio: number;
  categoriaId: number;
  disponible: boolean;
  imagen?: string;
}

/** Ítem dentro de una comanda */
export interface ItemComanda {
  id: string;
  producto: Producto;
  cantidad: number;
  subtotal: number;
  notas?: string;
  /** true si el ítem fue anulado (ya comandado): queda visible pero bloqueado. */
  cancelado?: boolean;
}

/** Comanda activa */
export interface Comanda {
  id: string;
  mesa?: number;
  items: ItemComanda[];
  total: number;
  creadaEn: Date;
  usuarioId: number;
}

/** Cliente Habitual (Cuentas Corrientes) */
export interface Cliente {
  id: string;
  nombre: string;
  apellido: string;
  telefono?: string | null;
  email?: string | null;
  limiteCredito: number;
  saldoActual: number;
}

/** Método de pago */
export interface MetodoPago {
  id: string;
  nombre: string;
  comisionPorcentaje: number;
  requiereFacturaAfip: boolean;
  esCuentaCorriente: boolean;
}

/** Ítem de pago parcial en el cobro */
export interface PagoItem {
  metodoPagoId: string;
  nombre: string;
  monto: number;
  vuelto?: number;
  clienteId?: string | null;
}

/** Movimiento de cuenta corriente */
export interface MovimientoCuentaCorriente {
  id: string;
  tipo: 'Cargo' | 'Pago';
  monto: number;
  detalle: string;
  comandaId?: string | null;
  fecha: string;
}
