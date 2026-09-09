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
    <div className="flex flex-col h-full bg-slate-950">
      {/* Barra de búsqueda */}
      <div className="p-4 pb-2">
        <div className="relative">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4.5 h-4.5 text-text-muted" />
          <input
            id="search-productos"
            type="text"
            placeholder="Buscar producto..."
            value={busqueda}
            onChange={e => setBusqueda(e.target.value)}
            className="w-full h-11 pl-10 pr-4 rounded-[var(--radius-input)] bg-slate-900/60 border border-border-default/80
              text-text-primary placeholder:text-text-muted text-sm
              focus:outline-none focus:border-amber-500/40 focus:ring-1 focus:ring-amber-500/20
              transition-all duration-150"
          />
        </div>
      </div>

      {/* Categorías - scroll horizontal */}
      <div className="px-4 pb-2">
        <div className="flex gap-1.5 overflow-x-auto pb-1.5 scrollbar-none">
          {categorias.map((cat: Categoria) => {
            const isActive = categoriaActiva === cat.id;
            return (
              <button
                key={cat.id}
                id={`cat-${cat.id}`}
                onClick={() => {
                  setCategoriaActiva(cat.id);
                  setBusqueda('');
                }}
                className={`
                  touch-btn flex items-center gap-1.5 px-3.5 py-2.5 rounded-[var(--radius-btn)] whitespace-nowrap
                  text-xs font-semibold transition-all duration-150 flex-shrink-0 border min-h-[40px] relative
                  ${
                    isActive
                      ? 'bg-slate-850 text-text-primary border-border-strong'
                      : 'bg-slate-900/50 text-text-secondary border-border-default/50 hover:text-text-primary hover:bg-slate-850 hover:border-border-default'
                  }
                `}
              >
                <span className="[&>svg]:w-4 [&>svg]:h-4" style={{ color: isActive ? cat.color : undefined }}>
                  {ICON_MAP[cat.icono] || null}
                </span>
                {cat.nombre}
                {isActive && (
                  <span
                    className="absolute left-3 right-3 -bottom-[1.5px] h-[2px] rounded-full"
                    style={{ backgroundColor: cat.color }}
                  />
                )}
              </button>
            );
          })}
        </div>
      </div>

      {/* Grilla de productos */}
      <div className="flex-1 overflow-y-auto px-4 pb-4">
        <div className="grid grid-cols-2 lg:grid-cols-3 gap-2.5 stagger-children">
          {productosFiltrados.map((prod: Producto) => (
            <button
              key={prod.id}
              id={`prod-${prod.id}`}
              onClick={() => prod.disponible && agregarItem(prod)}
              disabled={!prod.disponible}
              className={`
                touch-btn relative flex flex-col items-start justify-between p-3.5
                rounded-[var(--radius-card)] border transition-all duration-150 text-left
                min-h-[100px]
                ${
                  prod.disponible
                    ? 'bg-slate-900/50 border-border-default/60 hover:bg-slate-850 hover:border-border-strong active:scale-[0.98]'
                    : 'bg-slate-950/40 border-border-subtle/30 opacity-40 cursor-not-allowed'
                }
              `}
            >
              {/* Nombre */}
              <span className="text-sm font-semibold text-text-primary leading-tight pr-6">
                {prod.nombre}
              </span>

              {/* Precio */}
              <span
                className={`text-sm font-bold mt-2 tabular-nums ${
                  prod.disponible ? 'text-text-secondary' : 'text-text-muted line-through'
                }`}
              >
                {formatARS(prod.precio)}
              </span>

              {/* Badge no disponible */}
              {!prod.disponible && (
                <span className="absolute top-2 right-2 text-[9px] font-semibold uppercase tracking-wider text-danger-400 bg-slate-900 border border-danger-500/25 px-1.5 py-0.5 rounded-[3px]">
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
  );
}
