import { useState, useEffect } from 'react';
import { 
  FolderPlus, 
  Plus, 
  Edit2, 
  Folder, 
  ChefHat, 
  DollarSign, 
  Search, 
  Filter, 
  Check, 
  X, 
  RefreshCw, 
  CheckCircle2, 
  AlertCircle, 
  Sparkles,
  Power
} from 'lucide-react';
import api from '@/services/api';
import type { Categoria, Producto, Sucursal } from '@/common/types';
import { useAuth } from '@/contexts/AuthContext';
import { useSucursal } from '@/contexts/SucursalContext';

interface TipoVenta {
  id: string;
  nombre: string;
  aplicaRecargo: boolean;
  isActive: boolean;
}

interface ProductoPrecio {
  id?: string;
  productoId: string;
  sucursalId: string;
  tipoVentaId: string;
  precioVenta: number;
  isActive: boolean;
}

interface UpdateProductoPrecioRequest {
  sucursalId: string;
  tipoVentaId: string;
  precioVenta: number;
}

const COLOR_OPTIONS = [
  { value: '#3b82f6', name: 'Azul' },
  { value: '#10b981', name: 'Verde' },
  { value: '#f59e0b', name: 'Naranja' },
  { value: '#ef4444', name: 'Rojo' },
  { value: '#ec4899', name: 'Rosa' },
  { value: '#8b5cf6', name: 'Morado' },
  { value: '#06b6d4', name: 'Cian' },
  { value: '#6b7280', name: 'Gris' },
];

