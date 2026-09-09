import { useState, useEffect, useCallback } from 'react';
import { XCircle, RefreshCw, User, ChevronLeft, ChevronRight } from 'lucide-react';
import api from '@/services/api';
import { useSucursal } from '@/contexts/SucursalContext';
import DateRangeFilter from '@/common/components/DateRangeFilter';
import { getHoyISO } from '@/common/utils/date';

interface TransaccionAnuladaRow {
  id: string;
  numeroOrden: string;
  comandaId: string;
  monto: number;
  motivo: string;
  usuarioNombre: string;
  fecha: string;
}

const TAMANO_PAGINA = 50;

export default function TransaccionesAnuladasPage() {
  const { selectedSucursalId } = useSucursal();
  const [items, setItems] = useState<TransaccionAnuladaRow[]>([]);
  const [total, setTotal] = useState(0);
  const [pagina, setPagina] = useState(1);
  const [loading, setLoading] = useState(true);
  const [desde, setDesde] = useState(getHoyISO());
  const [hasta, setHasta] = useState(getHoyISO());

  const loadAnulaciones = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get('/reportes/anulaciones/comandas', {
        params: {
          desde: desde || undefined,
          hasta: hasta || undefined,
          sucursalId: selectedSucursalId ?? undefined,
          pagina,
          tamanoPagina: TAMANO_PAGINA
        }
      });
      setItems(res.data.items || []);
      setTotal(res.data.total || 0);
    } catch (err) {
      console.error('Error al cargar el reporte de transacciones anuladas:', err);
    } finally {
      setLoading(false);
    }
  }, [desde, hasta, selectedSucursalId, pagina]);

  useEffect(() => {
    loadAnulaciones();
  }, [loadAnulaciones]);

  const formatCurrency = (val: number) =>
    new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(val);

  const formatFecha = (dateStr: string) =>
    new Date(dateStr).toLocaleString('es-AR', {
      year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit'
    });

  const totalMonto = items.reduce((sum, i) => sum + i.monto, 0);
  const totalPaginas = Math.max(1, Math.ceil(total / TAMANO_PAGINA));

  const handleFiltrar = () => {
    setPagina(1);
    loadAnulaciones();
  };

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Transacciones Anuladas</h3>
          <p className="text-xs text-pearl-400">
            Órdenes completas anuladas en el período de fechas seleccionado
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <DateRangeFilter
            desde={desde}
            hasta={hasta}
            onDesdeChange={setDesde}
            onHastaChange={setHasta}
            onFiltrar={handleFiltrar}
          />
          <button
            onClick={loadAnulaciones}
            className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 bg-white rounded-lg hover:bg-ice-50 active:scale-[0.98] transition-all cursor-pointer"
            title="Refrescar"
          >
            <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      {/* Tarjetas resumen */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="bg-white rounded-xl border border-pearl-100 p-5 shadow-sm">
          <span className="text-[10px] font-bold text-pearl-400 uppercase tracking-widest block">Órdenes anuladas (página)</span>
          <span className="text-2xl font-extrabold text-pearl-900 block mt-1">{items.length} de {total}</span>
        </div>
        <div className="bg-white rounded-xl border border-pearl-100 p-5 shadow-sm">
          <span className="text-[10px] font-bold text-pearl-400 uppercase tracking-widest block">Monto anulado (página)</span>
          <span className="text-2xl font-extrabold text-danger-600 block mt-1">{formatCurrency(totalMonto)}</span>
        </div>
      </div>

      {loading && items.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <XCircle size={40} className="animate-pulse mb-3" />
          <p className="text-sm">Cargando transacciones anuladas...</p>
        </div>
      ) : (
        <div className="bg-white border border-pearl-100 rounded-2xl overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-pearl-700">
              <thead>
                <tr className="bg-ice-50 border-b border-pearl-100 text-pearl-500 font-semibold text-xs uppercase tracking-wider">
                  <th className="py-4.5 px-6">N° de Orden</th>
                  <th className="py-4.5 px-6 text-right">Monto</th>
                  <th className="py-4.5 px-6">Motivo</th>
                  <th className="py-4.5 px-6">Quién anuló</th>
                  <th className="py-4.5 px-6">Cuándo</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-pearl-50 text-sm">
                {items.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-8 text-center text-pearl-400">
                      No se encontraron transacciones anuladas en el rango seleccionado.
                    </td>
                  </tr>
                ) : (
                  items.map(a => (
                    <tr key={a.id} className="hover:bg-ice-50/50 transition-colors">
                      <td className="py-4 px-6 font-mono text-xs font-semibold text-pearl-900">{a.numeroOrden}</td>
                      <td className="py-4 px-6 text-right text-danger-600 font-semibold tabular-nums">{formatCurrency(a.monto)}</td>
                      <td className="py-4 px-6 max-w-xs truncate" title={a.motivo}>{a.motivo}</td>
                      <td className="py-4 px-6">
                        <div className="flex items-center gap-1.5">
                          <User size={14} className="text-pearl-400" />
                          <span>{a.usuarioNombre}</span>
                        </div>
                      </td>
                      <td className="py-4 px-6 text-pearl-500 text-xs">{formatFecha(a.fecha)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {totalPaginas > 1 && (
            <div className="flex items-center justify-between px-6 py-3 border-t border-pearl-100">
              <span className="text-xs text-pearl-400">Página {pagina} de {totalPaginas}</span>
              <div className="flex gap-2">
                <button
                  onClick={() => setPagina(p => Math.max(1, p - 1))}
                  disabled={pagina <= 1}
                  className="flex items-center gap-1 px-3 py-1.5 border border-pearl-200 rounded-lg text-xs font-semibold disabled:opacity-40 disabled:cursor-not-allowed hover:border-brand-500 hover:text-brand-600 transition-all cursor-pointer"
                >
                  <ChevronLeft size={14} /> Anterior
                </button>
                <button
                  onClick={() => setPagina(p => Math.min(totalPaginas, p + 1))}
                  disabled={pagina >= totalPaginas}
                  className="flex items-center gap-1 px-3 py-1.5 border border-pearl-200 rounded-lg text-xs font-semibold disabled:opacity-40 disabled:cursor-not-allowed hover:border-brand-500 hover:text-brand-600 transition-all cursor-pointer"
                >
                  Siguiente <ChevronRight size={14} />
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
