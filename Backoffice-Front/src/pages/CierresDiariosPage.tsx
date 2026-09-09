import { useState, useEffect, useCallback } from 'react';
import { 
  Calendar, 
  RefreshCw, 
  Eye, 
  X, 
  User, 
  Clock, 
  FileText,
  Search
} from 'lucide-react';
import api from '@/services/api';
import { useSucursal } from '@/contexts/SucursalContext';
import DateRangeFilter from '@/common/components/DateRangeFilter';
import { getHoyISO } from '@/common/utils/date';

interface CierreDiario {
  id: string;
  cajaId: string;
  cajaNombre: string;
  fecha: string;
  usuarioCierreId: string;
  usuarioCierreNombre: string;
  totalVentas: number;
  totalEgresos: number;
  totalNeto: number;
  observaciones?: string;
  createdAt: string;
}

interface DetalleTurno {
  turnoId: string;
  usuarioNombre: string;
  fechaApertura: string;
  fechaCierre: string;
  fondoInicial: number;
  diferenciaArqueo: number;
}

interface DesglosePorMetodo {
  metodoPagoId: string;
  metodoPagoNombre: string;
  cantidadOperaciones: number;
  total: number;
}

interface CierreDiarioDetalle extends CierreDiario {
  resumenJson?: string;
}