export default function CatalogoPage() {
  const { user } = useAuth();
  const { sucursales: sucursalesPermitidas, isGlobal } = useSucursal();
  const [activeTab, setActiveTab] = useState<'productos' | 'categorias'>('productos');
  const [categorias, setCategorias] = useState<Categoria[]>([]);
  const [productos, setProductos] = useState<Producto[]>([]);
  // Sucursales sobre las que este usuario puede fijar precios: todas si es global,
  // solo la propia si está acotado a una sucursal.
  const sucursales: Sucursal[] = isGlobal
    ? sucursalesPermitidas
    : user
      ? [{ id: user.sucursalId, nombre: user.sucursal, direccion: '', isActive: true, createdAt: '', updatedAt: '' }]
      : [];
  const [tiposVenta, setTiposVenta] = useState<TipoVenta[]>([]);
  const [loading, setLoading] = useState(true);

  // Filtros
  const [searchQuery, setSearchQuery] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');

  // Modales
  const [showCategoryModal, setShowCategoryModal] = useState(false);
  const [editingCategory, setEditingCategory] = useState<Categoria | null>(null);
  const [categoryForm, setCategoryForm] = useState({ nombre: '', ordenVisual: 1 });

  const [showProductModal, setShowProductModal] = useState(false);
  const [editingProduct, setEditingProduct] = useState<Producto | null>(null);
  const [productForm, setProductForm] = useState({
    nombre: '',
    categoriaId: '',
    colorUi: '#3b82f6',
    requiereCocina: false
  });

  // Modal de Precios
  const [showPriceModal, setShowPriceModal] = useState(false);
  const [selectedProductForPrices, setSelectedProductForPrices] = useState<Producto | null>(null);
  const [pricesGrid, setPricesGrid] = useState<Record<string, Record<string, number>>>({}); // { sucursalId: { tipoVentaId: precio } }
  const [savingPrices, setSavingPrices] = useState(false);
  const [uniquePriceInput, setUniquePriceInput] = useState('');

  // Carga de Datos
  const loadData = async () => {
    setLoading(true);
    try {
      const [catsRes, prodsRes, tvsRes] = await Promise.all([
        api.get('/categoria?includeInactive=true').catch(() => ({ data: [] })),
        api.get('/producto?includeInactive=true').catch(() => ({ data: [] })),
        api.get('/tipoventa?includeInactive=true').catch(() => ({ data: [] }))
      ]);

      setCategorias(catsRes.data || []);
      setProductos(prodsRes.data || []);
      setTiposVenta(tvsRes.data || []);
    } catch (err) {
      console.error('Error al cargar datos del catálogo:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  // --- GESTION CATEGORIAS ---

  const handleOpenCategoryModal = (cat?: Categoria) => {
    if (cat) {
      setEditingCategory(cat);
      setCategoryForm({ nombre: cat.nombre, ordenVisual: cat.ordenVisual });
    } else {
      setEditingCategory(null);
      setCategoryForm({ nombre: '', ordenVisual: categorias.length + 1 });
    }
    setShowCategoryModal(true);
  };

  const handleSaveCategory = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!categoryForm.nombre.trim()) return;

    try {
      if (editingCategory) {
        await api.put(`/categoria/${editingCategory.id}`, categoryForm);
      } else {
        await api.post('/categoria', categoryForm);
      }
      setShowCategoryModal(false);
      await loadData();
    } catch (err) {
      console.error('Error al guardar categoría:', err);
      alert('Error al guardar categoría');
    }
  };

  const handleToggleCategoryActive = async (cat: Categoria) => {
    try {
      if (cat.isActive) {
        if (confirm(`¿Estás seguro de que deseas desactivar la categoría "${cat.nombre}"?`)) {
          await api.delete(`/categoria/${cat.id}`);
        } else {
          return;
        }
      } else {
        await api.put(`/categoria/${cat.id}`, {
          nombre: cat.nombre,
          ordenVisual: cat.ordenVisual
        });
      }
      await loadData();
    } catch (err) {
      console.error('Error al cambiar estado de la categoría:', err);
      alert('Error al cambiar estado de la categoría');
    }
  };

  // --- GESTION PRODUCTOS ---

  const handleOpenProductModal = (prod?: Producto) => {
    if (prod) {
      setEditingProduct(prod);
      setProductForm({
        nombre: prod.nombre,
        categoriaId: prod.categoriaId,
        colorUi: prod.colorUi || '#3b82f6',
        requiereCocina: prod.requiereCocina,
        alicuotaIva: prod.alicuotaIva ?? 5
      });
    } else {
      setEditingProduct(null);
      setProductForm({
        nombre: '',
        categoriaId: categorias.find(c => c.isActive)?.id || '',
        colorUi: '#3b82f6',
        requiereCocina: false,
        alicuotaIva: 5
      });
    }
    setShowProductModal(true);
  };

  const handleSaveProduct = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!productForm.nombre.trim() || !productForm.categoriaId) return;

    try {
      const payload = {
        ...productForm,
        alicuotaIva: Number(productForm.alicuotaIva) || 5
      };

      if (editingProduct) {
        await api.put(`/producto/${editingProduct.id}`, payload);
      } else {
        await api.post('/producto', payload);
      }
      setShowProductModal(false);
      await loadData();
    } catch (err) {
      console.error('Error al guardar producto:', err);
      alert('Error al guardar producto');
    }
  };

  const handleToggleProductActive = async (prod: Producto) => {
    try {
      if (prod.isActive) {
        if (confirm(`¿Estás seguro de que deseas desactivar el producto "${prod.nombre}"?`)) {
          await api.delete(`/producto/${prod.id}`);
        } else {
          return;
        }
      } else {
        await api.put(`/producto/${prod.id}`, {
          nombre: prod.nombre,
          categoriaId: prod.categoriaId,
          colorUi: prod.colorUi,
          requiereCocina: prod.requiereCocina,
          alicuotaIva: prod.alicuotaIva ?? 5
        });
      }
      await loadData();
    } catch (err) {
      console.error('Error al cambiar estado del producto:', err);
      alert('Error al cambiar estado del producto');
    }
  };

  // --- GESTION PRECIOS ---

  const handleOpenPriceModal = async (prod: Producto) => {
    setSelectedProductForPrices(prod);
    setUniquePriceInput('');
    
    // Inicializar cuadrícula vacía
    const initialGrid: Record<string, Record<string, number>> = {};
    sucursales.forEach(s => {
      initialGrid[s.id] = {};
      tiposVenta.filter(t => t.isActive).forEach(t => {
        initialGrid[s.id][t.id] = 0;
      });
    });

    try {
      // Cargar precios existentes para este producto
      const res = await api.get(`/productoprecio/producto/${prod.id}`);
      const dbPrices: ProductoPrecio[] = res.data || [];
      
      dbPrices.forEach(p => {
        if (initialGrid[p.sucursalId]) {
          initialGrid[p.sucursalId][p.tipoVentaId] = p.precioVenta;
        }
      });
      
      setPricesGrid(initialGrid);
      setShowPriceModal(true);
    } catch (err) {
      console.error('Error al cargar precios del producto:', err);
      alert('Error al cargar precios');
    }
  };

  const handlePriceChange = (sucursalId: string, tipoVentaId: string, val: string) => {
    const numericValue = val === '' ? 0 : parseFloat(val);
    setPricesGrid(prev => ({
      ...prev,
      [sucursalId]: {
        ...prev[sucursalId],
        [tipoVentaId]: isNaN(numericValue) ? 0 : numericValue
      }
    }));
  };

  const handleApplyUniquePrice = () => {
    const val = parseFloat(uniquePriceInput);
    if (isNaN(val) || val < 0) return;

    setPricesGrid(prev => {
      const updated = { ...prev };
      sucursales.forEach(s => {
        updated[s.id] = { ...updated[s.id] };
        tiposVenta.filter(t => t.isActive).forEach(t => {
          updated[s.id][t.id] = val;
        });
      });
      return updated;
    });
  };

  const handleSavePrices = async () => {
    if (!selectedProductForPrices) return;
    setSavingPrices(true);

    try {
      const payload: UpdateProductoPrecioRequest[] = [];
      
      Object.entries(pricesGrid).forEach(([sucursalId, tvMap]) => {
        Object.entries(tvMap).forEach(([tipoVentaId, precio]) => {
          if (precio > 0) {
            payload.push({
              sucursalId,
              tipoVentaId,
              precioVenta: precio
            });
          }
        });
      });

      await api.put(`/productoprecio/producto/${selectedProductForPrices.id}`, payload);
      setShowPriceModal(false);
      await loadData();
    } catch (err) {
      console.error('Error al guardar precios:', err);
      alert('Error al guardar los precios en el servidor.');
    } finally {
      setSavingPrices(false);
    }
  };

  // --- FILTROS DE VISTA ---

  const filteredCategorias = categorias.filter(c => 
    c.nombre.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const filteredProductos = productos.filter(p => {
    const matchesSearch = p.nombre.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesCategory = !categoryFilter || p.categoriaId === categoryFilter;
    return matchesSearch && matchesCategory;
  });

  return (
    <div className="space-y-5 animate-fade-in pb-8">
      {/* Header */}
      <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-3">
        <div>
          <h2 className="text-xl font-bold text-pearl-900 flex items-center gap-2">
            <Sparkles className="text-brand-500" size={20} />
            Gestión del Menú & Catálogo
          </h2>
          <p className="text-xs text-pearl-400">Administra los productos de la carta, categorías de visualización y precios por local.</p>
        </div>
        <div className="flex items-center gap-2">
          <button 
            onClick={loadData}
            className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 rounded-lg hover:bg-ice-50 active:scale-[0.98] transition-all cursor-pointer bg-white"
            title="Refrescar datos"
          >
            <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
          </button>
          
          {activeTab === 'productos' ? (
            <button 
              onClick={() => handleOpenProductModal()}
              className="flex items-center gap-1.5 h-9 px-4 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all duration-200 cursor-pointer"
            >
              <Plus size={16} /> Nuevo Producto
            </button>
          ) : (
            <button 
              onClick={() => handleOpenCategoryModal()}
              className="flex items-center gap-1.5 h-9 px-4 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all duration-200 cursor-pointer"
            >
              <FolderPlus size={16} /> Nueva Categoría
            </button>
          )}
        </div>
      </div>

      {/* Tabs Layout */}
      <div className="flex border-b border-pearl-200">
        <button
          onClick={() => { setActiveTab('productos'); setSearchQuery(''); }}
          className={`px-4 py-2.5 text-sm font-semibold border-b-2 transition-all cursor-pointer ${activeTab === 'productos' ? 'border-brand-600 text-brand-700' : 'border-transparent text-pearl-400 hover:text-pearl-600'}`}
        >
          Productos ({productos.length})
        </button>
        <button
          onClick={() => { setActiveTab('categorias'); setSearchQuery(''); }}
          className={`px-4 py-2.5 text-sm font-semibold border-b-2 transition-all cursor-pointer ${activeTab === 'categorias' ? 'border-brand-600 text-brand-700' : 'border-transparent text-pearl-400 hover:text-pearl-600'}`}
        >
          Categorías ({categorias.length})
        </button>
      </div>

      {/* Filtros */}
      <div className="flex flex-wrap items-center gap-2 bg-white p-3 rounded-xl border border-pearl-100 shadow-sm">
        <div className="relative flex-1 min-w-[200px]">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-pearl-400" />
          <input 
            type="text" 
            placeholder={`Buscar por nombre en ${activeTab}...`}
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            className="w-full h-8.5 pl-9 pr-3 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400 transition-all"
          />
        </div>

        {activeTab === 'productos' && (
          <div className="relative">
            <Filter size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-pearl-400" />
            <select
              value={categoryFilter}
              onChange={e => setCategoryFilter(e.target.value)}
              className="h-8.5 pl-7 pr-6 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 text-pearl-600 cursor-pointer appearance-none"
            >
              <option value="">Todas las Categorías</option>
              {categorias.filter(c => c.isActive).map(c => (
                <option key={c.id} value={c.id}>{c.nombre}</option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* Contenido principal */}
      {loading && productos.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <RefreshCw size={36} className="animate-spin text-brand-500 mb-3" />
          <p className="text-sm">Cargando catálogo consolidado de la nube...</p>
        </div>
      ) : activeTab === 'productos' ? (
        /* VISTA DE PRODUCTOS */
        <div className="bg-white rounded-xl border border-pearl-100 overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="bg-ice-50 border-b border-pearl-100 text-pearl-500 text-[10px] font-semibold uppercase tracking-wider">
                  <th className="px-4 py-3 text-center w-12">UI</th>
                  <th className="px-4 py-3">Nombre del Producto</th>
                  <th className="px-4 py-3">Categoría</th>
                  <th className="px-4 py-3 text-center">Alícuota IVA</th>
                  <th className="px-4 py-3 text-center">Cocina</th>
                  <th className="px-4 py-3 text-center">Estado</th>
                  <th className="px-4 py-3 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-pearl-50">
                {filteredProductos.map(p => {
                  const cat = categorias.find(c => c.id === p.categoriaId);
                  const ivaLabels: Record<number, string> = {
                    5: '21%',
                    4: '10.5%',
                    6: '27%',
                    1: 'Exento (0%)',
                    0: 'No Gravado'
                  };
                  const ivaLabel = ivaLabels[p.alicuotaIva ?? 5] || '21%';

                  return (
                    <tr key={p.id} className={`hover:bg-brand-50/20 transition-colors ${!p.isActive ? 'opacity-65' : ''}`}>
                      <td className="px-4 py-3 text-center">
                        <div 
                          className="w-5 h-5 rounded-full mx-auto border border-black/10 shadow-sm"
                          style={{ backgroundColor: p.colorUi }}
                          title={`Color de botón UI: ${p.colorUi}`}
                        />
                      </td>
                      <td className="px-4 py-3 font-semibold text-pearl-800">{p.nombre}</td>
                      <td className="px-4 py-3">
                        <span className="text-xs bg-ice-100 text-pearl-600 px-2 py-0.5 rounded border border-pearl-200">
                          {cat?.nombre || 'Sin categoría'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-center">
                        <span className="text-[11px] font-mono font-bold text-brand-700 bg-brand-50 border border-brand-200 px-2 py-0.5 rounded">
                          {ivaLabel}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-center">
                        {p.requiereCocina ? (
                          <div className="inline-flex items-center gap-1 text-[10px] font-medium text-warning-600 bg-warning-50 border border-warning-200/50 px-2 py-0.5 rounded-full">
                            <ChefHat size={12} /> Requiere
                          </div>
                        ) : (
                          <span className="text-[10px] text-pearl-400">—</span>
                        )}
                      </td>
                      <td className="px-4 py-3 text-center">
                        <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full border ${p.isActive ? 'bg-success-50 text-success-600 border-success-200' : 'bg-pearl-100 text-pearl-500 border-pearl-200'}`}>
                          {p.isActive ? 'Activo' : 'Inactivo'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right">
                        <div className="flex items-center justify-end gap-2.5">
                          <button 
                            onClick={() => handleOpenPriceModal(p)}
                            className="flex items-center gap-1 text-xs text-brand-600 hover:text-brand-800 font-semibold cursor-pointer border border-brand-200 hover:border-brand-300 bg-brand-50/30 px-2 py-1 rounded"
                            title="Ver / Asignar precios por local"
                          >
                            <DollarSign size={13} /> Precios
                          </button>
                          
                          <button 
                            onClick={() => handleOpenProductModal(p)}
                            className="text-pearl-500 hover:text-brand-600 p-1 rounded hover:bg-ice-50 cursor-pointer"
                            title="Editar producto"
                          >
                            <Edit2 size={13} />
                          </button>

                          <button 
                            onClick={() => handleToggleProductActive(p)}
                            className={`p-1 rounded hover:bg-ice-50 cursor-pointer ${p.isActive ? 'text-pearl-400 hover:text-danger-500' : 'text-success-500 hover:text-success-600'}`}
                            title={p.isActive ? 'Desactivar producto' : 'Activar producto'}
                          >
                            <Power size={13} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}

                {filteredProductos.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-12 text-center text-pearl-400 italic">
                      No se encontraron productos. Crea uno nuevo usando el botón superior.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        /* VISTA DE CATEGORIAS */
        <div className="bg-white rounded-xl border border-pearl-100 overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="bg-ice-50 border-b border-pearl-100 text-pearl-500 text-[10px] font-semibold uppercase tracking-wider">
                  <th className="px-4 py-3 w-16 text-center">Orden</th>
                  <th className="px-4 py-3">Nombre de la Categoría</th>
                  <th className="px-4 py-3 text-center">Estado</th>
                  <th className="px-4 py-3 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-pearl-50">
                {filteredCategorias.map(c => (
                  <tr key={c.id} className={`hover:bg-brand-50/20 transition-colors ${!c.isActive ? 'opacity-65' : ''}`}>
                    <td className="px-4 py-3 text-center font-mono font-semibold text-brand-600 bg-ice-50/30">
                      #{c.ordenVisual}
                    </td>
                    <td className="px-4 py-3 font-semibold text-pearl-800 flex items-center gap-2">
                      <Folder size={14} className="text-brand-400" />
                      {c.nombre}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full border ${c.isActive ? 'bg-success-50 text-success-600 border-success-200' : 'bg-pearl-100 text-pearl-500 border-pearl-200'}`}>
                        {c.isActive ? 'Activo' : 'Inactivo'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex items-center justify-end gap-2.5">
                        <button 
                          onClick={() => handleOpenCategoryModal(c)}
                          className="text-pearl-500 hover:text-brand-600 p-1 rounded hover:bg-ice-50 cursor-pointer"
                          title="Editar categoría"
                        >
                          <Edit2 size={13} />
                        </button>

                        <button 
                          onClick={() => handleToggleCategoryActive(c)}
                          className={`p-1 rounded hover:bg-ice-50 cursor-pointer ${c.isActive ? 'text-pearl-400 hover:text-danger-500' : 'text-success-500 hover:text-success-600'}`}
                          title={c.isActive ? 'Desactivar categoría' : 'Activar categoría'}
                        >
                          <Power size={13} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}

                {filteredCategorias.length === 0 && (
                  <tr>
                    <td colSpan={4} className="px-4 py-12 text-center text-pearl-400 italic">
                      No se encontraron categorías. Crea una usando el botón superior.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* --- MODAL CATEGORIA --- */}
      {showCategoryModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={() => setShowCategoryModal(false)}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-sm mx-4 p-5 animate-fade-in" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-bold text-pearl-900">
                {editingCategory ? 'Editar Categoría' : 'Nueva Categoría'}
              </h3>
              <button onClick={() => setShowCategoryModal(false)} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={16} />
              </button>
            </div>

            <form onSubmit={handleSaveCategory} className="space-y-3.5">
              <div>
                <label className="block text-[11px] font-semibold text-pearl-600 mb-1">Nombre</label>
                <input 
                  type="text"
                  required
                  placeholder="Ej: Cervezas, Hamburguesas, Tragos..."
                  value={categoryForm.nombre}
                  onChange={e => setCategoryForm(prev => ({ ...prev, nombre: e.target.value }))}
                  className="w-full h-8.5 px-3 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 placeholder:text-pearl-400"
                />
              </div>

              <div>
                <label className="block text-[11px] font-semibold text-pearl-600 mb-1">Orden de Visualización en POS</label>
                <input 
                  type="number"
                  required
                  min={1}
                  value={categoryForm.ordenVisual}
                  onChange={e => setCategoryForm(prev => ({ ...prev, ordenVisual: parseInt(e.target.value) || 1 }))}
                  className="w-full h-8.5 px-3 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
                />
              </div>

              <div className="flex gap-2 pt-2 border-t border-pearl-50">
                <button 
                  type="button"
                  onClick={() => setShowCategoryModal(false)}
                  className="flex-1 h-8.5 text-xs font-semibold text-pearl-600 bg-pearl-100 rounded-lg hover:bg-pearl-200 transition-colors cursor-pointer"
                >
                  Cancelar
                </button>
                <button 
                  type="submit"
                  className="flex-1 h-8.5 text-xs font-semibold bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all cursor-pointer"
                >
                  Guardar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* --- MODAL PRODUCTO --- */}
      {showProductModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={() => setShowProductModal(false)}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-md mx-4 p-5 animate-fade-in animate-scale-in" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-bold text-pearl-900">
                {editingProduct ? 'Editar Producto' : 'Nuevo Producto'}
              </h3>
              <button onClick={() => setShowProductModal(false)} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={16} />
              </button>
            </div>

            <form onSubmit={handleSaveProduct} className="space-y-4">
              <div>
                <label className="block text-[11px] font-semibold text-pearl-600 mb-1">Nombre</label>
                <input 
                  type="text"
                  required
                  placeholder="Ej: Cerveza IPA 500cc, Pizza Muzzarella..."
                  value={productForm.nombre}
                  onChange={e => setProductForm(prev => ({ ...prev, nombre: e.target.value }))}
                  className="w-full h-8.5 px-3 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 placeholder:text-pearl-400"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-[11px] font-semibold text-pearl-600 mb-1">Categoría</label>
                  <select
                    required
                    value={productForm.categoriaId}
                    onChange={e => setProductForm(prev => ({ ...prev, categoriaId: e.target.value }))}
                    className="w-full h-8.5 px-2.5 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 cursor-pointer"
                  >
                    <option value="" disabled>Seleccionar...</option>
                    {categorias.filter(c => c.isActive).map(c => (
                      <option key={c.id} value={c.id}>{c.nombre}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-[11px] font-semibold text-pearl-600 mb-1">Alícuota IVA (Fiscal)</label>
                  <select
                    value={productForm.alicuotaIva ?? 5}
                    onChange={e => setProductForm(prev => ({ ...prev, alicuotaIva: Number(e.target.value) }))}
                    className="w-full h-8.5 px-2.5 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 cursor-pointer font-medium"
                  >
                    <option value={5}>21% (General)</option>
                    <option value={4}>10.5% (Reducido)</option>
                    <option value={6}>27% (Servicios)</option>
                    <option value={1}>Exento (0%)</option>
                    <option value={0}>No Gravado</option>
                  </select>
                </div>
              </div>

              <div className="pt-1">
                <label className="flex items-center gap-2 cursor-pointer select-none text-xs font-semibold text-pearl-600">
                  <input 
                    type="checkbox"
                    checked={productForm.requiereCocina}
                    onChange={e => setProductForm(prev => ({ ...prev, requiereCocina: e.target.checked }))}
                    className="w-4 h-4 rounded text-brand-600 border-pearl-300 focus:ring-brand-500 cursor-pointer"
                  />
                  <span>¿Pasa por cocina? (Genera ticket comandera)</span>
                </label>
              </div>

              <div>
                <label className="block text-[11px] font-semibold text-pearl-600 mb-2">Color del Botón (Punto de Venta Local)</label>
                <div className="grid grid-cols-8 gap-2">
                  {COLOR_OPTIONS.map(opt => {
                    const isSelected = productForm.colorUi.toLowerCase() === opt.value.toLowerCase();
                    return (
                      <button
                        key={opt.value}
                        type="button"
                        onClick={() => setProductForm(prev => ({ ...prev, colorUi: opt.value }))}
                        className={`w-7 h-7 rounded-full flex items-center justify-center border transition-all cursor-pointer hover:scale-105 active:scale-95 ${isSelected ? 'border-brand-600 ring-2 ring-brand-100 scale-105 shadow-sm' : 'border-black/15'}`}
                        style={{ backgroundColor: opt.value }}
                        title={opt.name}
                      >
                        {isSelected && <Check size={12} className="text-white drop-shadow-md stroke-[3]" />}
                      </button>
                    );
                  })}
                </div>
              </div>

              <div className="flex gap-2 pt-2 border-t border-pearl-50">
                <button 
                  type="button"
                  onClick={() => setShowProductModal(false)}
                  className="flex-1 h-8.5 text-xs font-semibold text-pearl-600 bg-pearl-100 rounded-lg hover:bg-pearl-200 transition-colors cursor-pointer"
                >
                  Cancelar
                </button>
                <button 
                  type="submit"
                  className="flex-1 h-8.5 text-xs font-semibold bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all cursor-pointer"
                >
                  Guardar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* --- MODAL MATRIZ DE PRECIOS --- */}
      {showPriceModal && selectedProductForPrices && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={() => setShowPriceModal(false)}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-2xl mx-4 p-6 animate-fade-in flex flex-col max-h-[90vh]" onClick={e => e.stopPropagation()}>
            {/* Header */}
            <div className="flex items-center justify-between pb-4 border-b border-pearl-100 shrink-0">
              <div>
                <h3 className="text-sm font-bold text-pearl-900 flex items-center gap-1.5">
                  <DollarSign size={16} className="text-brand-500" />
                  Asignar Precios: {selectedProductForPrices.nombre}
                </h3>
                <p className="text-[11px] text-pearl-400 mt-0.5">Define los valores de venta segmentados por sucursal y tipo de consumo.</p>
              </div>
              <button onClick={() => setShowPriceModal(false)} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            {/* Quick Fill Tool */}
            <div className="bg-ice-50 rounded-xl p-3 border border-pearl-100 my-4 flex flex-wrap items-center justify-between gap-3 shrink-0">
              <div className="flex items-center gap-2">
                <Sparkles size={14} className="text-brand-500" />
                <span className="text-xs font-semibold text-pearl-700">Fijar Precio Uniforme</span>
              </div>
              <div className="flex items-center gap-2">
                <div className="relative">
                  <DollarSign size={12} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-pearl-500 font-bold" />
                  <input 
                    type="number"
                    placeholder="Ej: 3500.00"
                    value={uniquePriceInput}
                    onChange={e => setUniquePriceInput(e.target.value)}
                    className="h-8 pl-6 pr-2.5 w-32 text-xs bg-white border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
                  />
                </div>
                <button 
                  type="button"
                  onClick={handleApplyUniquePrice}
                  className="h-8 px-3 text-xs font-semibold bg-pearl-800 text-white rounded-lg hover:bg-pearl-900 active:scale-[0.98] transition-all cursor-pointer"
                >
                  Aplicar a Todo
                </button>
              </div>
            </div>

            {/* Pricing Matrix */}
            <div className="flex-1 overflow-y-auto mb-4 border border-pearl-100 rounded-xl overflow-hidden">
              <table className="w-full text-left text-xs border-collapse">
                <thead>
                  <tr className="bg-ice-50 border-b border-pearl-100 text-[10px] font-bold text-pearl-500 uppercase">
                    <th className="px-4 py-3 bg-ice-100 font-semibold sticky left-0 z-10">Sucursal</th>
                    {tiposVenta.filter(t => t.isActive).map(t => (
                      <th key={t.id} className="px-4 py-3 text-center font-semibold">
                        {t.nombre} {t.aplicaRecargo && <span className="text-[9px] text-warning-500 font-normal">(Recargo)</span>}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-pearl-50">
                  {sucursales.filter(s => s.isActive).map(suc => (
                    <tr key={suc.id} className="hover:bg-brand-50/10 transition-colors">
                      <td className="px-4 py-3 font-semibold text-pearl-800 bg-ice-50/20 w-44 sticky left-0 z-10 border-r border-pearl-50 shadow-sm">
                        {suc.nombre}
                      </td>
                      {tiposVenta.filter(t => t.isActive).map(t => {
                        const val = pricesGrid[suc.id]?.[t.id] ?? 0;
                        return (
                          <td key={t.id} className="px-4 py-2 text-center w-36">
                            <div className="relative inline-block w-28">
                              <span className="absolute left-2.5 top-1/2 -translate-y-1/2 text-pearl-400 font-medium">$</span>
                              <input 
                                type="number"
                                min={0}
                                step="any"
                                placeholder="0.00"
                                value={val === 0 ? '' : val}
                                onChange={e => handlePriceChange(suc.id, t.id, e.target.value)}
                                className="w-full h-8 pl-5.5 pr-2.5 text-center text-xs font-semibold text-pearl-800 bg-white border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-1 focus:ring-brand-100"
                              />
                            </div>
                          </td>
                        );
                      })}
                    </tr>
                  ))}

                  {sucursales.filter(s => s.isActive).length === 0 && (
                    <tr>
                      <td colSpan={tiposVenta.length + 1} className="px-4 py-8 text-center text-pearl-400 italic">
                        No hay sucursales activas registradas para fijar precios.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            {/* Matrix Help Label */}
            <div className="flex items-start gap-2 text-[10px] text-pearl-400 mb-4 px-1 shrink-0">
              <AlertCircle size={13} className="shrink-0 text-pearl-400 mt-0.5" />
              <span>Los precios configurados con valor superior a cero se guardarán. Aquellos que se dejen vacíos o en 0.00 se omitirán o desactivarán para el respectivo local.</span>
            </div>

            {/* Footer Buttons */}
            <div className="flex gap-2 pt-3 border-t border-pearl-100 shrink-0">
              <button 
                type="button"
                onClick={() => setShowPriceModal(false)}
                className="flex-1 h-9.5 text-xs font-semibold text-pearl-600 bg-pearl-100 rounded-lg hover:bg-pearl-200 transition-colors cursor-pointer"
              >
                Cancelar
              </button>
              <button 
                type="button"
                onClick={handleSavePrices}
                disabled={savingPrices}
                className="flex-1 h-9.5 text-xs font-semibold bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] disabled:opacity-50 transition-all cursor-pointer flex items-center justify-center gap-1.5"
              >
                {savingPrices ? (
                  <>
                    <RefreshCw size={14} className="animate-spin" /> Guardando...
                  </>
                ) : (
                  <>
                    <CheckCircle2 size={14} /> Guardar Precios
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
