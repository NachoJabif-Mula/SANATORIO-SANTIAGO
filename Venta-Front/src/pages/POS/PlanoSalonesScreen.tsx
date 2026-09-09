import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  LogOut,
  User,
  Layers,
  DoorOpen,
  Leaf,
  Wine,
  Music,
  Maximize2,
  KeyRound,
  Sun,
  Moon,
  Wallet
} from 'lucide-react';
import { useAuth } from '@/contexts/AppContext';
import { useCaja } from '@/contexts/CajaContext';
import ModalAperturaTurno from '@/common/components/POS/ModalAperturaTurno';
import { ModalCliente } from '@/common/components/POS/ModalCliente';
import type { Cliente } from '@/common/types';
import api from '@/services/api';

// Interfaces para tipado estricto
interface TableItem {
  id: string;
  type: 'square' | 'circle' | 'rectangle' | 'decoration';
  isDecoration?: boolean;
  decorType?: 'bar' | 'plant' | 'restroom' | 'door' | 'stage' | 'wall';
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  label: string;
  color: string;
  seats: number;
}

interface TableMap {
  id: string;
  name: string;
  bgType: 'color' | 'texture' | 'image';
  bgColor: string;
  bgPattern: 'none' | 'grid' | 'dots';
  bgTexture: string;
  bgImage: string;
  bgImageFit: 'cover' | 'contain' | 'repeat';
  tables: TableItem[];
  targetWidth: number;
  targetHeight: number;
}

// Mapas iniciales por defecto (idénticos a los del backoffice en caso de que localStorage esté vacío)
const DEFAULT_MAPS: TableMap[] = [
  {
    id: 'map-1',
    name: 'Salón Principal',
    bgType: 'color',
    bgColor: '#ffffff',
    bgPattern: 'grid',
    bgTexture: '',
    bgImage: '',
    bgImageFit: 'cover',
    targetWidth: 1024,
    targetHeight: 768,
    tables: [
      { id: 't1', type: 'square', x: 15, y: 20, width: 10, height: 10, rotation: 0, label: 'Mesa 1', color: '#3b82f6', seats: 4 },
      { id: 't2', type: 'circle', x: 45, y: 20, width: 10, height: 10, rotation: 0, label: 'Mesa 2', color: '#10b981', seats: 4 },
      { id: 't3', type: 'rectangle', x: 70, y: 30, width: 16, height: 10, rotation: 90, label: 'Familiar 1', color: '#f59e0b', seats: 8 },
      { id: 't4', type: 'square', x: 15, y: 60, width: 10, height: 10, rotation: 45, label: 'Mesa 4', color: '#8b5cf6', seats: 4 },
      { id: 't5', type: 'circle', x: 45, y: 60, width: 12, height: 12, rotation: 0, label: 'VIP Redonda', color: '#ef4444', seats: 6 },
    ]
  },
  {
    id: 'map-2',
    name: 'Terraza Exterior',
    bgType: 'texture',
    bgColor: '',
    bgPattern: 'none',
    bgTexture: 'grass',
    bgImage: '',
    bgImageFit: 'cover',
    targetWidth: 1280,
    targetHeight: 800,
    tables: [
      { id: 't201', type: 'circle', x: 20, y: 20, width: 10, height: 10, rotation: 0, label: 'T-1', color: '#d97706', seats: 4 },
      { id: 't202', type: 'circle', x: 50, y: 25, width: 10, height: 10, rotation: 0, label: 'T-2', color: '#d97706', seats: 4 },
      { id: 't203', type: 'circle', x: 75, y: 30, width: 10, height: 10, rotation: 0, label: 'T-3', color: '#d97706', seats: 4 },
      { id: 'd201', type: 'decoration', isDecoration: true, decorType: 'bar', x: 30, y: 70, width: 35, height: 8, rotation: 0, label: 'Barra Exterior', color: '#d97706', seats: 0 },
      { id: 'd202', type: 'decoration', isDecoration: true, decorType: 'plant', x: 10, y: 10, width: 5, height: 5, rotation: 0, label: 'Palmera', color: '#10b981', seats: 0 },
      { id: 'd203', type: 'decoration', isDecoration: true, decorType: 'plant', x: 85, y: 10, width: 5, height: 5, rotation: 0, label: 'Arbusto', color: '#10b981', seats: 0 },
    ]
  },
  {
    id: 'map-3',
    name: 'Sector VIP',
    bgType: 'texture',
    bgColor: '',
    bgPattern: 'none',
    bgTexture: 'slate',
    bgImage: '',
    bgImageFit: 'cover',
    targetWidth: 1920,
    targetHeight: 1080,
    tables: [
      { id: 't301', type: 'rectangle', x: 30, y: 40, width: 18, height: 12, rotation: 0, label: 'VIP Imperial', color: '#f43f5e', seats: 10 },
      { id: 't302', type: 'square', x: 65, y: 35, width: 12, height: 12, rotation: 15, label: 'VIP Gold', color: '#f59e0b', seats: 6 },
      { id: 'd301', type: 'decoration', isDecoration: true, decorType: 'stage', x: 8, y: 10, width: 25, height: 15, rotation: 0, label: 'Escenario VIP', color: '#6366f1', seats: 0 },
      { id: 'd302', type: 'decoration', isDecoration: true, decorType: 'restroom', x: 85, y: 80, width: 12, height: 12, rotation: 0, label: 'Baños', color: '#4b5563', seats: 0 },
    ]
  }
];

