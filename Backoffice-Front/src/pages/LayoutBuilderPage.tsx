import React, { useState, useEffect, useRef } from 'react';
import { 
  LayoutGrid, 
  Square, 
  Circle, 
  Save, 
  Trash2, 
  Undo2, 
  Redo2, 
  Plus, 
  Edit2, 
  Copy, 
  Sliders, 
  Trash, 
  Check, 
  RotateCw, 
  Grid, 
  Image as ImageIcon, 
  Settings, 
  MousePointer, 
  DoorOpen, 
  Leaf, 
  Wine, 
  Music,
  KeyRound
} from 'lucide-react';
import api from '@/services/api';
import { useAuth } from '@/contexts/AuthContext';
import { useSucursal } from '@/contexts/SucursalContext';

// Interfaces para tipado estricto
interface TableItem {
  id: string;
  type: 'square' | 'circle' | 'rectangle' | 'decoration';
  isDecoration?: boolean; // Si es true, es un objeto estorboso/obstáculo (no facturable)
  decorType?: 'bar' | 'plant' | 'restroom' | 'door' | 'stage' | 'wall'; // Subtipo decorativo
  x: number;       // Posición horizontal en porcentaje (0-100)
  y: number;       // Posición vertical en porcentaje (0-100)
  width: number;   // Ancho en porcentaje del lienzo
  height: number;  // Alto en porcentaje del lienzo
  rotation: number;// Rotación en grados (0-360)
  label: string;   // Etiqueta visual del elemento
  color: string;   // Color de fondo en formato HEX
  seats: number;   // Cantidad de asientos (1-12) (0 para decoraciones)
}

interface TableMap {
  id: string;
  name: string;
  bgType: 'color' | 'texture' | 'image';
  bgColor: string;      // Color de fondo HEX
  bgPattern: 'none' | 'grid' | 'dots'; // Superposición de rejilla
  bgTexture: string;    // ID de textura predefinida ('wood' | 'slate' | 'marble' | 'grass')
  bgImage: string;      // Base64 o URL remota de la imagen
  bgImageFit: 'cover' | 'contain' | 'repeat';
  tables: TableItem[];
  targetWidth: number;  // Ancho de la pantalla de destino en px
  targetHeight: number; // Alto de la pantalla de destino en px
}

// Colores recomendados para las mesas (Paleta armónica)
const MESA_COLOR_PALETTE = [
  '#3b82f6', // Brand Blue
  '#10b981', // Success Green
  '#f59e0b', // Warning Amber
  '#ef4444', // Danger Red
  '#8b5cf6', // Violet
  '#ec4899', // Pink
  '#06b6d4', // Cyan
  '#d97706', // Wood Orange
  '#ffffff', // Clean White
  '#1f2937', // Dark Steel
  '#6b7280', // Medium Gray (Obstáculos)
  '#15803d'  // Forest Green (Plantas)
];

// Texturas prediseñadas
const PRESET_TEXTURES = [
  { id: 'wood', label: 'Madera Cálida', color: '#b45309' },
  { id: 'slate', label: 'Concreto Oscuro', color: '#1e293b' },
  { id: 'marble', label: 'Mármol Suave', color: '#f8fafc' },
  { id: 'grass', label: 'Terraza / Césped', color: '#15803d' }
];

// Mapas iniciales por defecto
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

