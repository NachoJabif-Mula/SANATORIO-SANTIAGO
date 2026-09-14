import { useState, useEffect } from 'react';
import { Wine, Beer, UtensilsCrossed, CakeSlice, CupSoda, Coffee, Search } from 'lucide-react';
import type { Categoria, Producto } from '@/common/types';
import api from '@/services/api';
import { useComanda } from '@/contexts/AppContext';

/** Mapeo de iconos de categoría */
const ICON_MAP: Record<string, React.ReactNode> = {
  wine: <Wine className="w-6 h-6" />,
  beer: <Beer className="w-6 h-6" />,
  'utensils-crossed': <UtensilsCrossed className="w-6 h-6" />,
  'cake-slice': <CakeSlice className="w-6 h-6" />,
  'cup-soda': <CupSoda className="w-6 h-6" />,
  coffee: <Coffee className="w-6 h-6" />,
};

/** Formateador de moneda ARS */
function formatARS(monto: number): string {
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(monto);
}

export default function CatalogoPanel() {
  const [categorias, setCategorias] = useState<Categoria[]>([]);
  const [productos, setProductos] = useState<Producto[]>([]);
  const [categoriaActiva, setCategoriaActiva] = useState<string | number>('');
  const [busqueda, setBusqueda] = useState('');
  const [loading, setLoading] = useState(true);
  const { agregarItem } = useComanda();

  // --- Cargar Catálogo desde la API Local ---
  useEffect(() => {
    const fetchCatalog = async () => {
      try {
        const [catsRes, prodsRes] = await Promise.all([
          api.get('/catalogo/categorias'),
          api.get('/catalogo/productos')
        ]);
        setCategorias(catsRes.data);
        setProductos(prodsRes.data);
        if (catsRes.data.length > 0) {
          setCategoriaActiva(catsRes.data[0].id);
        }
      } catch (err) {
        console.error('Error cargando catálogo:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchCatalog();
  }, []);

  // Filtrar productos
  const productosFiltrados = productos.filter(p => {
    const matchCategoria = p.categoriaId === categoriaActiva;
    const matchBusqueda = busqueda === '' || p.nombre.toLowerCase().includes(busqueda.toLowerCase());
    return matchCategoria && matchBusqueda;
  });

  if (loading) {
    return (
      <div className="h-full w-full flex items-center justify-center text-text-muted">
        <div className="flex flex-col items-center gap-2">
          <div className="w-8 h-8 border-4 border-amber-500 border-t-transparent rounded-full animate-spin" />
          <span className="text-sm font-semibold">Cargando catálogo...</span>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-full bg-surface-base">
      {/* Categorías — lista lateral compacta */}
      <aside className="flex-shrink-0 w-[160px] border-r border-border-default flex flex-col min-h-0">
        <div className="px-4 pt-3.5 pb-2 mono-label">Categorías</div>
        <div className="flex-1 overflow-y-auto px-2 pb-2 flex flex-col gap-0.5">
          {categorias.map((cat: Categoria) => {
            const isActive = categoriaActiva === cat.id && !busqueda;
            return (
              <button
                key={cat.id}
                id={`cat-${cat.id}`}
                onClick={() => {
                  setCategoriaActiva(cat.id);
                  setBusqueda('');
                }}
                className={`touch-btn flex items-center gap-2 px-3 py-2.5 rounded-[var(--radius-btn)] text-left
                  text-[13.5px] font-semibold border
                  ${isActive
                    ? 'bg-surface-overlay text-text-primary border-amber-500'
                    : 'bg-transparent text-text-secondary border-transparent hover:border-border-default'}
                `}
              >
                <span className="[&>svg]:w-4 [&>svg]:h-4 flex-shrink-0" style={{ color: isActive ? cat.color : undefined }}>
                  {ICON_MAP[cat.icono] || null}
                </span>
                <span className="truncate">{cat.nombre}</span>
              </button>
            );
          })}
        </div>
      </aside>

      <div className="flex flex-col flex-1 min-w-0 min-h-0">
        {/* Barra de búsqueda */}
        <div className="flex-shrink-0 flex items-center gap-3 px-4 py-2.5 border-b border-border-default">
          <div className="relative flex-1 max-w-[320px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-muted" />
            <input
              id="search-productos"
              type="text"
              placeholder="Buscar por código o nombre"
              value={busqueda}
              onChange={e => setBusqueda(e.target.value)}
              className="w-full h-[52px] pl-9 pr-4 rounded-[var(--radius-input)] bg-surface-overlay border border-border-default
                text-text-primary placeholder:text-text-muted text-[15px] font-mono
                focus:outline-none focus:border-amber-500"
            />
          </div>
        </div>

        {/* Grilla de productos */}
        <div className="flex-1 overflow-y-auto p-4">
          <div className="grid grid-cols-2 lg:grid-cols-3 gap-2.5 stagger-children">
            {productosFiltrados.map((prod: Producto) => (
              <button
                key={prod.id}
                id={`prod-${prod.id}`}
                onClick={() => prod.disponible && agregarItem(prod)}
                disabled={!prod.disponible}
                className={`
                  touch-btn relative flex flex-col justify-between items-start p-3.5
                  rounded-[var(--radius-card)] border text-left
                  min-h-[104px]
                  ${
                    prod.disponible
                      ? 'bg-surface-base border-border-default hover:border-amber-500 active:scale-[0.98]'
                      : 'bg-surface-base border-border-subtle opacity-40 cursor-not-allowed'
                  }
                `}
              >
                {/* Nombre */}
                <span className="text-[15.5px] font-semibold text-text-primary leading-tight">
                  {prod.nombre}
                </span>

                {/* Precio */}
                <span className="flex justify-between items-baseline w-full gap-1.5 mt-2">
                  <span className={`text-sm font-bold font-mono ${prod.disponible ? 'text-text-primary' : 'text-text-muted line-through'}`}>
                    {formatARS(prod.precio)}
                  </span>
                </span>

                {/* Badge no disponible */}
                {!prod.disponible && (
                  <span className="absolute top-2 right-2 text-[9px] font-semibold uppercase tracking-wider text-danger-500 bg-surface-base border border-danger-500/40 px-1.5 py-0.5 rounded-[3px]">
                    Agotado
                  </span>
                )}
              </button>
            ))}
          </div>

          {/* Sin resultados */}
          {productosFiltrados.length === 0 && (
            <div className="flex flex-col items-center justify-center py-20 text-text-muted">
              <Search className="w-8 h-8 mb-3 opacity-30" />
              <p className="text-sm font-semibold text-text-secondary">No se encontraron productos</p>
              <p className="text-xs mt-0.5 text-text-muted/80">Intente con otra búsqueda o categoría</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
