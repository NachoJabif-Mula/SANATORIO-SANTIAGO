import { useState, useEffect } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import {
  LayoutDashboard,
  Package,
  Building2,
  Smartphone,
  ChevronLeft,
  ChevronRight,
  ChevronDown,
  Wine,
  LayoutTemplate,
  ClipboardList,
  Printer,
  Users,
  ShieldCheck,
  RefreshCw,
  CreditCard,
  Calendar,
  FileCheck,
  ListTodo,
  Users2,
  Wallet,
  Ban,
  XCircle,
  BarChart3,
  UserCog,
  ShoppingBag,
  Settings
} from 'lucide-react';
import type { NavItem } from '@/common/types';

interface NavGroup {
  key: string;
  label: string;
  icon: NavItem['icon'];
  items: NavItem[];
}

// Ítems sueltos, siempre visibles en la raíz del menú.
const topItems: NavItem[] = [
  { label: 'Dashboard', path: '/', icon: LayoutDashboard },
];

// Ítems agrupados en submenúes desplegables por área.
const navGroups: NavGroup[] = [
  {
    key: 'comercial',
    label: 'Comercial',
    icon: ShoppingBag,
    items: [
      { label: 'Catálogo', path: '/catalogo', icon: Package },
      { label: 'Inventario y Recetas', path: '/inventario', icon: ClipboardList },
      { label: 'Clientes', path: '/clientes', icon: Users2 },
      { label: 'Cuentas Corrientes', path: '/cuentas-corrientes', icon: Wallet },
    ],
  },
  {
    key: 'reportes',
    label: 'Reportes',
    icon: BarChart3,
    items: [
      { label: 'Cierres Diarios', path: '/cierres-diarios', icon: Calendar },
      { label: 'Anulaciones por Ítem', path: '/anulaciones-items', icon: Ban },
      { label: 'Transacciones Anuladas', path: '/transacciones-anuladas', icon: XCircle },
    ],
  },
  {
    key: 'rrhh',
    label: 'RRHH',
    icon: UserCog,
    items: [
      { label: 'Gestión Empleados', path: '/empleados', icon: Users },
      { label: 'Roles de Acceso', path: '/roles', icon: ShieldCheck },
    ],
  },
  {
    key: 'configuracion',
    label: 'Configuración',
    icon: Settings,
    items: [
      { label: 'Sucursales', path: '/sucursales', icon: Building2 },
      { label: 'Constructor POS', path: '/layout-builder', icon: LayoutTemplate },
      { label: 'Medios de Pago', path: '/medios-de-pago', icon: CreditCard },
      { label: 'Activación POS', path: '/activacion-pos', icon: Smartphone },
      { label: 'Impresoras', path: '/impresoras', icon: Printer },
      { label: 'Cola Impresión', path: '/cola-impresion', icon: ListTodo },
      { label: 'Sincronización', path: '/sync', icon: RefreshCw },
      { label: 'Config. Fiscal', path: '/configuracion-fiscal', icon: FileCheck },
    ],
  },
];

function isPathActive(pathname: string, itemPath: string): boolean {
  return pathname === itemPath || (itemPath !== '/' && pathname.startsWith(itemPath));
}

function findActiveGroupKey(pathname: string): string | null {
  return navGroups.find(g => g.items.some(i => isPathActive(pathname, i.path)))?.key ?? null;
}