export default function CierresDiariosPage() {
  const { selectedSucursalId } = useSucursal();
  const [cierres, setCierres] = useState<CierreDiario[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [desde, setDesde] = useState(getHoyISO());
  const [hasta, setHasta] = useState(getHoyISO());

  // Detail Modal State
  const [selectedCierre, setSelectedCierre] = useState<CierreDiarioDetalle | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [resumenData, setResumenData] = useState<{
    turnos: DetalleTurno[];
    desglosePorMetodo: DesglosePorMetodo[];
    totalVentas: number;
    totalEgresos: number;
    totalIngresos: number;
    totalNeto: number;
  } | null>(null);

  const loadCierres = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get('/cierrediario', {
        params: {
          sucursalId: selectedSucursalId ?? undefined,
          desde: desde || undefined,
          hasta: hasta || undefined
        }
      });
      setCierres(res.data);
    } catch (err) {
      console.error('Error al cargar cierres diarios:', err);
    } finally {
      setLoading(false);
    }
  }, [selectedSucursalId, desde, hasta]);

  useEffect(() => {
    loadCierres();
  }, [loadCierres]);

  const handleVerDetalle = async (cierre: CierreDiario) => {
    setLoadingDetail(true);
    setSelectedCierre(cierre);
    setResumenData(null);
    try {
      const res = await api.get(`/cierrediario/${cierre.id}`);
      const detail: CierreDiarioDetalle = res.data;
      setSelectedCierre(detail);
      if (detail.resumenJson) {
        try {
          const parsed = JSON.parse(detail.resumenJson);
          setResumenData(parsed);
        } catch (e) {
          console.error('Error al parsear resumenJson:', e);
        }
      }
    } catch (err) {
      console.error('Error al cargar detalle del cierre:', err);
      alert('No se pudo cargar el detalle del cierre seleccionado.');
      setSelectedCierre(null);
    } finally {
      setLoadingDetail(false);
    }
  };

  const handleCerrarDetalle = () => {
    setSelectedCierre(null);
    setResumenData(null);
  };

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: 'ARS',
      minimumFractionDigits: 0
    }).format(val);
  };

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString('es-AR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit'
    });
  };

  const filteredCierres = cierres.filter(c => 
    c.cajaNombre.toLowerCase().includes(searchTerm.toLowerCase()) ||
    c.usuarioCierreNombre.toLowerCase().includes(searchTerm.toLowerCase()) ||
    c.fecha.includes(searchTerm)
  );

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Cierres Diarios</h3>
          <p className="text-xs text-pearl-400">
            Historial de cierres diarios de caja consolidados
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <DateRangeFilter
            desde={desde}
            hasta={hasta}
            onDesdeChange={setDesde}
            onHastaChange={setHasta}
          />
          <div className="relative">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-pearl-400" />
            <input
              type="text"
              placeholder="Buscar por caja, usuario..."
              value={searchTerm}
              onChange={e => setSearchTerm(e.target.value)}
              className="pl-9 h-9 w-60 text-sm bg-white border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400"
            />
          </div>
          <button
            onClick={loadCierres}
            className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 bg-white rounded-lg hover:bg-ice-50 active:scale-[0.98] transition-all cursor-pointer"
            title="Refrescar"
          >
            <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      {loading && cierres.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <Calendar size={40} className="animate-pulse mb-3" />
          <p className="text-sm">Cargando cierres diarios...</p>
        </div>
      ) : (
        <div className="bg-white border border-pearl-100 rounded-2xl overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-pearl-700">
              <thead>
                <tr className="bg-ice-50 border-b border-pearl-100 text-pearl-500 font-semibold text-xs uppercase tracking-wider">
                  <th className="py-4.5 px-6">Fecha Cierre</th>
                  <th className="py-4.5 px-6">Caja</th>
                  <th className="py-4.5 px-6">Usuario Cierre</th>
                  <th className="py-4.5 px-6 text-right">Total Ventas</th>
                  <th className="py-4.5 px-6 text-right">Total Egresos</th>
                  <th className="py-4.5 px-6 text-right">Neto Caja</th>
                  <th className="py-4.5 px-6 text-center">Acciones</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-pearl-50 text-sm">
                {filteredCierres.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-8 text-center text-pearl-400">
                      No se encontraron registros de cierres diarios.
                    </td>
                  </tr>
                ) : (
                  filteredCierres.map(c => (
                    <tr key={c.id} className="hover:bg-ice-50/50 transition-colors">
                      <td className="py-4 px-6 font-medium text-pearl-900">{formatDate(c.fecha)}</td>
                      <td className="py-4 px-6">{c.cajaNombre}</td>
                      <td className="py-4 px-6">
                        <div className="flex items-center gap-1.5">
                          <User size={14} className="text-pearl-400" />
                          <span>{c.usuarioCierreNombre}</span>
                        </div>
                      </td>
                      <td className="py-4 px-6 text-right text-success-600 font-semibold">{formatCurrency(c.totalVentas)}</td>
                      <td className="py-4 px-6 text-right text-danger-600 font-semibold">{formatCurrency(c.totalEgresos)}</td>
                      <td className="py-4 px-6 text-right text-pearl-900 font-bold">{formatCurrency(c.totalNeto)}</td>
                      <td className="py-4 px-6 text-center">
                        <button 
                          onClick={() => handleVerDetalle(c)}
                          className="inline-flex items-center gap-1 px-3 py-1.5 border border-pearl-200 hover:border-brand-500 hover:text-brand-600 rounded-lg text-xs font-semibold cursor-pointer transition-all"
                        >
                          <Eye size={14} /> Detalle
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Modal Detalle Cierre Diario */}
      {selectedCierre && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={handleCerrarDetalle}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-4xl mx-4 p-6 animate-fade-in max-h-[90vh] flex flex-col overflow-hidden" onClick={e => e.stopPropagation()}>
            {/* Header */}
            <div className="flex items-center justify-between pb-4 border-b border-pearl-100 flex-shrink-0">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
                  <FileText size={20} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-pearl-900">
                    Cierre Diario Consolidado
                  </h4>
                  <p className="text-[11px] text-pearl-400">
                    Caja: {selectedCierre.cajaNombre} | Fecha: {formatDate(selectedCierre.fecha)}
                  </p>
                </div>
              </div>
              <button onClick={handleCerrarDetalle} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            {loadingDetail ? (
              <div className="flex-1 py-20 flex flex-col items-center justify-center gap-3 text-pearl-400">
                <RefreshCw size={32} className="animate-spin" />
                <p className="text-sm">Cargando detalles consolidados...</p>
              </div>
            ) : (
              <div className="flex-1 overflow-y-auto py-5 space-y-6 pr-1">
                {/* Indicadores clave */}
                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                  <div className="bg-ice-50 rounded-xl p-4 text-center">
                    <span className="text-[10px] font-bold text-pearl-400 uppercase tracking-widest block">Total Ventas</span>
                    <span className="text-xl font-extrabold text-success-600 block mt-1">{formatCurrency(selectedCierre.totalVentas)}</span>
                  </div>
                  <div className="bg-ice-50 rounded-xl p-4 text-center">
                    <span className="text-[10px] font-bold text-pearl-400 uppercase tracking-widest block">Total Egresos</span>
                    <span className="text-xl font-extrabold text-danger-600 block mt-1">{formatCurrency(selectedCierre.totalEgresos)}</span>
                  </div>
                  <div className="bg-ice-50 rounded-xl p-4 text-center">
                    <span className="text-[10px] font-bold text-pearl-400 uppercase tracking-widest block">Neto en Caja</span>
                    <span className="text-xl font-extrabold text-pearl-900 block mt-1">{formatCurrency(selectedCierre.totalNeto)}</span>
                  </div>
                  <div className="bg-ice-50 rounded-xl p-4 text-center">
                    <span className="text-[10px] font-bold text-pearl-400 uppercase tracking-widest block">Registrado por</span>
                    <span className="text-sm font-bold text-pearl-800 block truncate mt-2.5" title={selectedCierre.usuarioCierreNombre}>
                      {selectedCierre.usuarioCierreNombre}
                    </span>
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Desglose por Medio de Pago */}
                  <div className="border border-pearl-100 rounded-xl p-4 space-y-3">
                    <h5 className="text-xs font-bold text-pearl-500 uppercase tracking-wider border-b border-pearl-50 pb-2">
                      Ventas por Medio de Pago
                    </h5>
                    <div className="space-y-2 max-h-[200px] overflow-y-auto">
                      {!resumenData || resumenData.desglosePorMetodo.length === 0 ? (
                        <p className="text-xs text-pearl-400 text-center py-6">No hay registros de ventas.</p>
                      ) : (
                        resumenData.desglosePorMetodo.map(m => (
                          <div key={m.metodoPagoId} className="flex justify-between items-center p-2 rounded bg-ice-50 text-xs">
                            <div>
                              <span className="font-bold text-pearl-900 block">{m.metodoPagoNombre}</span>
                              <span className="text-[9px] text-pearl-400">{m.cantidadOperaciones} transacciones</span>
                            </div>
                            <span className="font-bold text-pearl-900">{formatCurrency(m.total)}</span>
                          </div>
                        ))
                      )}
                    </div>
                  </div>

                  {/* Detalle de Turnos del día */}
                  <div className="border border-pearl-100 rounded-xl p-4 space-y-3">
                    <h5 className="text-xs font-bold text-pearl-500 uppercase tracking-wider border-b border-pearl-50 pb-2">
                      Turnos Operados
                    </h5>
                    <div className="space-y-2 max-h-[200px] overflow-y-auto">
                      {!resumenData || resumenData.turnos.length === 0 ? (
                        <p className="text-xs text-pearl-400 text-center py-6">No hay turnos registrados.</p>
                      ) : (
                        resumenData.turnos.map(t => (
                          <div key={t.turnoId} className="p-2.5 rounded bg-ice-50 text-xs space-y-1">
                            <div className="flex justify-between items-center font-bold text-pearl-900">
                              <span className="flex items-center gap-1">
                                <User size={12} className="text-pearl-400" />
                                {t.usuarioNombre}
                              </span>
                              <span className={t.diferenciaArqueo < 0 ? 'text-danger-600' : 'text-success-600'}>
                                Arqueo: {t.diferenciaArqueo === 0 ? '$0' : formatCurrency(t.diferenciaArqueo)}
                              </span>
                            </div>
                            <div className="flex justify-between text-[10px] text-pearl-400">
                              <span>Fondo inicial: {formatCurrency(t.fondoInicial)}</span>
                              <span className="flex items-center gap-0.5">
                                <Clock size={10} />
                                {t.fechaCierre ? 'Cerrado' : 'Abierto'}
                              </span>
                            </div>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                </div>

                {/* Observaciones */}
                <div className="bg-pearl-50/50 border border-pearl-100 rounded-xl p-4">
                  <span className="text-xs font-semibold text-pearl-500 uppercase tracking-wider block mb-1">
                    Observaciones de Cierre Diario
                  </span>
                  <p className="text-sm text-pearl-800 italic leading-relaxed">
                    {selectedCierre.observaciones || 'Sin observaciones registradas.'}
                  </p>
                </div>
              </div>
            )}

            {/* Footer buttons */}
            <div className="border-t border-pearl-100 pt-4 flex justify-end flex-shrink-0">
              <button 
                onClick={handleCerrarDetalle}
                className="h-10 px-5 text-sm font-medium text-pearl-600 bg-pearl-100 hover:bg-pearl-200 rounded-lg cursor-pointer transition-colors"
              >
                Cerrar Detalle
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
