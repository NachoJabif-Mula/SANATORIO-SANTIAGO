import { useState, useEffect } from 'react';
import { Package, UtensilsCrossed, Plus, Search, Trash2, X, Loader2 } from 'lucide-react';
import api from '@/services/api';

interface Insumo {
  id: string;
  nombre: string;
  unidad: string;
  stock: number;
  costoRef: number;
}

interface Producto {
  id: string;
  nombre: string;
}

interface RecetaItem {
  id: string;
  insumoId: string;
  cantidad: number;
}

export default function InventarioPage() {
  const [activeTab, setActiveTab] = useState<'stock' | 'recetas'>('stock');
  const [insumos, setInsumos] = useState<Insumo[]>([]);
  const [productos, setProductos] = useState<Producto[]>([]);
  const [selectedProduct, setSelectedProduct] = useState<string>('');
  const [recetaItems, setRecetaItems] = useState<RecetaItem[]>([]);
  const [loadingData, setLoadingData] = useState(true);

  // Form states for recipe
  const [newInsumoId, setNewInsumoId] = useState('');
  const [newCantidad, setNewCantidad] = useState('');

  // Modal states for new insumo
  const [showInsumoModal, setShowInsumoModal] = useState(false);
  const [insumoNombre, setInsumoNombre] = useState('');
  const [insumoUnidad, setInsumoUnidad] = useState('ml');
  const [insumoStockMin, setInsumoStockMin] = useState('10');
  const [submittingInsumo, setSubmittingInsumo] = useState(false);

  // Search filter
  const [searchTerm, setSearchTerm] = useState('');

  const loadInsumosAndProducts = async () => {
    setLoadingData(true);
    try {
      const [insumosRes, productosRes] = await Promise.all([
        api.get('/insumo'),
        api.get('/producto')
      ]);

      const mappedInsumos = insumosRes.data.map((i: any) => ({
        id: i.id,
        nombre: i.nombre,
        unidad: i.unidadMedida,
        stock: i.stockMinimo * 5 || 100, // Simulamos stock basado en stock minimo
        costoRef: 150 // Simulación del costo referencial ya que no se persiste en BD
      }));

      setInsumos(mappedInsumos);
      setProductos(productosRes.data);
    } catch (err) {
      console.error('Error al cargar insumos/productos:', err);
    } finally {
      setLoadingData(false);
    }
  };

  useEffect(() => {
    loadInsumosAndProducts();
  }, []);

  // Cargar la receta del producto seleccionado
  useEffect(() => {
    if (!selectedProduct) {
      setRecetaItems([]);
      return;
    }

    const loadRecetas = async () => {
      try {
        const res = await api.get(`/receta/por-producto/${selectedProduct}`);
        const mapped = res.data.map((r: any) => ({
          id: r.id,
          insumoId: r.insumoId,
          cantidad: r.cantidadNecesaria
        }));
        setRecetaItems(mapped);
      } catch (err) {
        console.error('Error al cargar recetas del producto:', err);
      }
    };

    loadRecetas();
  }, [selectedProduct]);

  const handleUpdateStock = (id: string, newStock: number) => {
    setInsumos(insumos.map(i => i.id === id ? { ...i, stock: newStock } : i));
  };

  const addInsumoToReceta = async () => {
    if (!selectedProduct || !newInsumoId || !newCantidad) return;
    try {
      // Registrar receta en base de datos
      const res = await api.post('/receta', {
        productoId: selectedProduct,
        insumoId: newInsumoId,
        cantidadNecesaria: Number(newCantidad)
      });
      // Recargar recetas
      const newItem: RecetaItem = {
        id: res.data.id,
        insumoId: newInsumoId,
        cantidad: Number(newCantidad)
      };
      setRecetaItems([...recetaItems, newItem]);
      setNewInsumoId('');
      setNewCantidad('');
    } catch (err) {
      console.error('Error al añadir insumo a receta:', err);
      alert('Error al agregar el insumo en el servidor. Puede que ya exista en la receta.');
    }
  };

  const removeRecetaItem = async (id: string) => {
    try {
      await api.delete(`/receta/${id}`);
      setRecetaItems(recetaItems.filter(r => r.id !== id));
    } catch (err) {
      console.error('Error al eliminar ítem de receta:', err);
      alert('Error al eliminar el ítem en el servidor.');
    }
  };

  const handleCreateInsumo = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!insumoNombre.trim()) return;
    setSubmittingInsumo(true);

    try {
      await api.post('/insumo', {
        nombre: insumoNombre,
        unidadMedida: insumoUnidad,
        stockMinimo: Number(insumoStockMin)
      });
      setShowInsumoModal(false);
      setInsumoNombre('');
      setInsumoUnidad('ml');
      setInsumoStockMin('10');
      await loadInsumosAndProducts();
    } catch (err) {
      console.error('Error creando insumo:', err);
      alert('Error al crear el insumo.');
    } finally {
      setSubmittingInsumo(false);
    }
  };

  // Costo total de la receta armada
  const costoTotalReceta = recetaItems.reduce((total, item) => {
    const insumo = insumos.find(i => i.id === item.insumoId);
    return total + (insumo ? insumo.costoRef * item.cantidad : 0);
  }, 0);

  const filteredInsumos = insumos.filter(i => 
    i.nombre.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="space-y-6 animate-fade-in pb-8">
      <div>
        <h2 className="text-xl font-bold text-pearl-900">Inventario y Escandallos</h2>
        <p className="text-xs text-pearl-400">Gestioná el stock de insumos y armá las recetas de tus productos</p>
      </div>

      {/* Custom Tabs */}
      <div className="flex bg-white rounded-lg border border-pearl-200 p-1 w-fit shadow-sm">
        <button
          onClick={() => setActiveTab('stock')}
          className={`flex items-center gap-2 px-4 py-2 rounded-md text-sm font-medium transition-all duration-200 ${
            activeTab === 'stock' ? 'bg-ice-100 text-pearl-900 shadow-sm' : 'text-pearl-500 hover:text-pearl-700 hover:bg-ice-50'
          }`}
        >
          <Package size={16} />
          Stock e Insumos
        </button>
        <button
          onClick={() => setActiveTab('recetas')}
          className={`flex items-center gap-2 px-4 py-2 rounded-md text-sm font-medium transition-all duration-200 ${
            activeTab === 'recetas' ? 'bg-ice-100 text-pearl-900 shadow-sm' : 'text-pearl-500 hover:text-pearl-700 hover:bg-ice-50'
          }`}
        >
          <UtensilsCrossed size={16} />
          Recetas (Escandallos)
        </button>
      </div>

      {activeTab === 'stock' && (
        <div className="bg-white border border-pearl-100 rounded-xl overflow-hidden shadow-sm animate-fade-in">
          <div className="p-4 border-b border-pearl-100 flex justify-between items-center bg-ice-50">
            <div className="relative group">
              <Search size={14} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-pearl-400 group-focus-within:text-brand-500 transition-colors" />
              <input 
                type="text" 
                placeholder="Buscar insumo..." 
                value={searchTerm}
                onChange={e => setSearchTerm(e.target.value)}
                className="w-64 h-9 pl-8 pr-3 text-sm bg-white border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 transition-all"
              />
            </div>
            <button 
              onClick={() => setShowInsumoModal(true)}
              className="flex items-center gap-2 px-4 py-2 bg-brand-600 text-white rounded-lg text-sm font-medium hover:bg-brand-700 transition-colors shadow-sm cursor-pointer"
            >
              <Plus size={16} /> Nuevo Insumo
            </button>
          </div>
          
          <table className="w-full text-sm text-left">
            <thead>
              <tr className="bg-white border-b border-pearl-100 text-[10px] text-pearl-500 uppercase tracking-wider font-semibold">
                <th className="px-6 py-3">Insumo</th>
                <th className="px-6 py-3">Unidad</th>
                <th className="px-6 py-3 text-right">Costo Ref. (x Unidad)</th>
                <th className="px-6 py-3 text-right">Stock Actual</th>
                <th className="px-6 py-3 text-center">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-pearl-50">
              {loadingData ? (
                <tr>
                  <td colSpan={5} className="px-6 py-12 text-center text-pearl-400">
                    <div className="flex items-center justify-center gap-2">
                      <Loader2 size={16} className="animate-spin text-brand-500" />
                      <span>Cargando insumos...</span>
                    </div>
                  </td>
                </tr>
              ) : (
                filteredInsumos.map((insumo) => (
                  <tr key={insumo.id} className="hover:bg-ice-50 transition-colors">
                    <td className="px-6 py-3 font-medium text-pearl-800">{insumo.nombre}</td>
                    <td className="px-6 py-3 text-pearl-500 font-mono text-xs">{insumo.unidad}</td>
                    <td className="px-6 py-3 text-right text-pearl-600">${insumo.costoRef}</td>
                    <td className="px-6 py-3">
                      <div className="flex justify-end">
                        <input 
                          type="number" 
                          value={insumo.stock}
                          onChange={(e) => handleUpdateStock(insumo.id, Number(e.target.value))}
                          className="w-24 text-right px-2 py-1 bg-white border border-pearl-200 rounded outline-none focus:border-brand-400 font-mono text-sm"
                        />
                      </div>
                    </td>
                    <td className="px-6 py-3 text-center">
                      <span className="text-pearl-300 text-xs">Sincronizado</span>
                    </td>
                  </tr>
                ))
              )}
              {!loadingData && filteredInsumos.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-6 py-12 text-center text-pearl-400">
                    No se encontraron insumos.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {activeTab === 'recetas' && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 animate-fade-in">
          
          {/* Columna Izquierda: Selector de Producto y Formulario */}
          <div className="lg:col-span-4 space-y-6">
            <div className="bg-white p-5 rounded-xl border border-pearl-100 shadow-sm">
              <label className="block text-xs font-semibold text-pearl-600 uppercase tracking-wider mb-2">
                1. Seleccionar Producto
              </label>
              <select 
                value={selectedProduct}
                onChange={(e) => setSelectedProduct(e.target.value)}
                className="w-full h-10 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 cursor-pointer text-pearl-800 font-medium"
              >
                <option value="">Seleccione un producto...</option>
                {productos.map(p => (
                  <option key={p.id} value={p.id}>{p.nombre}</option>
                ))}
              </select>
            </div>

            <div className={`bg-white p-5 rounded-xl border border-pearl-100 shadow-sm transition-opacity duration-300 ${!selectedProduct ? 'opacity-50 pointer-events-none' : ''}`}>
              <label className="block text-xs font-semibold text-pearl-600 uppercase tracking-wider mb-4">
                2. Agregar Insumos
              </label>
              
              <div className="space-y-4">
                <div>
                  <label className="block text-[11px] text-pearl-500 mb-1">Insumo</label>
                  <select 
                    value={newInsumoId}
                    onChange={(e) => setNewInsumoId(e.target.value)}
                    className="w-full h-9 px-3 text-sm bg-white border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
                  >
                    <option value="">Seleccionar insumo...</option>
                    {insumos.map(i => (
                      <option key={i.id} value={i.id}>{i.nombre} ({i.unidad})</option>
                    ))}
                  </select>
                </div>
                
                <div>
                  <label className="block text-[11px] text-pearl-500 mb-1">Cantidad a descontar</label>
                  <div className="flex gap-2">
                    <input 
                      type="number" 
                      value={newCantidad}
                      onChange={(e) => setNewCantidad(e.target.value)}
                      placeholder="Ej: 50"
                      className="flex-1 h-9 px-3 text-sm bg-white border border-pearl-200 rounded-lg outline-none focus:border-brand-400 font-mono"
                    />
                    <button 
                      onClick={addInsumoToReceta}
                      disabled={!newInsumoId || !newCantidad}
                      className="h-9 px-4 bg-ice-200 text-pearl-700 rounded-lg hover:bg-brand-100 hover:text-brand-700 disabled:opacity-50 transition-colors font-medium flex items-center gap-1 cursor-pointer"
                    >
                      <Plus size={16} /> Añadir
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Columna Derecha: Receta Armada */}
          <div className="lg:col-span-8">
            <div className="bg-white rounded-xl border border-pearl-100 shadow-sm h-full flex flex-col">
              <div className="p-5 border-b border-pearl-100 flex justify-between items-center bg-ice-50 rounded-t-xl">
                <div>
                  <h3 className="text-sm font-bold text-pearl-900 flex items-center gap-2">
                    Composición de la Receta
                  </h3>
                  <p className="text-xs text-pearl-500 mt-1">
                    {selectedProduct ? productos.find(p => p.id === selectedProduct)?.nombre : 'Ningún producto seleccionado'}
                  </p>
                </div>
                <button 
                  onClick={loadInsumosAndProducts}
                  className="flex items-center gap-2 px-4 py-2 border border-pearl-200 text-pearl-600 rounded-lg text-xs font-medium hover:bg-ice-50 transition-colors cursor-pointer"
                >
                  Refrescar Receta
                </button>
              </div>

              <div className="flex-1 p-5 overflow-auto">
                {recetaItems.length === 0 ? (
                  <div className="h-full flex flex-col items-center justify-center text-pearl-300 py-16">
                    <UtensilsCrossed size={48} className="mb-4 opacity-50" />
                    <p className="text-sm">La receta está vacía.</p>
                    <p className="text-xs">Agregá insumos desde el panel izquierdo.</p>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {recetaItems.map((item, index) => {
                      const insumo = insumos.find(i => i.id === item.insumoId);
                      if (!insumo) return null;
                      
                      const costoFila = insumo.costoRef * item.cantidad;

                      return (
                        <div key={item.id} className="flex items-center justify-between p-3 border border-pearl-100 rounded-lg bg-ice-50 hover:bg-white transition-colors group">
                          <div className="flex items-center gap-4">
                            <span className="w-6 h-6 rounded-full bg-pearl-200 text-pearl-600 flex items-center justify-center text-xs font-bold">
                              {index + 1}
                            </span>
                            <div>
                              <p className="text-sm font-medium text-pearl-800">{insumo.nombre}</p>
                              <div className="flex items-center gap-1 mt-0.5">
                                <span className="text-[10px] text-pearl-400 font-mono">Descuenta:</span>
                                <span className="text-xs font-bold text-danger-600">{item.cantidad} {insumo.unidad}</span>
                              </div>
                            </div>
                          </div>
                          
                          <div className="flex items-center gap-6">
                            <div className="text-right">
                              <p className="text-[10px] text-pearl-400 uppercase">Costo</p>
                              <p className="text-sm font-mono font-medium text-pearl-700">${costoFila}</p>
                            </div>
                            <button 
                              onClick={() => removeRecetaItem(item.id)}
                              className="text-pearl-300 hover:text-danger-500 transition-colors p-2 cursor-pointer"
                            >
                              <Trash2 size={16} />
                            </button>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
              
              {/* Footer Total */}
              {recetaItems.length > 0 && (
                <div className="p-4 border-t border-pearl-200 bg-brand-50 flex justify-between items-center rounded-b-xl">
                  <span className="text-sm font-bold text-brand-800 uppercase tracking-wider">Costo Total Escandallo</span>
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-brand-600 font-medium">({recetaItems.length} insumos)</span>
                    <span className="text-lg font-bold text-brand-700 font-mono">${costoTotalReceta.toLocaleString()}</span>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Modal Nuevo Insumo */}
      {showInsumoModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={() => setShowInsumoModal(false)}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-md mx-4 p-6 animate-fade-in" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-5">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
                  <Package size={20} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-pearl-900">Nuevo Insumo</h4>
                  <p className="text-[11px] text-pearl-400">Registra una nueva materia prima</p>
                </div>
              </div>
              <button onClick={() => setShowInsumoModal(false)} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleCreateInsumo} className="space-y-4">
              <div>
                <label htmlFor="insumo-nombre" className="block text-xs font-medium text-pearl-600 mb-1.5">Nombre del insumo</label>
                <input 
                  id="insumo-nombre" 
                  type="text" 
                  value={insumoNombre} 
                  onChange={e => setInsumoNombre(e.target.value)} 
                  placeholder="Ej: Ron Blanco o Pan Brioche" 
                  required
                  className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400" 
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label htmlFor="insumo-unidad" className="block text-xs font-medium text-pearl-600 mb-1.5">Unidad</label>
                  <select 
                    id="insumo-unidad" 
                    value={insumoUnidad} 
                    onChange={e => setInsumoUnidad(e.target.value)}
                    className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 cursor-pointer"
                  >
                    <option value="ml">ml</option>
                    <option value="gr">gr</option>
                    <option value="u">unidad</option>
                    <option value="kg">kg</option>
                  </select>
                </div>
                <div>
                  <label htmlFor="insumo-stockmin" className="block text-xs font-medium text-pearl-600 mb-1.5">Stock Mínimo</label>
                  <input 
                    id="insumo-stockmin" 
                    type="number" 
                    value={insumoStockMin} 
                    onChange={e => setInsumoStockMin(e.target.value)} 
                    required
                    className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100" 
                  />
                </div>
              </div>
              <div className="flex gap-2 pt-2">
                <button 
                  type="button"
                  onClick={() => setShowInsumoModal(false)} 
                  className="flex-1 h-9 text-sm font-medium text-pearl-600 bg-pearl-100 rounded-lg hover:bg-pearl-200 transition-colors cursor-pointer"
                >
                  Cancelar
                </button>
                <button 
                  type="submit"
                  disabled={submittingInsumo || !insumoNombre.trim()}
                  className="flex-1 h-9 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed transition-all cursor-pointer"
                >
                  {submittingInsumo ? 'Creando...' : 'Crear Insumo'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