export default function Sidebar() {
  const [collapsed, setCollapsed] = useState(false);
  const location = useLocation();

  const [openGroup, setOpenGroup] = useState<string | null>(() => findActiveGroupKey(location.pathname));

  // Mantiene abierto el grupo que contiene la ruta activa (p.ej. al navegar por URL directa).
  useEffect(() => {
    const active = findActiveGroupKey(location.pathname);
    if (active) setOpenGroup(active);
  }, [location.pathname]);

  function toggleGroup(key: string) {
    setOpenGroup(prev => (prev === key ? null : key));
  }

  function renderLink(item: NavItem) {
    const isActive = isPathActive(location.pathname, item.path);
    const Icon = item.icon as any;

    return (
      <NavLink
        key={item.path}
        to={item.path}
        className={`
          group relative flex items-center gap-3 px-3 py-2.5 rounded-lg
          transition-all duration-200 ease-out font-medium
          ${collapsed ? 'justify-center' : ''}
          ${isActive
            ? 'bg-brand-50 text-brand-700 shadow-sm'
            : 'text-pearl-500 hover:bg-ice-100 hover:text-pearl-800'
          }
        `}
      >
        {isActive && (
          <span className="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-5 bg-brand-500 rounded-r-full shadow-[0_0_8px_rgba(37,99,235,0.4)]" />
        )}
        <Icon
          size={20}
          strokeWidth={isActive ? 2.5 : 2}
          className={`shrink-0 transition-colors duration-200 ${
            isActive ? 'text-brand-600' : 'text-pearl-400 group-hover:text-pearl-600'
          }`}
        />
        {!collapsed && (
          <span className="text-sm truncate animate-fade-in">
            {item.label}
          </span>
        )}
        {!collapsed && item.badge && (
          <span className="ml-auto text-[10px] font-bold bg-brand-500 text-white px-2 py-0.5 rounded-full shadow-sm">
            {item.badge}
          </span>
        )}
      </NavLink>
    );
  }

  function renderGroup(group: NavGroup) {
    const GroupIcon = group.icon as any;
    const isGroupActive = group.items.some(i => isPathActive(location.pathname, i.path));
    const isOpen = openGroup === group.key;

    // En modo colapsado (solo íconos) no hay espacio para desplegar submenúes:
    // se listan los ítems del grupo directamente, sin encabezado de grupo.
    if (collapsed) {
      return (
        <div key={group.key} className="space-y-1">
          {group.items.map(renderLink)}
        </div>
      );
    }

    return (
      <div key={group.key}>
        <button
          type="button"
          onClick={() => toggleGroup(group.key)}
          className={`
            w-full flex items-center gap-3 px-3 py-2.5 rounded-lg
            transition-all duration-200 ease-out font-medium cursor-pointer
            ${isGroupActive ? 'text-pearl-800' : 'text-pearl-500 hover:bg-ice-100 hover:text-pearl-800'}
          `}
        >
          <GroupIcon
            size={20}
            strokeWidth={isGroupActive ? 2.5 : 2}
            className={`shrink-0 transition-colors duration-200 ${isGroupActive ? 'text-brand-600' : 'text-pearl-400'}`}
          />
          <span className="text-sm truncate flex-1 text-left">{group.label}</span>
          <ChevronDown
            size={15}
            className={`shrink-0 text-pearl-400 transition-transform duration-200 ${isOpen ? 'rotate-180' : ''}`}
          />
        </button>

        <div
          className={`grid transition-all duration-200 ease-out ${isOpen ? 'grid-rows-[1fr] opacity-100 mt-1' : 'grid-rows-[0fr] opacity-0'}`}
        >
          <div className="overflow-hidden">
            <div className="pl-4 border-l border-pearl-100 ml-5 space-y-1">
              {group.items.map(renderLink)}
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <aside
      className={`
        relative flex flex-col h-screen bg-white border-r border-pearl-200
        transition-all duration-300 ease-in-out
        ${collapsed ? 'w-[72px]' : 'w-[260px]'}
      `}
      style={{ boxShadow: 'var(--shadow-sidebar)' }}
    >
      {/* Logo & Brand */}
      <div className="flex items-center gap-3 px-5 py-6 border-b border-pearl-100">
        <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-gradient-to-br from-brand-500 to-brand-700 text-white shrink-0">
          <Wine size={22} />
        </div>
        {!collapsed && (
          <div className="animate-fade-in overflow-hidden">
            <h1 className="text-sm font-bold text-pearl-900 tracking-tight leading-none">
              BARES FAMILIA
            </h1>
            <span className="text-[11px] font-medium text-pearl-400 tracking-wider uppercase">
              Backoffice
            </span>
          </div>
        )}
      </div>

      {/* Navigation */}
      <nav className="flex-1 py-4 px-3 space-y-1 overflow-y-auto">
        <span className={`
          block text-[10px] font-semibold text-pearl-400 uppercase tracking-widest mb-3
          ${collapsed ? 'text-center' : 'px-3'}
        `}>
          {collapsed ? '•••' : 'Menú Principal'}
        </span>

        {topItems.map(renderLink)}

        <div className="pt-3 mt-3 border-t border-pearl-100 space-y-1">
          {navGroups.map(renderGroup)}
        </div>
      </nav>

      {/* Footer */}
      <div className="px-3 py-4 border-t border-pearl-100">
        <div className={`flex items-center gap-3 px-3 py-2 ${collapsed ? 'justify-center' : ''}`}>
          <div className="w-8 h-8 rounded-full bg-gradient-to-br from-brand-400 to-brand-600 flex items-center justify-center text-white text-xs font-bold shrink-0">
            AD
          </div>
          {!collapsed && (
            <div className="animate-fade-in overflow-hidden">
              <p className="text-sm font-semibold text-pearl-800 truncate">Admin</p>
              <p className="text-[11px] text-pearl-400 truncate">admin@baresfamilia.com</p>
            </div>
          )}
        </div>
      </div>

      {/* Collapse Button */}
      <button
        id="sidebar-toggle"
        onClick={() => setCollapsed(!collapsed)}
        className="
          absolute -right-3 top-20 z-10
          w-6 h-6 rounded-full bg-white border border-pearl-200
          flex items-center justify-center
          text-pearl-400 hover:text-brand-600 hover:border-brand-300
          shadow-card hover:shadow-card-hover
          transition-all duration-200
          cursor-pointer
        "
        aria-label={collapsed ? 'Expandir sidebar' : 'Colapsar sidebar'}
      >
        {collapsed ? <ChevronRight size={14} /> : <ChevronLeft size={14} />}
      </button>
    </aside>
  );
}