export default function LayoutBuilderPage() {
  const { user } = useAuth();
  const { isGlobal } = useSucursal();
  // --- Estados de Datos ---
  const [maps, setMaps] = useState<TableMap[]>([]);
  const [activeMapId, setActiveMapId] = useState<string>('');
  const [sucursales, setSucursales] = useState<{ id: string; nombre: string }[]>([]);
  const [selectedBranchId, setSelectedBranchId] = useState<string>('');

  const [selectedTableId, setSelectedTableId] = useState<string | null>(null);

  // --- Estados de Navegación y Renombrado ---
  const [editingMapId, setEditingMapId] = useState<string | null>(null);
  const [renameMapName, setRenameMapName] = useState('');
  const [isAddingMap, setIsAddingMap] = useState(false);
  const [newMapName, setNewMapName] = useState('');

  // --- Estados de Configuración Global del Lienzo ---
  const [snapToGrid, setSnapToGrid] = useState<boolean>(true);
  const [gridStep, setGridStep] = useState<number>(2.5); // paso de la rejilla en %
  const [elementTypeTab, setElementTypeTab] = useState<'mesas' | 'decoracion'>('mesas');
  const [, setIsDragging] = useState(false);

  // --- Configuración de Opciones del Panel Admin en POS ---
  const [adminButtons, setAdminButtons] = useState([
    { id: 'cierre-caja', label: 'Cierre de Caja', requiredPermission: 'pos.cierre_caja', enabled: true },
    { id: 'egresos', label: 'Registrar Egreso', requiredPermission: 'pos.egresos', enabled: true },
    { id: 'cuentas-corrientes', label: 'Cuentas Corrientes', requiredPermission: 'pos.cuentas_corrientes', enabled: true },
    { id: 'historial', label: 'Historial de Ventas', requiredPermission: 'pos.vender', enabled: true },
    { id: 'reimprimir', label: 'Reimprimir Último Ticket', requiredPermission: 'pos.vender', enabled: true },
    { id: 'sincronizar', label: 'Sincronización Manual', requiredPermission: 'gerente.override', enabled: true }
  ]);

  // --- Historial ---
  const [history, setHistory] = useState<TableMap[][]>([[]]);
  const [historyIndex, setHistoryIndex] = useState(0);

  // --- Sistema de Toast ---
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);

  // --- Referencias ---
  const canvasRef = useRef<HTMLDivElement>(null);
  const dragStateRef = useRef<{
    tableId: string;
    startX: number;
    startY: number;
    startTableX: number;
    startTableY: number;
    canvasWidth: number;
    canvasHeight: number;
  } | null>(null);

  const activeMap = maps.find(m => m.id === activeMapId) || maps[0];

  // --- Cargar Sucursales ---
  // Un usuario acotado a una sucursal solo puede editar el plano de la propia;
  // uno global puede elegir entre todas.
  useEffect(() => {
    if (!isGlobal) {
      if (user) {
        setSucursales([{ id: user.sucursalId, nombre: user.sucursal }]);
        setSelectedBranchId(user.sucursalId);
      }
      return;
    }

    api.get('/sucursal')
      .then(res => {
        setSucursales(res.data);
        if (res.data.length > 0) {
          setSelectedBranchId(res.data[0].id);
        }
      })
      .catch(err => {
        console.error('Error cargando sucursales:', err);
        showToast('Error al conectar con la Nube', 'error');
      });
  }, [isGlobal, user]);

  // --- Cargar Planos de la Sucursal Seleccionada ---
  useEffect(() => {
    if (!selectedBranchId) return;

    api.get(`/ConfiguracionPos/sucursal/${selectedBranchId}`)
      .then(res => {
        const data = res.data;
        if (data && data.length > 0) {
          let adminButtonsLoaded = false;
          const loadedMaps = data.map((cfg: any) => {
            let parsedJson: any = {};
            try {
              parsedJson = JSON.parse(cfg.configuracionJson);
            } catch (e) {}

            if (!adminButtonsLoaded && parsedJson.adminPanelButtons && parsedJson.adminPanelButtons.length > 0) {
              setAdminButtons(parsedJson.adminPanelButtons);
              adminButtonsLoaded = true;
            }

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
          setHistory([JSON.parse(JSON.stringify(loadedMaps))]);
          setHistoryIndex(0);
        } else {
          // Si no hay planos creados, arrancar con DEFAULT_MAPS pero marcar IDs para guardarlos como nuevos
          const newDefaultMaps = DEFAULT_MAPS.map((m, idx) => ({
            ...m,
            id: `new-${Date.now()}-${idx}`
          }));
          setMaps(newDefaultMaps);
          setActiveMapId(newDefaultMaps[0].id);
          setHistory([JSON.parse(JSON.stringify(newDefaultMaps))]);
          setHistoryIndex(0);
        }
      })
      .catch(err => {
        console.error('Error cargando planos:', err);
        showToast('Error al cargar planos de salones', 'error');
      });
  }, [selectedBranchId]);

  // --- Notificaciones Toast ---
  const showToast = (message: string, type: 'success' | 'error' | 'info' = 'success') => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 3500);
  };

  // --- Guardar en el Historial ---
  const saveToHistory = (newMaps: TableMap[]) => {
    const updatedHistory = history.slice(0, historyIndex + 1);
    if (updatedHistory.length >= 50) {
      updatedHistory.shift();
    }
    const snapshot = JSON.parse(JSON.stringify(newMaps));
    setHistory([...updatedHistory, snapshot]);
    setHistoryIndex(prev => Math.min(49, prev + 1));
  };

  const handleUndo = () => {
    if (historyIndex > 0) {
      const prevIndex = historyIndex - 1;
      setHistoryIndex(prevIndex);
      const prevMaps = JSON.parse(JSON.stringify(history[prevIndex]));
      setMaps(prevMaps);
      // Reajustar mapa activo si se deshizo su creación
      if (prevMaps.length > 0 && !prevMaps.some((m: TableMap) => m.id === activeMapId)) {
        setActiveMapId(prevMaps[0].id);
      }
      setSelectedTableId(null);
      showToast('Acción deshecha', 'info');
    }
  };

  const handleRedo = () => {
    if (historyIndex < history.length - 1) {
      const nextIndex = historyIndex + 1;
      setHistoryIndex(nextIndex);
      const nextMaps = JSON.parse(JSON.stringify(history[nextIndex]));
      setMaps(nextMaps);
      if (nextMaps.length > 0 && !nextMaps.some((m: TableMap) => m.id === activeMapId)) {
        setActiveMapId(nextMaps[0].id);
      }
      setSelectedTableId(null);
      showToast('Acción rehecha', 'info');
    }
  };

  // --- Guardar Cambios en la Nube ---
  const handleSave = async () => {
    if (!selectedBranchId) {
      showToast('Seleccione una sucursal primero', 'error');
      return;
    }

    try {
      showToast('Guardando planos...', 'info');
      const updatedMaps = [...maps];
      
      for (let i = 0; i < updatedMaps.length; i++) {
        const map = updatedMaps[i];
        
        // Convertir IDs de mesas locales temporales a UUIDs válidos para que coincidan con la base de datos
        const parsedTables = map.tables.map(t => {
          if (!t.isDecoration && (t.id.startsWith('table-') || t.id.startsWith('item-'))) {
            // Generar UUID si es mesa facturable nueva
            return { ...t, id: crypto.randomUUID() };
          }
          return t;
        });

        const payload = {
          sucursalId: selectedBranchId,
          nombre: map.name,
          configuracionJson: JSON.stringify({
            bgType: map.bgType,
            bgColor: map.bgColor,
            bgPattern: map.bgPattern,
            bgTexture: map.bgTexture,
            bgImage: map.bgImage,
            bgImageFit: map.bgImageFit,
            targetWidth: map.targetWidth,
            targetHeight: map.targetHeight,
            tables: parsedTables,
            adminPanelButtons: adminButtons
          })
        };

        const isNew = map.id.startsWith('map-') || map.id.startsWith('new-');
        if (isNew) {
          const res = await api.post('/ConfiguracionPos', payload);
          updatedMaps[i] = {
            ...map,
            id: res.data.id,
            tables: parsedTables
          };
        } else {
          await api.put(`/ConfiguracionPos/${map.id}`, payload);
          updatedMaps[i] = {
            ...map,
            tables: parsedTables
          };
        }
      }
      
      setMaps(updatedMaps);
      showToast('¡Plano de mesas guardado con éxito!', 'success');
    } catch (err) {
      console.error('Error al guardar:', err);
      showToast('Error al guardar en el servidor', 'error');
    }
  };

  // --- Operaciones de Mapas ---
  const handleCreateMap = () => {
    if (!newMapName.trim()) return;
    
    const newMap: TableMap = {
      id: `map-${Date.now()}`,
      name: newMapName.trim(),
      bgType: 'color',
      bgColor: '#ffffff',
      bgPattern: 'grid',
      bgTexture: '',
      bgImage: '',
      bgImageFit: 'cover',
      tables: [],
      targetWidth: 1024,
      targetHeight: 768
    };
    
    const updatedMaps = [...maps, newMap];
    setMaps(updatedMaps);
    setActiveMapId(newMap.id);
    setIsAddingMap(false);
    setNewMapName('');
    saveToHistory(updatedMaps);
    showToast(`Salón "${newMap.name}" creado`);
  };

  const startRenameMap = (map: TableMap) => {
    setEditingMapId(map.id);
    setRenameMapName(map.name);
  };

  const finishRenameMap = () => {
    if (renameMapName.trim() && editingMapId) {
      const updatedMaps = maps.map(m => m.id === editingMapId ? { ...m, name: renameMapName.trim() } : m);
      setMaps(updatedMaps);
      saveToHistory(updatedMaps);
      showToast('Salón renombrado');
    }
    setEditingMapId(null);
  };

  const deleteMap = async (mapId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (maps.length <= 1) {
      showToast('Debe haber al menos un salón.', 'error');
      return;
    }
    
    if (confirm('¿Estás seguro de que deseas eliminar este salón y todas sus mesas?')) {
      const isNew = mapId.startsWith('map-') || mapId.startsWith('new-');
      if (!isNew) {
        try {
          await api.delete(`/ConfiguracionPos/${mapId}`);
        } catch (err) {
          console.error(err);
          showToast('Error al eliminar en el servidor', 'error');
          return;
        }
      }

      const updatedMaps = maps.filter(m => m.id !== mapId);
      setMaps(updatedMaps);
      if (activeMapId === mapId) {
        setActiveMapId(updatedMaps[0].id);
      }
      setSelectedTableId(null);
      saveToHistory(updatedMaps);
      showToast('Salón eliminado');
    }
  };

  const duplicateMap = (map: TableMap) => {
    const newMap: TableMap = {
      ...JSON.parse(JSON.stringify(map)),
      id: `map-${Date.now()}`,
      name: `${map.name} (Copia)`,
    };
    
    const updatedMaps = [...maps, newMap];
    setMaps(updatedMaps);
    setActiveMapId(newMap.id);
    setSelectedTableId(null);
    saveToHistory(updatedMaps);
    showToast('Salón duplicado');
  };

  // --- Operaciones de Mesas ---
  const addTable = (type: 'square' | 'circle' | 'rectangle') => {
    if (!activeMap) return;
    
    // Dimensiones iniciales recomendables
    let width = 10;
    let height = 10;
    let seats = 4;
    
    if (type === 'rectangle') {
      width = 15;
      height = 10;
      seats = 6;
    }
    
    const newTable: TableItem = {
      id: `table-${Date.now()}-${Math.random().toString(36).substr(2, 5)}`,
      type,
      x: 40,
      y: 40,
      width,
      height,
      rotation: 0,
      label: `Mesa ${activeMap.tables.length + 1}`,
      color: '#3b82f6', // Brand color por defecto
      seats
    };
    
    const updatedMaps = maps.map(m => {
      if (m.id === activeMapId) {
        return {
          ...m,
          tables: [...m.tables, newTable]
        };
      }
      return m;
    });
    
    setMaps(updatedMaps);
    setSelectedTableId(newTable.id);
    saveToHistory(updatedMaps);
    showToast('Mesa añadida');
  };

  // --- Operaciones de Objetos Decorativos ---
  const addDecoration = (decorType: 'bar' | 'plant' | 'restroom' | 'door' | 'stage' | 'wall') => {
    if (!activeMap) return;
    
    let width = 12;
    let height = 10;
    let label = 'Objeto';
    let color = '#6b7280'; // gray default
    
    switch (decorType) {
      case 'bar':
        width = 24;
        height = 8;
        label = 'Barra';
        color = '#d97706'; // wood color
        break;
      case 'plant':
        width = 6;
        height = 6;
        label = 'Planta';
        color = '#15803d'; // green
        break;
      case 'restroom':
        width = 12;
        height = 12;
        label = 'Baños';
        color = '#4b5563'; // dark gray
        break;
      case 'door':
        width = 10;
        height = 6;
        label = 'Acceso';
        color = '#78350f'; // brown
        break;
      case 'stage':
        width = 25;
        height = 14;
        label = 'Escenario';
        color = '#6366f1'; // indigo
        break;
      case 'wall':
        width = 20;
        height = 2;
        label = 'Muro';
        color = '#9ca3af'; // light gray
        break;
    }
    
    const newDecor: TableItem = {
      id: `decor-${Date.now()}-${Math.random().toString(36).substr(2, 5)}`,
      type: 'decoration',
      isDecoration: true,
      decorType,
      x: 35,
      y: 35,
      width,
      height,
      rotation: 0,
      label,
      color,
      seats: 0
    };
    
    const updatedMaps = maps.map(m => {
      if (m.id === activeMapId) {
        return {
          ...m,
          tables: [...m.tables, newDecor]
        };
      }
      return m;
    });
    
    setMaps(updatedMaps);
    setSelectedTableId(newDecor.id);
    saveToHistory(updatedMaps);
    showToast('Objeto no facturable añadido');
  };

  const duplicateTable = (tableId: string) => {
    if (!activeMap) return;
    const table = activeMap.tables.find(t => t.id === tableId);
    if (!table) return;
    
    const newTable: TableItem = {
      ...JSON.parse(JSON.stringify(table)),
      id: `item-${Date.now()}-${Math.random().toString(36).substr(2, 5)}`,
      x: Math.min(85, table.x + 4), // Leve desplazamiento para notar la duplicación
      y: Math.min(85, table.y + 4),
      label: `${table.label} Copia`
    };
    
    const updatedMaps = maps.map(m => {
      if (m.id === activeMapId) {
        return {
          ...m,
          tables: [...m.tables, newTable]
        };
      }
      return m;
    });
    
    setMaps(updatedMaps);
    setSelectedTableId(newTable.id);
    saveToHistory(updatedMaps);
    showToast('Duplicado con éxito');
  };

  const deleteTable = (tableId: string) => {
    const updatedMaps = maps.map(m => {
      if (m.id === activeMapId) {
        return {
          ...m,
          tables: m.tables.filter(t => t.id !== tableId)
        };
      }
      return m;
    });
    
    setMaps(updatedMaps);
    setSelectedTableId(null);
    saveToHistory(updatedMaps);
    showToast('Elemento eliminado');
  };

  const updateTableProperty = (property: keyof TableItem, value: any) => {
    if (!selectedTableId) return;
    
    const updatedMaps = maps.map(m => {
      if (m.id === activeMapId) {
        return {
          ...m,
          tables: m.tables.map(t => {
            if (t.id === selectedTableId) {
              const updatedTable = { ...t, [property]: value };
              // Mantener proporción en cuadrados y círculos si cambian ancho/alto si se desea
              if ((t.type === 'square' || t.type === 'circle' || (t.type === 'decoration' && t.decorType === 'plant')) && !t.isDecoration) {
                if (property === 'width') updatedTable.height = value;
                if (property === 'height') updatedTable.width = value;
              }
              return updatedTable;
            }
            return t;
          })
        };
      }
      return m;
    });
    
    setMaps(updatedMaps);
  };

  // Guardado de historial al soltar sliders o blurear inputs
  const handlePropertyChangeComplete = () => {
    saveToHistory(maps);
  };

  // --- Operaciones de Fondo de Salón ---
  const updateBackgroundProperty = (property: keyof TableMap, value: any) => {
    const updatedMaps = maps.map(m => {
      if (m.id === activeMapId) {
        return {
          ...m,
          [property]: value
        };
      }
      return m;
    });
    setMaps(updatedMaps);
    saveToHistory(updatedMaps);
  };

  // Carga de imagen local en Base64
  const handleBackgroundImageUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    
    if (file.size > 2 * 1024 * 1024) {
      showToast('La imagen es demasiado grande. Máx 2MB para almacenamiento local.', 'error');
      return;
    }
    
    const reader = new FileReader();
    reader.onload = (event) => {
      const base64 = event.target?.result as string;
      if (base64) {
        updateBackgroundProperty('bgImage', base64);
        updateBackgroundProperty('bgType', 'image');
        showToast('Imagen de fondo cargada');
      }
    };
    reader.readAsDataURL(file);
  };

  // --- Lógica del Arrastre con PointerEvents (Suave e Independiente de Cuadrícula) ---
  const handlePointerDown = (e: React.PointerEvent, tableId: string) => {
    // Si se hace clic en el botón de borrar, no arrastrar
    if ((e.target as HTMLElement).closest('.delete-btn-inner')) return;

    e.preventDefault();
    e.stopPropagation();
    
    setSelectedTableId(tableId);
    
    const table = activeMap.tables.find(t => t.id === tableId);
    if (!table) return;
    
    const canvasElement = canvasRef.current;
    if (!canvasElement) return;
    
    const rect = canvasElement.getBoundingClientRect();
    
    dragStateRef.current = {
      tableId,
      startX: e.clientX,
      startY: e.clientY,
      startTableX: table.x,
      startTableY: table.y,
      canvasWidth: rect.width,
      canvasHeight: rect.height,
    };
    
    setIsDragging(true);
    
    document.addEventListener('pointermove', handlePointerMove);
    document.addEventListener('pointerup', handlePointerUp);
  };

  const handlePointerMove = (e: PointerEvent) => {
    if (!dragStateRef.current) return;
    const ds = dragStateRef.current;
    
    const deltaX = e.clientX - ds.startX;
    const deltaY = e.clientY - ds.startY;
    
    // Conversión de delta pixeles a porcentaje relativo
    const deltaXPercent = (deltaX / ds.canvasWidth) * 100;
    const deltaYPercent = (deltaY / ds.canvasHeight) * 100;
    
    let newX = ds.startTableX + deltaXPercent;
    let newY = ds.startTableY + deltaYPercent;
    
    const table = activeMap.tables.find(t => t.id === ds.tableId);
    if (!table) return;
    
    // Aplicar ajuste magnético (Snap to Grid) si está activo
    if (snapToGrid && gridStep > 0) {
      newX = Math.round(newX / gridStep) * gridStep;
      newY = Math.round(newY / gridStep) * gridStep;
    }
    
    // Limitar dentro del lienzo (0% a 100% menos el tamaño del objeto)
    newX = Math.max(0, Math.min(100 - table.width, newX));
    newY = Math.max(0, Math.min(100 - table.height, newY));
    
    setMaps(prevMaps => prevMaps.map(m => {
      if (m.id === activeMapId) {
        return {
          ...m,
          tables: m.tables.map(t => t.id === ds.tableId ? { ...t, x: newX, y: newY } : t)
        };
      }
      return m;
    }));
  };

  const handlePointerUp = () => {
    document.removeEventListener('pointermove', handlePointerMove);
    document.removeEventListener('pointerup', handlePointerUp);
    
    if (dragStateRef.current) {
      saveToHistory(maps);
    }
    dragStateRef.current = null;
    setIsDragging(false);
  };

  // --- Lógica del HTML5 Drag & Drop (para soltar desde el Panel de Herramientas) ---
  const handleToolDragStart = (
    e: React.DragEvent, 
    type: 'square' | 'circle' | 'rectangle' | 'decor-bar' | 'decor-plant' | 'decor-restroom' | 'decor-door' | 'decor-stage' | 'decor-wall'
  ) => {
    e.dataTransfer.setData('text/plain', type);
  };

  const handleCanvasDrop = (e: React.DragEvent) => {
    e.preventDefault();
    const rawType = e.dataTransfer.getData('text/plain');
    if (!rawType || !canvasRef.current) return;
    
    const rect = canvasRef.current.getBoundingClientRect();
    const dropX = e.clientX - rect.left;
    const dropY = e.clientY - rect.top;
    
    // Convertir pixeles a porcentaje
    let pctX = (dropX / rect.width) * 100;
    let pctY = (dropY / rect.height) * 100;
    
    let w = 10;
    let h = 10;
    let seats = 4;
    let label = 'Mesa';
    let color = '#3b82f6';
    let isDecoration = false;
    let decorType: 'bar' | 'plant' | 'restroom' | 'door' | 'stage' | 'wall' | undefined;
    let type: 'square' | 'circle' | 'rectangle' | 'decoration';
    
    if (rawType.startsWith('decor-')) {
      type = 'decoration';
      isDecoration = true;
      decorType = rawType.replace('decor-', '') as any;
      seats = 0;
      
      switch (decorType) {
        case 'bar': w = 24; h = 8; label = 'Barra'; color = '#d97706'; break;
        case 'plant': w = 6; h = 6; label = 'Planta'; color = '#15803d'; break;
        case 'restroom': w = 12; h = 12; label = 'Baños'; color = '#4b5563'; break;
        case 'door': w = 10; h = 6; label = 'Acceso'; color = '#78350f'; break;
        case 'stage': w = 25; h = 14; label = 'Escenario'; color = '#6366f1'; break;
        case 'wall': w = 20; h = 2; label = 'Muro'; color = '#9ca3af'; break;
      }
    } else {
      type = rawType as any;
      if (type === 'rectangle') {
        w = 15; h = 10; seats = 6;
      }
      label = `Mesa ${activeMap.tables.length + 1}`;
    }
    
    // Centrar el elemento respecto al cursor
    pctX = pctX - w / 2;
    pctY = pctY - h / 2;
    
    // Validar límites y aplicar snap
    if (snapToGrid) {
      pctX = Math.round(pctX / gridStep) * gridStep;
      pctY = Math.round(pctY / gridStep) * gridStep;
    }
    pctX = Math.max(0, Math.min(100 - w, pctX));
    pctY = Math.max(0, Math.min(100 - h, pctY));
    
    const newItem: TableItem = {
      id: `item-${Date.now()}-${Math.random().toString(36).substr(2, 5)}`,
      type,
      isDecoration,
      decorType,
      x: pctX,
      y: pctY,
      width: w,
      height: h,
      rotation: 0,
      label,
      color,
      seats
    };
    
    const updatedMaps = maps.map(m => {
      if (m.id === activeMapId) {
        return {
          ...m,
          tables: [...m.tables, newItem]
        };
      }
      return m;
    });
    
    setMaps(updatedMaps);
    setSelectedTableId(newItem.id);
    saveToHistory(updatedMaps);
    showToast('Elemento colocado');
  };

  // --- Eventos de teclado (Flechas de Movimiento y Suprimir) ---
  useEffect(() => {
    let movementTriggered = false;
    
    const handleKeyDown = (e: KeyboardEvent) => {
      if (!selectedTableId || !activeMap) return;
      
      // Evitar mover mesas si se escribe en inputs
      const activeEl = document.activeElement;
      if (activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA')) {
        return;
      }
      
      let dx = 0;
      let dy = 0;
      const step = e.shiftKey ? 3 : 0.8; // Movimiento porcentual fino / rápido
      let isArrow = false;
      
      if (e.key === 'ArrowUp') { dy = -step; isArrow = true; }
      else if (e.key === 'ArrowDown') { dy = step; isArrow = true; }
      else if (e.key === 'ArrowLeft') { dx = -step; isArrow = true; }
      else if (e.key === 'ArrowRight') { dx = step; isArrow = true; }
      else if (e.key === 'Delete' || e.key === 'Backspace') {
        deleteTable(selectedTableId);
        e.preventDefault();
        return;
      } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'd') {
        duplicateTable(selectedTableId);
        e.preventDefault();
        return;
      }
      
      if (isArrow) {
        movementTriggered = true;
        e.preventDefault();
        
        setMaps(prevMaps => prevMaps.map(m => {
          if (m.id === activeMapId) {
            return {
              ...m,
              tables: m.tables.map(t => {
                if (t.id === selectedTableId) {
                  let newX = Math.max(0, Math.min(100 - t.width, t.x + dx));
                  let newY = Math.max(0, Math.min(100 - t.height, t.y + dy));
                  
                  // Opcionalmente alinear a rejilla en pasos de flechas
                  if (snapToGrid && !e.shiftKey) {
                    newX = Math.round(newX / gridStep) * gridStep;
                    newY = Math.round(newY / gridStep) * gridStep;
                  }
                  
                  return { ...t, x: newX, y: newY };
                }
                return t;
              })
            };
          }
          return m;
        }));
      }
    };
    
    const handleKeyUp = () => {
      if (movementTriggered) {
        saveToHistory(maps);
        movementTriggered = false;
      }
    };
    
    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, [selectedTableId, activeMapId, maps, snapToGrid, gridStep]);

  // --- Determinación del contraste del texto sobre el color de mesa ---
  const getContrastColor = (hexColor: string) => {
    if (!hexColor || !hexColor.startsWith('#')) return '#1f2937';
    const r = parseInt(hexColor.substr(1, 2), 16);
    const g = parseInt(hexColor.substr(3, 2), 16);
    const b = parseInt(hexColor.substr(5, 2), 16);
    const yiq = (r * 299 + g * 587 + b * 114) / 1000;
    return yiq >= 128 ? '#1f2937' : '#ffffff';
  };

  // --- Algoritmo de distribución visual de sillas ---
  const renderChairs = (seats: number, type: 'square' | 'circle' | 'rectangle') => {
    const chairs = [];
    const chairSize = 'w-3.5 h-3.5 md:w-4 md:h-4';
    
    if (type === 'circle') {
      for (let i = 0; i < seats; i++) {
        // Angulo radial equitativo
        const angle = (i * 2 * Math.PI) / seats - Math.PI / 2;
        // Radio un poco más allá del borde (56%)
        const r = 56;
        const left = 50 + r * Math.cos(angle);
        const top = 50 + r * Math.sin(angle);
        
        chairs.push(
          <div
            key={`chair-${i}`}
            className={`absolute ${chairSize} bg-pearl-500 border-2 border-white rounded-full -translate-x-1/2 -translate-y-1/2 shadow-xs transition-colors`}
            style={{
              left: `${left}%`,
              top: `${top}%`,
            }}
          />
        );
      }
    } else {
      // Distribución en bordes de rectángulos o cuadrados
      const sides: number[][] = [[], [], [], []];
      for (let i = 0; i < seats; i++) {
        sides[i % 4].push(i);
      }
      
      // Lado superior
      sides[0].forEach((id, idx) => {
        const count = sides[0].length;
        const left = count === 1 ? 50 : 20 + (idx * 60) / (count - 1);
        chairs.push(
          <div
            key={`chair-top-${id}`}
            className={`absolute ${chairSize} bg-pearl-500 border-2 border-white rounded-sm -translate-x-1/2 -translate-y-1/2 shadow-xs`}
            style={{ left: `${left}%`, top: '-4px' }}
          />
        );
      });
      
      // Lado derecho
      sides[1].forEach((id, idx) => {
        const count = sides[1].length;
        const top = count === 1 ? 50 : 20 + (idx * 60) / (count - 1);
        chairs.push(
          <div
            key={`chair-right-${id}`}
            className={`absolute ${chairSize} bg-pearl-500 border-2 border-white rounded-sm -translate-x-1/2 -translate-y-1/2 shadow-xs`}
            style={{ left: 'calc(100% + 4px)', top: `${top}%` }}
          />
        );
      });
      
      // Lado inferior
      sides[2].forEach((id, idx) => {
        const count = sides[2].length;
        const left = count === 1 ? 50 : 20 + (idx * 60) / (count - 1);
        chairs.push(
          <div
            key={`chair-bottom-${id}`}
            className={`absolute ${chairSize} bg-pearl-500 border-2 border-white rounded-sm -translate-x-1/2 -translate-y-1/2 shadow-xs`}
            style={{ left: `${left}%`, top: 'calc(100% + 4px)' }}
          />
        );
      });
      
      // Lado izquierdo
      sides[3].forEach((id, idx) => {
        const count = sides[3].length;
        const top = count === 1 ? 50 : 20 + (idx * 60) / (count - 1);
        chairs.push(
          <div
            key={`chair-left-${id}`}
            className={`absolute ${chairSize} bg-pearl-500 border-2 border-white rounded-sm -translate-x-1/2 -translate-y-1/2 shadow-xs`}
            style={{ left: '-4px', top: `${top}%` }}
          />
        );
      });
    }
    
    return chairs;
  };

  // --- Renderizar Iconos específicos de objetos no facturables ---
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
        return <span className="text-[10px] font-extrabold font-mono tracking-tighter">WC</span>;
      case 'stage':
        return <Music size={size} className="text-indigo-900" />;
      case 'wall':
        return <div className="w-full h-full bg-pearl-400 opacity-60 border border-pearl-500" />;
      default:
        return null;
    }
  };

  // --- Estilos CSS dinámicos para el fondo del Lienzo (Canvas) ---
  const getCanvasStyles = (): React.CSSProperties => {
    if (!activeMap) return {};
    
    const styles: React.CSSProperties = {};
    
    if (activeMap.bgType === 'color') {
      styles.backgroundColor = activeMap.bgColor || '#ffffff';
      styles.backgroundImage = 'none';
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
        styles.backgroundColor = '#e2e8f0';
        styles.backgroundImage = 'none';
      }
    }
    
    return styles;
  };

  // Mesa seleccionada actualmente
  const selectedTable = activeMap?.tables.find(t => t.id === selectedTableId) || null;

  return (
    <div className="space-y-6 animate-fade-in pb-8 h-[calc(100vh-80px)] flex flex-col relative select-none">
      
      {/* --- Encabezado --- */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-pearl-900">Diseñador del Salón y POS</h2>
          <p className="text-xs text-pearl-400">Ubica mesas, configura salones, define la pantalla destino y añade obstáculos visuales.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2.5 self-stretch sm:self-auto">
          {/* Selector de Sucursal */}
          <div className="flex items-center gap-2 bg-white border border-pearl-100 rounded-lg px-3 py-1.5 shadow-sm h-[38px]">
            <span className="text-[10px] font-extrabold text-pearl-500 uppercase tracking-wider">Sucursal:</span>
            <select
              value={selectedBranchId}
              onChange={(e) => setSelectedBranchId(e.target.value)}
              className="bg-transparent text-xs font-bold text-pearl-800 outline-none cursor-pointer border-none p-0"
            >
              <option value="">Seleccione...</option>
              {sucursales.map(s => (
                <option key={s.id} value={s.id}>{s.nombre}</option>
              ))}
            </select>
          </div>

          {/* Historial */}
          <div className="flex items-center bg-white border border-pearl-100 rounded-lg p-0.5 shadow-sm h-[38px]">
            <button 
              onClick={handleUndo} 
              disabled={historyIndex <= 0}
              className="p-1.5 text-pearl-500 hover:text-pearl-800 disabled:opacity-30 disabled:hover:text-pearl-500 rounded-md transition-colors"
              title="Deshacer"
            >
              <Undo2 size={16} />
            </button>
            <button 
              onClick={handleRedo} 
              disabled={historyIndex >= history.length - 1}
              className="p-1.5 text-pearl-500 hover:text-pearl-800 disabled:opacity-30 disabled:hover:text-pearl-500 rounded-md transition-colors"
              title="Rehacer"
            >
              <Redo2 size={16} />
            </button>
          </div>
          
          <button 
            onClick={handleSave}
            className="flex-1 sm:flex-initial flex items-center justify-center gap-2 px-4 py-2 bg-brand-600 text-white rounded-lg text-sm font-medium hover:bg-brand-700 transition-colors shadow-sm cursor-pointer"
          >
            <Save size={16} />
            Guardar Cambios
          </button>
        </div>
      </div>

      {/* --- Contenido Principal de 3 Columnas --- */}
      <div className="grid grid-cols-1 xl:grid-cols-12 gap-6 flex-1 min-h-0">
        
        {/* --- COLUMNA IZQUIERDA: Herramientas del Salón (3 Cols) --- */}
        <div className="xl:col-span-3 flex flex-col space-y-4 min-h-0 overflow-y-auto pr-1">
              {/* Sección: Elementos para Añadir */}
              <div className="bg-white rounded-xl border border-pearl-100 p-4 shadow-xs space-y-3">
                <h3 className="text-xs font-bold text-pearl-800 uppercase tracking-widest flex items-center gap-2">
                  <LayoutGrid size={14} className="text-brand-500" />
                  Agregar Elementos
                </h3>
                
                {/* Selector secundario de tipos de elemento */}
                <div className="flex bg-ice-50 border border-pearl-100 rounded-lg p-0.5">
                  <button
                    onClick={() => setElementTypeTab('mesas')}
                    className={`flex-1 py-1 text-[10px] font-bold rounded-md transition-all
                      ${elementTypeTab === 'mesas' ? 'bg-white text-brand-700 shadow-2xs' : 'text-pearl-500 hover:text-pearl-800'}
                    `}
                  >
                    Mesas
                  </button>
                  <button
                    onClick={() => setElementTypeTab('decoracion')}
                    className={`flex-1 py-1 text-[10px] font-bold rounded-md transition-all
                      ${elementTypeTab === 'decoracion' ? 'bg-white text-brand-700 shadow-2xs' : 'text-pearl-500 hover:text-pearl-800'}
                    `}
                  >
                    Objetos / Decoración
                  </button>
                </div>
                
                {elementTypeTab === 'mesas' ? (
                  <div className="grid grid-cols-3 gap-2 animate-fade-in">
                    {/* Cuadrada */}
                    <div 
                      onClick={() => addTable('square')}
                      draggable
                      onDragStart={(e) => handleToolDragStart(e, 'square')}
                      className="flex flex-col items-center justify-center p-2.5 rounded-lg border border-pearl-100 bg-ice-50 hover:bg-white hover:border-brand-300 transition-all cursor-grab active:cursor-grabbing hover:shadow-xs group text-center"
                    >
                      <div className="w-10 h-10 bg-white border border-pearl-200 rounded flex items-center justify-center mb-1 shadow-2xs group-hover:border-brand-200">
                        <Square size={18} className="text-brand-500" />
                      </div>
                      <span className="text-[9px] font-bold text-pearl-600 uppercase tracking-wider">Cuadrada</span>
                    </div>

                    {/* Redonda */}
                    <div 
                      onClick={() => addTable('circle')}
                      draggable
                      onDragStart={(e) => handleToolDragStart(e, 'circle')}
                      className="flex flex-col items-center justify-center p-2.5 rounded-lg border border-pearl-100 bg-ice-50 hover:bg-white hover:border-success-300 transition-all cursor-grab active:cursor-grabbing hover:shadow-xs group text-center"
                    >
                      <div className="w-10 h-10 bg-white border border-pearl-200 rounded-full flex items-center justify-center mb-1 shadow-2xs group-hover:border-success-200">
                        <Circle size={18} className="text-success-500" />
                      </div>
                      <span className="text-[9px] font-bold text-pearl-600 uppercase tracking-wider">Redonda</span>
                    </div>

                    {/* Rectangular */}
                    <div 
                      onClick={() => addTable('rectangle')}
                      draggable
                      onDragStart={(e) => handleToolDragStart(e, 'rectangle')}
                      className="flex flex-col items-center justify-center p-2.5 rounded-lg border border-pearl-100 bg-ice-50 hover:bg-white hover:border-warning-300 transition-all cursor-grab active:cursor-grabbing hover:shadow-xs group text-center"
                    >
                      <div className="w-10 h-10 bg-white border border-pearl-200 rounded flex items-center justify-center mb-1 shadow-2xs group-hover:border-warning-200">
                        <div className="w-6 h-3.5 border border-warning-350 rounded-xs bg-warning-50/50" />
                      </div>
                      <span className="text-[9px] font-bold text-pearl-600 uppercase tracking-wider">Rectang.</span>
                    </div>
                  </div>
                ) : (
                  <div className="grid grid-cols-3 gap-2 animate-fade-in">
                    {/* Barra */}
                    <div 
                      onClick={() => addDecoration('bar')}
                      draggable
                      onDragStart={(e) => handleToolDragStart(e, 'decor-bar')}
                      className="flex flex-col items-center justify-center p-2.5 rounded-lg border border-pearl-100 bg-ice-50 hover:bg-white hover:border-amber-400 transition-all cursor-grab active:cursor-grabbing hover:shadow-xs group text-center"
                    >
                      <div className="w-10 h-10 bg-white border border-pearl-200 rounded flex items-center justify-center mb-1 shadow-2xs group-hover:border-amber-200">
                        <Wine size={16} className="text-amber-600" />
                      </div>
                      <span className="text-[9px] font-bold text-pearl-600 uppercase tracking-wider">Barra</span>
                    </div>

                    {/* Planta */}
                    <div 
                      onClick={() => addDecoration('plant')}
                      draggable
                      onDragStart={(e) => handleToolDragStart(e, 'decor-plant')}
                      className="flex flex-col items-center justify-center p-2.5 rounded-lg border border-pearl-100 bg-ice-50 hover:bg-white hover:border-emerald-400 transition-all cursor-grab active:cursor-grabbing hover:shadow-xs group text-center"
                    >
                      <div className="w-10 h-10 bg-white border border-pearl-200 rounded flex items-center justify-center mb-1 shadow-2xs group-hover:border-emerald-200">
                        <Leaf size={16} className="text-emerald-600" />
                      </div>
                      <span className="text-[9px] font-bold text-pearl-600 uppercase tracking-wider">Planta</span>
                    </div>

                    {/* Baño */}
                    <div 
                      onClick={() => addDecoration('restroom')}
                      draggable
                      onDragStart={(e) => handleToolDragStart(e, 'decor-restroom')}
                      className="flex flex-col items-center justify-center p-2.5 rounded-lg border border-pearl-100 bg-ice-50 hover:bg-white hover:border-pearl-400 transition-all cursor-grab active:cursor-grabbing hover:shadow-xs group text-center"
                    >
                      <div className="w-10 h-10 bg-white border border-pearl-200 rounded flex items-center justify-center mb-1 shadow-2xs group-hover:border-pearl-400">
                        <span className="text-[10px] font-bold font-mono text-pearl-650">WC</span>
                      </div>
                      <span className="text-[9px] font-bold text-pearl-600 uppercase tracking-wider">Baños</span>
                    </div>

                    {/* Acceso */}
                    <div 
                      onClick={() => addDecoration('door')}
                      draggable
                      onDragStart={(e) => handleToolDragStart(e, 'decor-door')}
                      className="flex flex-col items-center justify-center p-2.5 rounded-lg border border-pearl-100 bg-ice-50 hover:bg-white hover:border-amber-700 transition-all cursor-grab active:cursor-grabbing hover:shadow-xs group text-center"
                    >
                      <div className="w-10 h-10 bg-white border border-pearl-200 rounded flex items-center justify-center mb-1 shadow-2xs group-hover:border-amber-500">
                        <DoorOpen size={16} className="text-amber-900" />
                      </div>
                      <span className="text-[9px] font-bold text-pearl-600 uppercase tracking-wider">Entrada</span>
                    </div>

                    {/* Escenario */}
                    <div 
                      onClick={() => addDecoration('stage')}
                      draggable
                      onDragStart={(e) => handleToolDragStart(e, 'decor-stage')}
                      className="flex flex-col items-center justify-center p-2.5 rounded-lg border border-pearl-100 bg-ice-50 hover:bg-white hover:border-indigo-400 transition-all cursor-grab active:cursor-grabbing hover:shadow-xs group text-center"
                    >
                      <div className="w-10 h-10 bg-white border border-pearl-200 rounded flex items-center justify-center mb-1 shadow-2xs group-hover:border-indigo-200">
                        <Music size={16} className="text-indigo-600" />
                      </div>
                      <span className="text-[9px] font-bold text-pearl-600 uppercase tracking-wider">Escenario</span>
                    </div>

                    {/* Muro */}
                    <div 
                      onClick={() => addDecoration('wall')}
                      draggable
                      onDragStart={(e) => handleToolDragStart(e, 'decor-wall')}
                      className="flex flex-col items-center justify-center p-2.5 rounded-lg border border-pearl-100 bg-ice-50 hover:bg-white hover:border-pearl-400 transition-all cursor-grab active:cursor-grabbing hover:shadow-xs group text-center"
                    >
                      <div className="w-10 h-10 bg-white border border-pearl-200 rounded flex items-center justify-center mb-1 shadow-2xs group-hover:border-pearl-300">
                        <div className="w-8 h-1 bg-pearl-400 rounded-full" />
                      </div>
                      <span className="text-[9px] font-bold text-pearl-600 uppercase tracking-wider">Muro</span>
                    </div>
                  </div>
                )}
              </div>

              {/* Sección: Pantalla Objetivo (Resolución) */}
              <div className="bg-white rounded-xl border border-pearl-100 p-4 shadow-xs space-y-3">
                <h3 className="text-xs font-bold text-pearl-800 uppercase tracking-widest flex items-center gap-2 border-b border-pearl-50 pb-2">
                  <Settings size={14} className="text-brand-500" />
                  Pantalla Destino (POS)
                </h3>
                
                <p className="text-[10px] text-pearl-400">Define las dimensiones de pantalla en píxeles del dispositivo que ejecutará el POS.</p>

                <div className="grid grid-cols-2 gap-2">
                  <div className="space-y-1">
                    <label className="text-[9px] font-bold text-pearl-500 uppercase">Ancho (px)</label>
                    <input
                      type="number"
                      min="400"
                      max="3840"
                      value={activeMap?.targetWidth || 1024}
                      onChange={(e) => updateBackgroundProperty('targetWidth', Math.max(1, parseInt(e.target.value)) || 1024)}
                      onBlur={handlePropertyChangeComplete}
                      className="w-full px-2 py-1.5 border border-pearl-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-brand-500 bg-white font-mono"
                    />
                  </div>
                  <div className="space-y-1">
                    <label className="text-[9px] font-bold text-pearl-500 uppercase">Alto (px)</label>
                    <input
                      type="number"
                      min="300"
                      max="2160"
                      value={activeMap?.targetHeight || 768}
                      onChange={(e) => updateBackgroundProperty('targetHeight', Math.max(1, parseInt(e.target.value)) || 768)}
                      onBlur={handlePropertyChangeComplete}
                      className="w-full px-2 py-1.5 border border-pearl-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-brand-500 bg-white font-mono"
                    />
                  </div>
                </div>

                {/* Resoluciones Rápidas */}
                <div className="space-y-1 pt-1.5">
                  <span className="text-[9px] font-bold text-pearl-400 uppercase">Preajustes comunes:</span>
                  <div className="grid grid-cols-2 gap-1.5">
                    {[
                      { label: 'iPad / Tablet', w: 1024, h: 768 },
                      { label: 'POS Estándar', w: 1280, h: 800 },
                      { label: 'Monitor Oficina', w: 1366, h: 768 },
                      { label: 'Full HD 1080p', w: 1920, h: 1080 }
                    ].map(res => (
                      <button
                        key={res.label}
                        onClick={() => {
                          updateBackgroundProperty('targetWidth', res.w);
                          updateBackgroundProperty('targetHeight', res.h);
                          showToast(`Resolución de "${activeMap.name}" fijada a ${res.w}x${res.h}px`);
                        }}
                        className={`py-1 px-1 border rounded text-[9px] font-semibold text-left transition-colors truncate
                          ${activeMap?.targetWidth === res.w && activeMap?.targetHeight === res.h 
                            ? 'bg-brand-50 border-brand-200 text-brand-700' 
                            : 'border-pearl-100 bg-white hover:bg-ice-50 text-pearl-500 hover:text-pearl-800'}
                        `}
                      >
                        {res.label} ({res.w}x{res.h})
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* Sección: Configuración del Fondo */}
              <div className="bg-white rounded-xl border border-pearl-100 p-4 shadow-xs space-y-4">
                <h3 className="text-xs font-bold text-pearl-800 uppercase tracking-widest flex items-center gap-2 border-b border-pearl-50 pb-2">
                  <ImageIcon size={14} className="text-purple-500" />
                  Personalizar Fondo
                </h3>

                {/* Tipo de Fondo */}
                <div className="space-y-1">
                  <label className="text-[10px] font-bold text-pearl-500 uppercase">Tipo de Fondo</label>
                  <div className="grid grid-cols-3 gap-1">
                    {(['color', 'texture', 'image'] as const).map(t => (
                      <button
                        key={t}
                        onClick={() => updateBackgroundProperty('bgType', t)}
                        className={`py-1 text-[10px] font-semibold border rounded-md capitalize transition-all
                          ${activeMap?.bgType === t 
                            ? 'bg-purple-50 border-purple-200 text-purple-700 font-bold' 
                            : 'bg-white border-pearl-100 text-pearl-500 hover:bg-ice-50'}
                        `}
                      >
                        {t === 'color' ? 'Color' : t === 'texture' ? 'Textura' : 'Imagen'}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Contenido condicional según tipo */}
                {activeMap?.bgType === 'color' && (
                  <div className="space-y-2 animate-fade-in">
                    <label className="text-[10px] font-bold text-pearl-500 uppercase">Color Sólido</label>
                    <div className="flex gap-2">
                      <input 
                        type="color" 
                        value={activeMap.bgColor || '#ffffff'}
                        onChange={(e) => updateBackgroundProperty('bgColor', e.target.value)}
                        className="w-8 h-8 rounded border border-pearl-200 p-0 cursor-pointer bg-transparent"
                      />
                      <input 
                        type="text" 
                        value={activeMap.bgColor || '#ffffff'}
                        onChange={(e) => updateBackgroundProperty('bgColor', e.target.value)}
                        placeholder="#ffffff"
                        className="flex-1 px-2.5 py-1 border border-pearl-200 rounded bg-white text-xs font-mono focus:outline-none focus:ring-1 focus:ring-brand-500"
                      />
                    </div>
                  </div>
                )}

                {activeMap?.bgType === 'texture' && (
                  <div className="space-y-2 animate-fade-in">
                    <label className="text-[10px] font-bold text-pearl-500 uppercase">Texturas Predefinidas</label>
                    <div className="grid grid-cols-2 gap-1.5">
                      {PRESET_TEXTURES.map(txt => (
                        <button
                          key={txt.id}
                          onClick={() => updateBackgroundProperty('bgTexture', txt.id)}
                          className={`flex items-center gap-1.5 p-1.5 border rounded-lg text-left transition-all
                            ${activeMap.bgTexture === txt.id 
                              ? 'border-purple-400 bg-purple-50/50 shadow-2xs' 
                              : 'border-pearl-150 hover:bg-ice-50'}
                          `}
                        >
                          <div className="w-4 h-4 rounded-full border border-pearl-200" style={{ backgroundColor: txt.color }} />
                          <span className="text-[10px] text-pearl-700 font-medium truncate">{txt.label}</span>
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                {activeMap?.bgType === 'image' && (
                  <div className="space-y-3 animate-fade-in">
                    <div className="space-y-1">
                      <label className="text-[10px] font-bold text-pearl-500 uppercase">Subir Plano/Imagen</label>
                      <input
                        type="file"
                        accept="image/*"
                        onChange={handleBackgroundImageUpload}
                        className="w-full text-[10px] text-pearl-500 file:mr-2 file:py-1 file:px-2 file:rounded-md file:border-0 file:text-[10px] file:font-semibold file:bg-purple-50 file:text-purple-700 hover:file:bg-purple-100 cursor-pointer"
                      />
                    </div>

                    <div className="space-y-1">
                      <label className="text-[10px] font-bold text-pearl-500 uppercase">URL de Imagen Remota</label>
                      <input
                        type="text"
                        value={activeMap.bgImage && !activeMap.bgImage.startsWith('data:') ? activeMap.bgImage : ''}
                        onChange={(e) => updateBackgroundProperty('bgImage', e.target.value)}
                        placeholder="https://ejemplo.com/plano.png"
                        className="w-full px-2 py-1.5 border border-pearl-200 rounded text-[10px] focus:outline-none focus:ring-1 focus:ring-brand-500 bg-white"
                      />
                    </div>

                    <div className="space-y-1">
                      <label className="text-[10px] font-bold text-pearl-500 uppercase">Ajuste de Imagen</label>
                      <select
                        value={activeMap.bgImageFit || 'cover'}
                        onChange={(e) => updateBackgroundProperty('bgImageFit', e.target.value)}
                        className="w-full px-2 py-1.5 border border-pearl-200 rounded text-[10px] focus:outline-none focus:ring-1 focus:ring-brand-500 bg-white"
                      >
                        <option value="cover">Estirar (Cover)</option>
                        <option value="contain">Ajustar (Contain)</option>
                        <option value="repeat">Mosaico (Repeat)</option>
                      </select>
                    </div>
                  </div>
                )}

                {/* Rejilla y Guías */}
                <div className="border-t border-pearl-50 pt-3 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-[10px] font-bold text-pearl-700 flex items-center gap-1">
                      <Grid size={12} className="text-pearl-400" />
                      Mostrar Rejilla
                    </span>
                    <label className="relative inline-flex items-center cursor-pointer">
                      <input 
                        type="checkbox" 
                        checked={activeMap ? activeMap.bgPattern !== 'none' : false} 
                        onChange={(e) => updateBackgroundProperty('bgPattern', e.target.checked ? 'grid' : 'none')}
                        className="sr-only peer"
                      />
                      <div className="w-7 h-4 bg-pearl-200 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-0.5 after:left-[2px] after:bg-white after:border-pearl-300 after:border after:rounded-full after:h-3 after:w-3 after:transition-all peer-checked:bg-brand-500" />
                    </label>
                  </div>
                  
                  {activeMap && activeMap.bgPattern !== 'none' && (
                    <div className="flex items-center justify-between gap-1 animate-fade-in">
                      <span className="text-[9px] text-pearl-400">Estilo:</span>
                      <div className="flex gap-1">
                        {(['grid', 'dots'] as const).map(p => (
                          <button
                            key={p}
                            onClick={() => updateBackgroundProperty('bgPattern', p)}
                            className={`px-1.5 py-0.5 text-[9px] font-bold border rounded capitalize
                              ${activeMap.bgPattern === p ? 'bg-pearl-100 border-pearl-300 text-pearl-700' : 'bg-white border-pearl-150 text-pearl-455'}
                            `}
                          >
                            {p === 'grid' ? 'Líneas' : 'Puntos'}
                          </button>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              </div>

              {/* Sección: Seguridad y Panel Admin POS */}
              <div className="bg-white rounded-xl border border-pearl-100 p-4 shadow-xs space-y-3">
                <h3 className="text-xs font-bold text-pearl-800 uppercase tracking-widest flex items-center gap-2 border-b border-pearl-50 pb-2">
                  <KeyRound size={14} className="text-amber-500 font-bold" />
                  Seguridad y Panel Admin POS
                </h3>
                <p className="text-[10px] text-pearl-400">Configura la visibilidad y permisos requeridos para las opciones administrativas del Punto de Venta.</p>
                
                <div className="space-y-3">
                  {adminButtons.map((btn, idx) => (
                    <div key={btn.id} className="p-2.5 bg-ice-50/50 border border-pearl-100 rounded-lg space-y-2">
                      <div className="flex items-center justify-between">
                        <span className="text-[11px] font-bold text-pearl-800">{btn.label}</span>
                        <label className="relative inline-flex items-center cursor-pointer">
                          <input 
                            type="checkbox" 
                            checked={btn.enabled}
                            onChange={(e) => {
                              const updated = adminButtons.map((b, i) => i === idx ? { ...b, enabled: e.target.checked } : b);
                              setAdminButtons(updated);
                            }}
                            className="sr-only peer"
                          />
                          <div className="w-7 h-4 bg-pearl-200 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-0.5 after:left-[2px] after:bg-white after:border-pearl-300 after:border after:rounded-full after:h-3 after:w-3 after:transition-all peer-checked:bg-amber-500" />
                        </label>
                      </div>
                      
                      {btn.enabled && (
                        <div className="space-y-1">
                          <label className="text-[9px] font-bold text-pearl-500 uppercase">Permiso requerido:</label>
                          <select
                            value={btn.requiredPermission}
                            onChange={(e) => {
                              const updated = adminButtons.map((b, i) => i === idx ? { ...b, requiredPermission: e.target.value } : b);
                              setAdminButtons(updated);
                            }}
                            className="w-full px-2 py-1 border border-pearl-200 rounded text-[10px] focus:outline-none focus:ring-1 focus:ring-amber-500 bg-white cursor-pointer"
                          >
                            <option value="pos.vender">Vender (pos.vender)</option>
                            <option value="pos.egresos">Registrar Egresos (pos.egresos)</option>
                            <option value="pos.cuentas_corrientes">Cuentas Corrientes (pos.cuentas_corrientes)</option>
                            <option value="pos.cierre_caja">Cierre de Caja (pos.cierre_caja)</option>
                            <option value="pos.descuentos">Aplicar Descuentos (pos.descuentos)</option>
                            <option value="pos.anular">Anular Comandas (pos.anular)</option>
                            <option value="gerente.override">Autorización Gerente (gerente.override)</option>
                          </select>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
        </div>

        {/* --- COLUMNA CENTRAL: Workspace del Salón (6 Cols) --- */}
        <div className="xl:col-span-6 flex flex-col bg-white border border-pearl-150 rounded-xl overflow-hidden shadow-xs min-h-[500px]">
          
          {/* Navegación y pestañas de salones */}
          <div className="flex items-center justify-between border-b border-pearl-100 bg-ice-50/50 p-2 overflow-x-auto select-none no-scrollbar">
            <div className="flex items-center gap-1">
              {maps.map(map => (
                <div
                  key={map.id}
                  onClick={() => {
                    setActiveMapId(map.id);
                    setSelectedTableId(null);
                  }}
                  className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold cursor-pointer border transition-all select-none whitespace-nowrap
                    ${activeMapId === map.id 
                      ? 'bg-white border-pearl-200 text-brand-700 shadow-2xs font-bold' 
                      : 'bg-transparent border-transparent text-pearl-500 hover:text-pearl-800 hover:bg-pearl-100/50'}
                  `}
                >
                  {editingMapId === map.id ? (
                    <input
                      type="text"
                      value={renameMapName}
                      onChange={(e) => setRenameMapName(e.target.value)}
                      onBlur={finishRenameMap}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') finishRenameMap();
                        if (e.key === 'Escape') setEditingMapId(null);
                      }}
                      className="px-1.5 py-0.5 border border-brand-300 rounded bg-white text-pearl-850 focus:outline-none focus:ring-1 focus:ring-brand-500 text-xs w-24"
                      autoFocus
                      onClick={(e) => e.stopPropagation()}
                    />
                  ) : (
                    <span onDoubleClick={() => startRenameMap(map)}>{map.name}</span>
                  )}
                  
                  {editingMapId !== map.id && (
                    <div className="flex items-center gap-0.5">
                      <button
                        onClick={(e) => { e.stopPropagation(); startRenameMap(map); }}
                        className="text-pearl-300 hover:text-pearl-600 p-0.5 rounded"
                        title="Renombrar"
                      >
                        <Edit2 size={11} />
                      </button>
                      <button
                        onClick={(e) => { e.stopPropagation(); duplicateMap(map); }}
                        className="text-pearl-300 hover:text-brand-600 p-0.5 rounded"
                        title="Duplicar"
                      >
                        <Copy size={11} />
                      </button>
                      {maps.length > 1 && (
                        <button
                          onClick={(e) => deleteMap(map.id, e)}
                          className="text-pearl-300 hover:text-danger-500 p-0.5 rounded"
                          title="Eliminar"
                        >
                          <Trash2 size={11} />
                        </button>
                      )}
                    </div>
                  )}
                </div>
              ))}
            </div>

            {/* Crear nuevo mapa */}
            {isAddingMap ? (
              <div className="flex items-center gap-1 ml-2" onClick={(e) => e.stopPropagation()}>
                <input
                  type="text"
                  placeholder="Nombre de salón..."
                  value={newMapName}
                  onChange={(e) => setNewMapName(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') handleCreateMap();
                    if (e.key === 'Escape') setIsAddingMap(false);
                  }}
                  className="px-2 py-1 border border-pearl-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-brand-500 w-28 bg-white"
                  autoFocus
                />
                <button 
                  onClick={handleCreateMap} 
                  className="p-1 bg-brand-600 text-white rounded hover:bg-brand-700 cursor-pointer"
                >
                  <Check size={12} />
                </button>
                <button 
                  onClick={() => setIsAddingMap(false)} 
                  className="p-1 bg-pearl-200 text-pearl-600 rounded hover:bg-pearl-300 cursor-pointer"
                >
                  <Trash size={12} />
                </button>
              </div>
            ) : (
              <button
                onClick={() => {
                  setIsAddingMap(true);
                  setNewMapName('');
                }}
                className="flex items-center gap-1 px-2.5 py-1.5 text-brand-600 hover:bg-brand-50 rounded-lg text-xs font-bold transition-colors cursor-pointer whitespace-nowrap"
              >
                <Plus size={12} />
                Añadir Salón
              </button>
            )}
          </div>

          {/* Lienzo / Canvas del Plano */}
          <div className="flex-1 bg-ice-100 p-4 flex items-center justify-center overflow-auto min-h-0 relative">
            <div 
              ref={canvasRef}
              onDragOver={(e) => e.preventDefault()}
              onDrop={handleCanvasDrop}
              onClick={() => setSelectedTableId(null)}
              className="w-full bg-white border border-pearl-200 rounded-lg shadow-sm relative overflow-hidden transition-all duration-300"
              style={{
                ...getCanvasStyles(),
                aspectRatio: `${activeMap?.targetWidth || 1024} / ${activeMap?.targetHeight || 768}`,
                maxWidth: `min(100%, calc(62vh * (${activeMap?.targetWidth || 1024} / ${activeMap?.targetHeight || 768})))`
              }}
            >
              {/* Indicador de resolución destino (Badge superior derecho) */}
              <div className="absolute top-2.5 right-2.5 bg-black/60 backdrop-blur-xs text-white px-2 py-1 rounded text-[10px] font-mono font-bold tracking-wider z-20 pointer-events-none">
                {activeMap?.targetWidth || 1024} x {activeMap?.targetHeight || 768} px
              </div>

              {/* Overlay de Rejilla / Puntos */}
              {activeMap && activeMap.bgPattern !== 'none' && (
                <div 
                  className="absolute inset-0 pointer-events-none opacity-20"
                  style={{
                    backgroundImage: activeMap.bgPattern === 'grid' 
                      ? 'linear-gradient(#6b7280 1px, transparent 1px), linear-gradient(90deg, #6b7280 1px, transparent 1px)'
                      : 'radial-gradient(#6b7280 1.5px, transparent 1.5px)',
                    backgroundSize: activeMap.bgPattern === 'grid' ? '30px 30px' : '20px 20px'
                  }}
                />
              )}

              {/* Elementos del Salón (Mesas y Decoraciones) */}
              {activeMap?.tables.map(table => {
                const isSelected = selectedTableId === table.id;
                
                return (
                  <div
                    key={table.id}
                    onPointerDown={(e) => handlePointerDown(e, table.id)}
                    onClick={(e) => e.stopPropagation()}
                    className={`absolute select-none flex items-center justify-center transition-all cursor-move group
                      ${isSelected 
                        ? 'ring-2 ring-brand-500 ring-offset-2 z-30 shadow-lg scale-[1.02]' 
                        : 'hover:scale-[1.01] hover:shadow-xs z-25'}
                    `}
                    style={{
                      left: `${table.x}%`,
                      top: `${table.y}%`,
                      width: `${table.width}%`,
                      height: `${table.height}%`,
                      transform: `rotate(${table.rotation || 0}deg)`,
                      backgroundColor: table.color || '#3b82f6',
                      color: getContrastColor(table.color || '#3b82f6'),
                      borderRadius: table.type === 'circle' ? '50%' : '6px',
                      border: '2px solid rgba(0, 0, 0, 0.15)',
                      overflow: 'visible', // Permite ver las sillas por fuera de la mesa
                    }}
                  >
                    {table.isDecoration ? (
                      /* Renderizado de Objeto Decorativo/Obstáculo */
                      <div className="flex flex-col items-center justify-center w-full h-full p-1 text-center pointer-events-none select-none">
                        {renderDecorationIcon(table.decorType)}
                        {table.width >= 8 && table.height >= 5 && (
                          <span className="text-[8px] font-extrabold uppercase mt-0.5 tracking-wider truncate max-w-full">
                            {table.label}
                          </span>
                        )}
                      </div>
                    ) : (
                      /* Renderizado de Mesa de Comensales */
                      <>
                        <span className="text-[10px] font-bold text-center px-1 truncate pointer-events-none select-none">
                          {table.label}
                        </span>

                        {/* Sillas alrededor de la mesa */}
                        {renderChairs(table.seats, table.type as 'square' | 'circle' | 'rectangle')}
                      </>
                    )}

                    {/* Botón Borrar Rápido (Hover) */}
                    {isSelected && (
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          deleteTable(table.id);
                        }}
                        className="delete-btn-inner absolute -top-3 -right-3 w-5 h-5 bg-danger-500 hover:bg-danger-600 text-white rounded-full flex items-center justify-center shadow-md transition-transform hover:scale-110 cursor-pointer z-40"
                        title="Eliminar elemento"
                      >
                        <Trash size={10} />
                      </button>
                    )}
                  </div>
                );
              })}

              {/* Mensaje si el salón está vacío */}
              {activeMap?.tables.length === 0 && (
                <div className="absolute inset-0 flex flex-col items-center justify-center text-center p-6 bg-white/70 backdrop-blur-3xs pointer-events-none">
                  <MousePointer size={36} className="text-pearl-300 animate-bounce mb-2" />
                  <p className="text-xs font-semibold text-pearl-600">Salón Vacío</p>
                  <p className="text-[10px] text-pearl-400">Arrastra o haz click en elementos de la izquierda para agregar mesas u obstáculos.</p>
                </div>
              )}
            </div>
          </div>

          {/* Información y Controles de Rejilla del Lienzo */}
          <div className="border-t border-pearl-100 px-4 py-3 bg-ice-50/50 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 text-xs">
            <span className="text-pearl-550 font-medium">
              {activeMap?.tables.filter(t => !t.isDecoration).length || 0} Mesas y {activeMap?.tables.filter(t => t.isDecoration).length || 0} Obstáculos en {activeMap?.name}
            </span>
            
            <div className="flex items-center gap-4 flex-wrap">
              <label className="flex items-center gap-1.5 cursor-pointer text-pearl-600 font-medium">
                <input 
                  type="checkbox" 
                  checked={snapToGrid}
                  onChange={(e) => setSnapToGrid(e.target.checked)}
                  className="rounded text-brand-600 focus:ring-brand-500 w-3.5 h-3.5"
                />
                Alineación Magnética
              </label>

              {snapToGrid && (
                <div className="flex items-center gap-1">
                  <span className="text-pearl-450 text-[11px]">Rejilla:</span>
                  <select
                    value={gridStep}
                    onChange={(e) => setGridStep(parseFloat(e.target.value))}
                    className="px-1.5 py-0.5 border border-pearl-200 rounded text-[11px] bg-white text-pearl-700"
                  >
                    <option value="1">Fino (1%)</option>
                    <option value="2.5">Medio (2.5%)</option>
                    <option value="5">Grueso (5%)</option>
                  </select>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* --- COLUMNA DERECHA: Propiedades del Elemento Seleccionado (3 Cols) --- */}
        <div className="xl:col-span-3">
          {selectedTable ? (
            <div className="bg-white rounded-xl border border-pearl-155 p-4 shadow-sm space-y-5 animate-fade-in">
              <div className="flex items-center justify-between border-b border-pearl-50 pb-2">
                <h3 className="text-xs font-bold text-pearl-800 uppercase tracking-widest flex items-center gap-2">
                  <Sliders size={14} className="text-brand-500" />
                  {selectedTable.isDecoration ? 'Editar Objeto' : 'Editar Mesa'}
                </h3>
                <span className="text-[10px] bg-brand-50 text-brand-700 px-2 py-0.5 rounded font-mono font-bold uppercase">
                  {selectedTable.isDecoration ? selectedTable.decorType : selectedTable.type}
                </span>
              </div>

              {/* Nombre / Identificador */}
              <div className="space-y-1">
                <label className="text-[10px] font-bold text-pearl-500 uppercase">Identificador / Nombre</label>
                <input 
                  type="text"
                  value={selectedTable.label}
                  onChange={(e) => updateTableProperty('label', e.target.value)}
                  onBlur={handlePropertyChangeComplete}
                  className="w-full px-3 py-1.5 border border-pearl-200 rounded-lg text-xs focus:outline-none focus:ring-1 focus:ring-brand-500 bg-white text-pearl-800 font-semibold"
                />
              </div>

              {/* Tipo de Objeto Decorativo (Sólo para decoraciones) */}
              {selectedTable.isDecoration && (
                <div className="space-y-1">
                  <label className="text-[10px] font-bold text-pearl-500 uppercase">Icono del Objeto</label>
                  <select
                    value={selectedTable.decorType || 'wall'}
                    onChange={(e) => {
                      updateTableProperty('decorType', e.target.value);
                      handlePropertyChangeComplete();
                    }}
                    className="w-full px-2 py-1.5 border border-pearl-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-brand-500 bg-white text-pearl-750"
                  >
                    <option value="bar">🍸 Barra de Bebidas</option>
                    <option value="plant">🌿 Planta / Decoración</option>
                    <option value="restroom">🚻 Baños / WC</option>
                    <option value="door">🚪 Entrada / Salida</option>
                    <option value="stage">🎵 Escenario / Tarima</option>
                    <option value="wall">🧱 Muro / Panel Divisor</option>
                  </select>
                </div>
              )}

              {/* Cantidad de Asientos (Sólo si NO es decoración) */}
              {!selectedTable.isDecoration && (
                <div className="space-y-1">
                  <div className="flex justify-between items-center">
                    <label className="text-[10px] font-bold text-pearl-500 uppercase">Capacidad (Sillas)</label>
                    <span className="text-xs font-bold text-brand-650">{selectedTable.seats} comensales</span>
                  </div>
                  <input 
                    type="range"
                    min="1"
                    max="12"
                    step="1"
                    value={selectedTable.seats}
                    onChange={(e) => updateTableProperty('seats', parseInt(e.target.value))}
                    onMouseUp={handlePropertyChangeComplete}
                    onTouchEnd={handlePropertyChangeComplete}
                    className="w-full accent-brand-500 cursor-pointer"
                  />
                </div>
              )}

              {/* Dimensiones (Ancho y Alto) */}
              <div className="space-y-3 border-t border-pearl-50 pt-3">
                <div className="space-y-1">
                  <div className="flex justify-between items-center">
                    <label className="text-[10px] font-bold text-pearl-500 uppercase">Ancho (%)</label>
                    <span className="text-[11px] font-mono text-pearl-400">{selectedTable.width}%</span>
                  </div>
                  <input 
                    type="range"
                    min="2"
                    max="45"
                    step="0.5"
                    value={selectedTable.width}
                    onChange={(e) => updateTableProperty('width', parseFloat(e.target.value))}
                    onMouseUp={handlePropertyChangeComplete}
                    onTouchEnd={handlePropertyChangeComplete}
                    className="w-full accent-brand-500 cursor-pointer"
                  />
                </div>

                {/* Mostrar Alto si es un rectángulo, pared, barra o escenario */}
                {(selectedTable.type === 'rectangle' || selectedTable.isDecoration) && (
                  <div className="space-y-1">
                    <div className="flex justify-between items-center">
                      <label className="text-[10px] font-bold text-pearl-500 uppercase">Alto (%)</label>
                      <span className="text-[11px] font-mono text-pearl-400">{selectedTable.height}%</span>
                    </div>
                    <input 
                      type="range"
                      min="1"
                      max="35"
                      step="0.5"
                      value={selectedTable.height}
                      onChange={(e) => updateTableProperty('height', parseFloat(e.target.value))}
                      onMouseUp={handlePropertyChangeComplete}
                      onTouchEnd={handlePropertyChangeComplete}
                      className="w-full accent-brand-500 cursor-pointer"
                    />
                  </div>
                )}
              </div>

              {/* Rotación */}
              <div className="space-y-1 border-t border-pearl-50 pt-3">
                <div className="flex justify-between items-center">
                  <label className="text-[10px] font-bold text-pearl-500 uppercase flex items-center gap-1">
                    <RotateCw size={11} className="text-pearl-450" />
                    Rotación
                  </label>
                  <span className="text-[11px] font-mono text-pearl-400">{selectedTable.rotation || 0}°</span>
                </div>
                <input 
                  type="range"
                  min="0"
                  max="360"
                  step="5"
                  value={selectedTable.rotation || 0}
                  onChange={(e) => updateTableProperty('rotation', parseInt(e.target.value))}
                  onMouseUp={handlePropertyChangeComplete}
                  onTouchEnd={handlePropertyChangeComplete}
                  className="w-full accent-brand-500 cursor-pointer"
                />
              </div>

              {/* Color de la Mesa / Objeto */}
              <div className="space-y-2 border-t border-pearl-50 pt-3">
                <label className="text-[10px] font-bold text-pearl-500 uppercase">Color de Fondo</label>
                
                {/* Paleta rápida */}
                <div className="grid grid-cols-5 gap-2">
                  {MESA_COLOR_PALETTE.map(color => (
                    <button
                      key={color}
                      onClick={() => {
                        updateTableProperty('color', color);
                        handlePropertyChangeComplete();
                      }}
                      className={`w-full aspect-square rounded-lg border shadow-3xs transition-transform hover:scale-110 cursor-pointer
                        ${selectedTable.color === color ? 'border-pearl-900 ring-1 ring-pearl-900 scale-105' : 'border-pearl-200'}
                      `}
                      style={{ backgroundColor: color }}
                      title={color}
                    />
                  ))}
                </div>

                {/* Color Selector Avanzado */}
                <div className="flex items-center gap-2 mt-2">
                  <input 
                    type="color" 
                    value={selectedTable.color || '#3b82f6'}
                    onChange={(e) => updateTableProperty('color', e.target.value)}
                    onBlur={handlePropertyChangeComplete}
                    className="w-8 h-8 rounded border border-pearl-200 p-0 cursor-pointer bg-transparent"
                  />
                  <input 
                    type="text" 
                    value={selectedTable.color || '#3b82f6'}
                    onChange={(e) => updateTableProperty('color', e.target.value)}
                    onBlur={handlePropertyChangeComplete}
                    className="flex-1 px-2 py-1.5 border border-pearl-200 rounded text-xs font-mono focus:outline-none focus:ring-1 focus:ring-brand-500 bg-white"
                  />
                </div>
              </div>

              {/* Acciones de Edición */}
              <div className="grid grid-cols-2 gap-2 border-t border-pearl-50 pt-3">
                <button
                  onClick={() => duplicateTable(selectedTable.id)}
                  className="flex items-center justify-center gap-1.5 py-2 px-3 border border-pearl-200 rounded-lg text-xs font-semibold hover:bg-ice-50 transition-colors text-pearl-700 cursor-pointer"
                >
                  <Copy size={13} />
                  Duplicar
                </button>
                <button
                  onClick={() => deleteTable(selectedTable.id)}
                  className="flex items-center justify-center gap-1.5 py-2 px-3 border border-danger-100 rounded-lg text-xs font-semibold hover:bg-danger-50 transition-colors text-danger-650 cursor-pointer"
                >
                  <Trash size={13} />
                  Eliminar
                </button>
              </div>
            </div>
          ) : (
            <div className="bg-white rounded-xl border border-pearl-150 p-6 shadow-sm text-center flex flex-col items-center justify-center h-48 xl:h-auto xl:py-16">
              <Settings size={28} className="text-pearl-300 animate-spin-slow mb-3" />
              <h4 className="text-xs font-bold text-pearl-800 uppercase tracking-wider">Editor de Propiedades</h4>
              <p className="text-[10px] text-pearl-400 max-w-[200px] mt-1">
                Haz clic sobre cualquier mesa u obstáculo en el plano para configurar su identificador, color, rotación, dimensiones e iconos.
              </p>
              
              <div className="mt-4 border-t border-pearl-50 pt-4 w-full text-left">
                <span className="text-[9px] font-bold text-pearl-400 uppercase tracking-widest">Consejo de Experto</span>
                <p className="text-[9px] text-pearl-500 mt-1 leading-relaxed">
                  • Mueve el elemento seleccionado usando las <b>flechas del teclado</b>.<br />
                  • Mantén presionado <b>Shift + Flecha</b> para mayor rapidez.<br />
                  • Presiona <b>Supr / Backspace</b> para eliminarlo o <b>Ctrl + D</b> para duplicarlo.
                </p>
              </div>
            </div>
          )}
        </div>

      </div>

      {/* --- Toast Notificaciones --- */}
      {toast && (
        <div className={`fixed bottom-4 right-4 z-50 flex items-center gap-2 px-4 py-3 rounded-lg shadow-lg text-white font-medium text-xs border animate-fade-in
          ${toast.type === 'success' ? 'bg-success-600 border-success-700' : ''}
          ${toast.type === 'error' ? 'bg-danger-600 border-danger-700' : ''}
          ${toast.type === 'info' ? 'bg-pearl-700 border-pearl-800' : ''}
        `}>
          <span>{toast.message}</span>
        </div>
      )}

    </div>
  );
}