function formatARS(monto: number): string {
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(monto);
}

export default function PlanoSalonesScreen() {
  const { usuario, logout, tienePermiso, theme, toggleTheme } = useAuth();
  const { turnoActivo, cargando: cargandoCaja, refrescarTurno } = useCaja();
  const navigate = useNavigate();
  const [maps, setMaps] = useState<TableMap[]>([]);
  const [activeMapId, setActiveMapId] = useState<string>('');
  const [openMesaIds, setOpenMesaIds] = useState<Set<string>>(new Set());
  const [dbMesas, setDbMesas] = useState<any[]>([]);
  const [openComandas, setOpenComandas] = useState<any[]>([]);
  const [showModalClienteCC, setShowModalClienteCC] = useState(false);

  // Cuentas corrientes abiertas: comandas exentas de turno a nombre de un cliente
  const cuentasAbiertas = openComandas.filter((c: any) => !!c.clienteId);

  // --- Verificar Activación y Cargar Planos ---
  useEffect(() => {
    const checkAndLoad = async () => {
      try {
        // 1. Verificar estado de activación local
        const stateRes = await api.get('/dispositivo/estado');
        if (!stateRes.data.activado) {
          navigate('/activar-pos', { replace: true });
          return;
        }

        // 2. Cargar planos de la base de datos local
        const configRes = await api.get('/configuracionpos');
        const data = configRes.data;

        if (data && data.length > 0) {
          const loadedMaps = data.map((cfg: any) => {
            let parsedJson: any = {};
            try {
              parsedJson = JSON.parse(cfg.configuracionJson);
            } catch (e) {}

            return {
              id: cfg.id,
              name: cfg.nombre,
              bgType: parsedJson.bgType || 'color',
              bgColor: parsedJson.bgColor || '#ffffff',
              bgPattern: parsedJson.bgPattern || 'none',
              bgTexture: parsedJson.bgTexture || '',
              bgImage: parsedJson.bgImage || '',
              bgImageFit: parsedJson.bgImageFit || 'cover',
              targetWidth: parsedJson.targetWidth || 1024,
              targetHeight: parsedJson.targetHeight || 768,
              tables: parsedJson.tables || []
            };
          });

          setMaps(loadedMaps);
          setActiveMapId(loadedMaps[0].id);
        } else {
          setMaps(DEFAULT_MAPS);
          if (DEFAULT_MAPS.length > 0) {
            setActiveMapId(DEFAULT_MAPS[0].id);
          }
        }

        // 3. Cargar mesas de la base de datos local
        try {
          const mesasRes = await api.get('/catalogo/mesas');
          setDbMesas(mesasRes.data || []);
        } catch (e) {
          console.error('Error al cargar mesas del catálogo:', e);
        }

        // 4. Cargar comandas abiertas para identificar mesas ocupadas
        try {
          const comRes = await api.get('/comanda');
          const activeOpenComandas = (comRes.data || []).filter((c: any) => c.estado === 0 || c.estado === 'Abierta');
          setOpenComandas(activeOpenComandas);
          const ids = new Set<string>(activeOpenComandas.map((c: any) => c.mesaId).filter(Boolean));
          setOpenMesaIds(ids);
        } catch (e) {
          console.error('Error al cargar comandas abiertas:', e);
        }
      } catch (err) {
        console.error('Error al iniciar PlanoSalonesScreen:', err);
        setMaps(DEFAULT_MAPS);
        if (DEFAULT_MAPS.length > 0) {
          setActiveMapId(DEFAULT_MAPS[0].id);
        }
      }
    };

    checkAndLoad();
  }, [navigate]);

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  const handleTableClick = (table: TableItem) => {
    // 1. Intentar encontrar la mesa correspondiente en la base de datos por etiqueta
    const dbMesa = dbMesas.find(
      (m: any) => m.etiqueta.toLowerCase() === table.label.toLowerCase()
    );

    // Usar el ID de la base de datos (GUID) si existe, o el ID visual de fallback
    const targetMesaId = dbMesa ? dbMesa.id : table.id;

    // 2. Verificar si está ocupada y buscar su comanda activa
    const isOccupied = dbMesa ? dbMesa.ocupada : openMesaIds.has(table.id);
    const activeComanda = isOccupied
      ? openComandas.find((c: any) => c.mesaId === targetMesaId)
      : null;

    if (activeComanda) {
      // Redirigir a editar el pedido activo
      navigate(`/pos?comandaId=${activeComanda.id}&mesaId=${targetMesaId}&mesaName=${encodeURIComponent(table.label)}`);
    } else {
      // Redirigir a crear nueva comanda
      navigate(`/pos?mesaId=${targetMesaId}&mesaName=${encodeURIComponent(table.label)}`);
    }
  };

  const handleClienteSeleccionado = (cliente: Cliente) => {
    setShowModalClienteCC(false);
    const nombreCompleto = `${cliente.nombre} ${cliente.apellido}`;
    // Si el cliente ya tiene una cuenta corriente abierta, continuarla en vez de abrir una nueva.
    const cuentaAbierta = openComandas.find((c: any) => c.clienteId === cliente.id);
    if (cuentaAbierta) {
      navigate(`/pos?comandaId=${cuentaAbierta.id}&clienteId=${cliente.id}&clienteNombre=${encodeURIComponent(nombreCompleto)}`);
    } else {
      navigate(`/pos?clienteId=${cliente.id}&clienteNombre=${encodeURIComponent(nombreCompleto)}`);
    }
  };

  const activeMap = maps.find(m => m.id === activeMapId) || maps[0];

  // --- Contraste del texto ---
  const getContrastColor = (hexColor: string) => {
    if (!hexColor || !hexColor.startsWith('#')) return '#ffffff';
    const r = parseInt(hexColor.slice(1, 3), 16);
    const g = parseInt(hexColor.slice(3, 5), 16);
    const b = parseInt(hexColor.slice(5, 7), 16);
    const yiq = (r * 299 + g * 587 + b * 114) / 1000;
    return yiq >= 128 ? '#111218' : '#ffffff';
  };

  // --- Sillas alrededor de las mesas ---
  const renderChairs = (seats: number, type: 'square' | 'circle' | 'rectangle') => {
    const chairs = [];
    const chairSize = 'w-3.5 h-3.5';
    
    if (type === 'circle') {
      for (let i = 0; i < seats; i++) {
        const angle = (i * 2 * Math.PI) / seats - Math.PI / 2;
        const r = 56;
        const left = 50 + r * Math.cos(angle);
        const top = 50 + r * Math.sin(angle);
        
        chairs.push(
          <div
            key={`chair-${i}`}
            className={`absolute ${chairSize} bg-slate-900 border border-border-default rounded-full -translate-x-1/2 -translate-y-1/2 shadow-xs`}
            style={{ left: `${left}%`, top: `${top}%` }}
          />
        );
      }
    } else {
      const sides: number[][] = [[], [], [], []];
      for (let i = 0; i < seats; i++) {
        sides[i % 4].push(i);
      }
      
      // Superior
      sides[0].forEach((id, idx) => {
        const count = sides[0].length;
        const left = count === 1 ? 50 : 20 + (idx * 60) / (count - 1);
        chairs.push(
          <div
            key={`chair-top-${id}`}
            className={`absolute ${chairSize} bg-slate-900 border border-border-default rounded-[2px] -translate-x-1/2 -translate-y-1/2 shadow-xs`}
            style={{ left: `${left}%`, top: '-2px' }}
          />
        );
      });
      
      // Derecho
      sides[1].forEach((id, idx) => {
        const count = sides[1].length;
        const top = count === 1 ? 50 : 20 + (idx * 60) / (count - 1);
        chairs.push(
          <div
            key={`chair-right-${id}`}
            className={`absolute ${chairSize} bg-slate-900 border border-border-default rounded-[2px] -translate-x-1/2 -translate-y-1/2 shadow-xs`}
            style={{ left: 'calc(100% + 2px)', top: `${top}%` }}
          />
        );
      });
      
      // Inferior
      sides[2].forEach((id, idx) => {
        const count = sides[2].length;
        const left = count === 1 ? 50 : 20 + (idx * 60) / (count - 1);
        chairs.push(
          <div
            key={`chair-bottom-${id}`}
            className={`absolute ${chairSize} bg-slate-900 border border-border-default rounded-[2px] -translate-x-1/2 -translate-y-1/2 shadow-xs`}
            style={{ left: `${left}%`, top: 'calc(100% + 2px)' }}
          />
        );
      });
      
      // Izquierdo
      sides[3].forEach((id, idx) => {
        const count = sides[3].length;
        const top = count === 1 ? 50 : 20 + (idx * 60) / (count - 1);
        chairs.push(
          <div
            key={`chair-left-${id}`}
            className={`absolute ${chairSize} bg-slate-900 border border-border-default rounded-[2px] -translate-x-1/2 -translate-y-1/2 shadow-xs`}
            style={{ left: '-2px', top: `${top}%` }}
          />
        );
      });
    }
    return chairs;
  };

  // --- Renderizar Iconos de decoración ---
  const renderDecorationIcon = (decorType?: string) => {
    const size = 18;
    switch (decorType) {
      case 'bar':
        return <Wine size={size} className="text-amber-800" />;
      case 'plant':
        return <Leaf size={size} className="text-emerald-700" />;
      case 'door':
        return <DoorOpen size={size} className="text-amber-950" />;
      case 'restroom':
        return <span className="text-[10px] font-extrabold font-mono tracking-tighter text-slate-350">WC</span>;
      case 'stage':
        return <Music size={size} className="text-indigo-900" />;
      case 'wall':
        return <div className="w-full h-full bg-slate-600 border border-slate-700 opacity-60" />;
      default:
        return null;
    }
  };

  // --- Estilos de lienzo ---
  const getCanvasStyles = (): React.CSSProperties => {
    if (!activeMap) return {};
    const styles: React.CSSProperties = {};

    if (activeMap.bgType === 'color') {
      styles.backgroundColor = activeMap.bgColor || '#ffffff';
    } else if (activeMap.bgType === 'texture') {
      if (activeMap.bgTexture === 'wood') {
        styles.backgroundColor = '#b45309';
        styles.backgroundImage = 'repeating-linear-gradient(90deg, #d97706 0px, #d97706 40px, #b45309 40px, #b45309 44px)';
      } else if (activeMap.bgTexture === 'slate') {
        styles.backgroundColor = '#1e293b';
        styles.backgroundImage = 'radial-gradient(rgba(255,255,255,0.06) 1.5px, transparent 1.5px)';
        styles.backgroundSize = '24px 24px';
      } else if (activeMap.bgTexture === 'marble') {
        styles.backgroundColor = '#f1f5f9';
        styles.backgroundImage = 'linear-gradient(135deg, rgba(255,255,255,0.9) 0%, rgba(226,232,240,0.85) 100%)';
      } else if (activeMap.bgTexture === 'grass') {
        styles.backgroundColor = '#166534';
        styles.backgroundImage = 'radial-gradient(#15803d 2px, transparent 0)';
        styles.backgroundSize = '16px 16px';
      }
    } else if (activeMap.bgType === 'image') {
      if (activeMap.bgImage) {
        styles.backgroundImage = `url(${activeMap.bgImage})`;
        styles.backgroundPosition = 'center';
        if (activeMap.bgImageFit === 'cover') {
          styles.backgroundSize = 'cover';
          styles.backgroundRepeat = 'no-repeat';
        } else if (activeMap.bgImageFit === 'contain') {
          styles.backgroundSize = 'contain';
          styles.backgroundRepeat = 'no-repeat';
        } else {
          styles.backgroundSize = 'auto';
          styles.backgroundRepeat = 'repeat';
        }
      } else {
        styles.backgroundColor = '#1e293b';
      }
    }
    return styles;
  };

  const esEncargado = usuario && (usuario.rol === 'encargado' || usuario.rol === 'gerente' || usuario.rol === 'administrador' || tienePermiso('caja.apertura'));

  if (cargandoCaja) {
    return (
      <div className="h-screen w-screen flex flex-col items-center justify-center bg-slate-950 text-text-primary">
        <div className="w-10 h-10 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
        <p className="mt-4 text-sm font-semibold text-text-secondary">Verificando estado de caja...</p>
      </div>
    );
  }

  if (!turnoActivo) {
    if (esEncargado) {
      return (
        <ModalAperturaTurno onAperturaExitosa={() => refrescarTurno()} />
      );
    } else {
      return (
        <div className="h-screen w-screen flex flex-col items-center justify-center bg-slate-950 text-text-primary px-4 text-center">
          <div className="w-14 h-14 rounded-[var(--radius-btn)] bg-slate-850 border border-border-default flex items-center justify-center mb-6">
            <KeyRound className="w-6 h-6 text-amber-500" />
          </div>
          <h3 className="text-lg font-bold text-text-primary">Caja Cerrada</h3>
          <p className="text-sm text-text-secondary mt-2 max-w-sm">
            Esperando la apertura del turno de caja por parte de un <span className="font-semibold text-text-primary">Encargado</span> para poder operar.
          </p>
          <button
            onClick={handleLogout}
            className="touch-btn mt-6 flex items-center gap-2 px-6 py-3 rounded-[var(--radius-btn)] bg-slate-900 text-text-secondary hover:text-text-primary hover:bg-slate-850 border border-border-default transition-all font-semibold cursor-pointer active:scale-95"
          >
            <LogOut className="w-4 h-4" />
            Cerrar Sesión
          </button>
        </div>
      );
    }
  }

  return (
    <div className="h-screen w-screen flex flex-col bg-slate-950 overflow-hidden text-text-primary">
      {/* Top Bar */}
      <header className="flex items-center justify-between px-6 py-3 bg-surface-base border-b border-border-default flex-shrink-0 shadow-sm">
        <div className="flex items-center gap-2.5">
          <h1 className="text-sm font-bold tracking-tight text-text-primary">
            Bares <span className="text-text-secondary font-semibold">Familia</span>
          </h1>
          <span className="text-[9px] font-semibold uppercase tracking-wider text-text-muted bg-slate-850 px-2 py-0.5 rounded-[3px] border border-border-default/50">
            Salón de Mesas
          </span>
          {turnoActivo && (
            <span className="text-[10px] font-semibold uppercase tracking-wide text-text-secondary bg-slate-850 px-2.5 py-1 rounded-[3px] border border-border-default/60 flex items-center gap-1.5">
              <span className="w-1.5 h-1.5 rounded-full bg-success-500" />
              {new Date(turnoActivo.fechaContable).toLocaleDateString('es-AR')} · Turno {turnoActivo.turno}
            </span>
          )}
        </div>

        {/* Salons list tabs */}
        <div className="flex items-center bg-slate-900/60 border border-border-default/80 rounded-[var(--radius-btn)] p-0.5 max-w-[50%] overflow-x-auto no-scrollbar gap-1">
          {maps.map(map => (
            <button
              key={map.id}
              onClick={() => setActiveMapId(map.id)}
              className={`flex items-center gap-1 px-3.5 py-1.5 rounded-[var(--radius-btn)] text-xs font-semibold transition-all whitespace-nowrap min-h-[32px] cursor-pointer
                ${activeMapId === map.id
                  ? 'bg-slate-800 text-text-primary border border-border-strong'
                  : 'text-text-secondary hover:text-text-primary hover:bg-slate-900/40 border border-transparent'}
              `}
            >
              <Layers size={11} className={activeMapId === map.id ? 'text-amber-500' : 'text-text-muted'} />
              {map.name}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-2">
          {/* Botón de cambio de Tema Claro/Oscuro */}
          <button 
            onClick={toggleTheme}
            className="touch-btn flex items-center justify-center w-[38px] h-[38px] rounded-[var(--radius-btn)] bg-slate-900/80 text-text-secondary hover:text-text-primary hover:bg-slate-800 border border-border-default/60 transition-all duration-150 shadow-sm"
            title={theme === 'dark' ? 'Modo Claro' : 'Modo Oscuro'}
          >
            {theme === 'dark' ? <Sun className="w-4 h-4 text-amber-500" /> : <Moon className="w-4 h-4 text-cyan-500" />}
          </button>

          <button
            onClick={() => setShowModalClienteCC(true)}
            className="touch-btn flex items-center gap-1.5 px-4 py-2 rounded-[var(--radius-btn)] bg-slate-900/80 text-cyan-500 hover:text-cyan-400 hover:bg-cyan-500/5 border border-cyan-500/20 transition-all duration-200 text-xs font-bold min-h-[38px] shadow-sm"
          >
            <Wallet className="w-4 h-4" />
            Cuenta Corriente
            {cuentasAbiertas.length > 0 && (
              <span className="ml-0.5 px-1.5 py-0.5 rounded-full text-[9px] font-black bg-cyan-500 text-slate-950">{cuentasAbiertas.length}</span>
            )}
          </button>

          {esEncargado && (
            <button
              onClick={() => navigate('/pos-admin')}
              className="touch-btn flex items-center gap-1.5 px-4 py-2 rounded-[var(--radius-btn)] bg-slate-900/80 text-amber-500 hover:text-amber-400 hover:bg-amber-500/5 border border-amber-500/20 transition-all duration-200 text-xs font-bold min-h-[38px] shadow-sm"
            >
              <KeyRound className="w-4 h-4" />
              Panel Admin
            </button>
          )}
          {usuario && (
            <div className="flex items-center gap-2 px-3 py-1.5 rounded-[var(--radius-btn)] bg-slate-900/40 border border-border-default/50 shadow-sm">
              <User className="w-4 h-4 text-text-muted" />
              <span className="text-xs font-semibold text-text-secondary">{usuario.nombre}</span>
              <span className="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider bg-info-500/5 text-info-400 border border-info-500/10">
                {usuario.rol}
              </span>
            </div>
          )}
          <button onClick={handleLogout}
            className="touch-btn flex items-center gap-1.5 px-4 py-2 rounded-[var(--radius-btn)] bg-slate-900/80 text-text-muted hover:text-danger-400 hover:bg-danger-500/5 border border-border-default transition-all duration-200 text-xs font-bold min-h-[38px] shadow-sm">
            <LogOut className="w-4 h-4" />
            Salir
          </button>
        </div>
      </header>

      {/* Main Workspace */}
      <main className="flex-1 overflow-auto p-6 flex flex-col items-center justify-center bg-slate-900/25 relative">
        <div className="mb-4 text-center">
          <h2 className="text-sm font-bold text-text-secondary uppercase tracking-widest flex items-center justify-center gap-1.5">
            <Maximize2 size={13} className="text-amber-500" />
            Seleccione una Mesa
          </h2>
          <p className="text-xs text-text-muted mt-0.5">Haga click en una mesa para gestionar la comanda activa o iniciar un pedido.</p>
        </div>

        {/* Cuentas Corrientes abiertas */}
        {cuentasAbiertas.length > 0 && (
          <div className="w-full max-w-3xl mb-4 flex flex-wrap items-center justify-center gap-2">
            {cuentasAbiertas.map((c: any) => (
              <button
                key={c.id}
                onClick={() => navigate(`/pos?comandaId=${c.id}&clienteId=${c.clienteId}&clienteNombre=${encodeURIComponent(c.cliente ? `${c.cliente.nombre} ${c.cliente.apellido}` : 'Cliente')}`)}
                className="touch-btn flex items-center gap-1.5 px-3 py-1.5 rounded-[var(--radius-btn)] bg-cyan-500/5 border border-cyan-500/25 text-cyan-400 hover:bg-cyan-500/10 hover:border-cyan-500/40 transition-all text-xs font-bold"
              >
                <Wallet className="w-3.5 h-3.5" />
                {c.cliente ? `${c.cliente.nombre} ${c.cliente.apellido}` : 'Cuenta abierta'}
                <span className="text-cyan-500/70 font-semibold">{formatARS(c.total)}</span>
              </button>
            ))}
          </div>
        )}

        {/* Canvas del Salón */}
        {activeMap && (
          <div 
            className="w-full bg-slate-900/50 border border-border-default/60 rounded-xl shadow-card relative overflow-hidden transition-all duration-300"
            style={{
              ...getCanvasStyles(),
              aspectRatio: `${activeMap.targetWidth || 1024} / ${activeMap.targetHeight || 768}`,
              maxWidth: `min(85vw, calc(72vh * (${activeMap.targetWidth || 1024} / ${activeMap.targetHeight || 768})))`
            }}
          >
            {/* Rejilla decorativa */}
            {activeMap.bgPattern !== 'none' && (
              <div 
                className="absolute inset-0 pointer-events-none opacity-5"
                style={{
                  backgroundImage: activeMap.bgPattern === 'grid' 
                    ? 'linear-gradient(#6b7280 1px, transparent 1px), linear-gradient(90deg, #6b7280 1px, transparent 1px)'
                    : 'radial-gradient(#6b7280 1.5px, transparent 1.5px)',
                  backgroundSize: activeMap.bgPattern === 'grid' ? '30px 30px' : '20px 20px'
                }}
              />
            )}

            {/* Elementos */}
            {activeMap.tables.map(table => {
              if (table.isDecoration) {
                return (
                  <div
                    key={table.id}
                    className="absolute flex items-center justify-center select-none shadow-sm"
                    style={{
                      left: `${table.x}%`,
                      top: `${table.y}%`,
                      width: `${table.width}%`,
                      height: `${table.height}%`,
                      transform: `rotate(${table.rotation || 0}deg)`,
                      backgroundColor: table.color || '#6b7280',
                      borderRadius: '4px',
                      border: '1.5px solid rgba(0,0,0,0.15)',
                    }}
                  >
                    <div className="flex flex-col items-center justify-center w-full h-full p-1 text-center select-none opacity-60">
                      {renderDecorationIcon(table.decorType)}
                      {table.width >= 8 && table.height >= 5 && (
                        <span className="text-[8px] font-bold uppercase mt-0.5 tracking-wider truncate max-w-full text-slate-400">
                          {table.label}
                        </span>
                      )}
                    </div>
                  </div>
                );
              }

              const dbMesa = dbMesas.find(
                (m: any) => m.etiqueta.toLowerCase() === table.label.toLowerCase()
              );
              const isOccupied = dbMesa ? dbMesa.ocupada : openMesaIds.has(table.id);
              return (
                <div
                  key={table.id}
                  onClick={() => handleTableClick(table)}
                  className={`absolute flex items-center justify-center select-none shadow-sm hover:scale-[1.03] transition-all duration-150 cursor-pointer active:scale-95 group border ${
                    isOccupied 
                      ? 'border-rose-600 bg-rose-950/70 text-rose-300' 
                      : 'hover:border-amber-500 text-text-primary'
                  }`}
                  style={{
                    left: `${table.x}%`,
                    top: `${table.y}%`,
                    width: `${table.width}%`,
                    height: `${table.height}%`,
                    transform: `rotate(${table.rotation || 0}deg)`,
                    backgroundColor: isOccupied ? '#7f1d1d' : (table.color || '#3b82f6'),
                    color: isOccupied ? '#ffffff' : getContrastColor(table.color || '#3b82f6'),
                    borderColor: isOccupied ? '#ef4444' : 'rgba(0,0,0,0.15)',
                    borderRadius: table.type === 'circle' ? '50%' : '6px',
                    overflow: 'visible',
                  }}
                  title={`Mesa: ${table.label} (${table.seats} sillas) ${isOccupied ? '- OCUPADA' : '- LIBRE'}`}
                >
                  <div className="flex flex-col items-center justify-center text-center">
                    <span className="text-[10px] md:text-[11px] font-bold tracking-tight truncate max-w-full px-1">
                      {table.label}
                    </span>
                    {isOccupied && (
                      <span className="text-[7px] md:text-[8px] font-semibold text-rose-300 uppercase tracking-widest mt-0.5 scale-90">
                        Ocupada
                      </span>
                    )}
                  </div>

                  {/* Renderizar sillas perimetrales */}
                  {renderChairs(table.seats, table.type as 'square' | 'circle' | 'rectangle')}

                  {/* Brillo al pasar el cursor */}
                  <div className="absolute inset-0 rounded-inherit opacity-0 group-hover:opacity-[0.03] bg-white transition-opacity duration-150" />
                </div>
              );
            })}
          </div>
        )}
      </main>
      
      {/* Footer Info bar */}
      <footer className="px-5 py-2.5 bg-surface-base border-t border-border-default text-center text-[10px] text-text-muted flex-shrink-0 flex items-center justify-between">
        <span>Bares Familia © 2026</span>
        <span>Terminal Local — Módulo de Ventas</span>
      </footer>

      {showModalClienteCC && (
        <ModalCliente
          onSeleccionar={handleClienteSeleccionado}
          onCancelar={() => setShowModalClienteCC(false)}
        />
      )}
    </div>
  );
}
