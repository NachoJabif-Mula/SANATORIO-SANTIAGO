import { useState, useEffect } from 'react';
import {
  TrendingUp,
  DollarSign,
  ShoppingCart,
  Building2,
  ArrowUpRight,
  ArrowDownRight,
  Clock,
  ShieldAlert,
  Loader2,
  RefreshCw,
  Sunrise,
  Sunset,
  Ban,
  CreditCard,
  Trophy
} from 'lucide-react';
import api from '@/services/api';
import { useSucursal } from '@/contexts/SucursalContext';
import DateRangeFilter from '@/common/components/DateRangeFilter';
import { getHoyISO } from '@/common/utils/date';

interface StatCardProps {
  title: string;
  value: string;
  change: number;
  iconName: string;
  color: string;
  invertirColor?: boolean;
}

const iconMap: Record<string, React.ReactNode> = {
  DollarSign: <DollarSign size={20} />,
  ShoppingCart: <ShoppingCart size={20} />,
  TrendingUp: <TrendingUp size={20} />,
  Building2: <Building2 size={20} />,
  Ban: <Ban size={20} />
};

function StatCard({ title, value, change, iconName, color, invertirColor }: StatCardProps) {
  const changeSubio = change >= 0;
  // Para tarjetas donde subir es una mala noticia (ej. anulaciones), el color se invierte;
  // la flecha y el signo "+" siempre reflejan la dirección real del cambio.
  const esBuenaNoticia = invertirColor ? !changeSubio : changeSubio;
  const icon = iconMap[iconName] || <DollarSign size={20} />;

  return (
    <div className="
      bg-white rounded-xl border border-pearl-100 p-5
      hover:shadow-lg hover:-translate-y-0.5
      transition-all duration-300 ease-out
      group cursor-default
    ">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-xs font-medium text-pearl-400 uppercase tracking-wider">{title}</p>
          <p className="text-2xl font-bold text-pearl-900 mt-1">{value}</p>
        </div>
        <div
          className="w-10 h-10 rounded-lg flex items-center justify-center transition-transform duration-300 group-hover:scale-110"
          style={{ backgroundColor: `${color}15`, color }}
        >
          {icon}
        </div>
      </div>
      <div className="flex items-center gap-1 mt-3">
        {changeSubio ? (
          <ArrowUpRight size={14} className={esBuenaNoticia ? 'text-success-500' : 'text-danger-500'} />
        ) : (
          <ArrowDownRight size={14} className={esBuenaNoticia ? 'text-success-500' : 'text-danger-500'} />
        )}
        <span className={`text-xs font-semibold ${esBuenaNoticia ? 'text-success-500' : 'text-danger-500'}`}>
          {changeSubio ? '+' : ''}{change}%
        </span>
        <span className="text-xs text-pearl-400 ml-1">vs. período anterior</span>
      </div>
    </div>
  );
}

interface HeatmapItem {
  hora: string;
  valor: number;
}

interface AuditoriaItem {
  tipo: string;
  comanda: string;
  detalle: string;
  gerente: string;
  hora: string;
}

interface MedioPagoItem {
  medioPago: string;
  monto: number;
  operaciones: number;
}

interface TopProductoItem {
  producto: string;
  cantidad: number;
  monto: number;
}

interface TurnoResumen {
  ventas: number;
  comandas: number;
  porcentaje: number;
}

interface VentasPorTurno {
  am: TurnoResumen;
  pm: TurnoResumen;
  sinTurno: { ventas: number; comandas: number };
  diferencia: number;
  diferenciaPorcentual: number;
}

const formatCurrency = (val: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(val);

export default function DashboardPage() {
  const { selectedSucursalId } = useSucursal();
  const [stats, setStats] = useState<StatCardProps[]>([]);
  const [heatmap, setHeatmap] = useState<HeatmapItem[]>([]);
  const [auditorias, setAuditorias] = useState<AuditoriaItem[]>([]);
  const [mediosPago, setMediosPago] = useState<MedioPagoItem[]>([]);
  const [topProductos, setTopProductos] = useState<TopProductoItem[]>([]);
  const [ventasPorTurno, setVentasPorTurno] = useState<VentasPorTurno | null>(null);
  const [loading, setLoading] = useState(true);
  const [desde, setDesde] = useState(getHoyISO());
  const [hasta, setHasta] = useState(getHoyISO());

  const fetchDashboardData = async () => {
    setLoading(true);
    try {
      const params = {
        sucursalId: selectedSucursalId ?? undefined,
        desde: desde || undefined,
        hasta: hasta || undefined
      };
      const [statsRes, heatmapRes, auditRes, mediosPagoRes, topProductosRes, turnosRes] = await Promise.all([
        api.get('/dashboard/stats', { params }),
        api.get('/dashboard/heatmap', { params }),
        api.get('/dashboard/auditoria', { params }),
        api.get('/dashboard/medios-pago', { params }),
        api.get('/dashboard/top-productos', { params }),
        api.get('/dashboard/turnos', { params })
      ]);

      setStats(statsRes.data);
      setHeatmap(heatmapRes.data);
      setAuditorias(auditRes.data);
      setMediosPago(mediosPagoRes.data);
      setTopProductos(topProductosRes.data);
      setVentasPorTurno(turnosRes.data);
    } catch (err) {
      console.error('Error al cargar datos del dashboard:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchDashboardData();
  }, [selectedSucursalId, desde, hasta]);

  return (
    <div className="space-y-6 animate-fade-in pb-8">
      {/* Refresh and loading state */}
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-xl font-bold text-pearl-900">Panel de Control General</h2>
          <p className="text-xs text-pearl-400">Consolidado en la Nube de ventas y operaciones</p>
        </div>
        <div className="flex items-center gap-2">
          <DateRangeFilter
            desde={desde}
            hasta={hasta}
            onDesdeChange={setDesde}
            onHastaChange={setHasta}
          />
          <button
            onClick={fetchDashboardData}
            disabled={loading}
            className="flex items-center gap-1.5 h-9 px-3 text-xs font-medium border border-pearl-200 text-pearl-600 rounded-lg hover:bg-ice-50 active:scale-[0.98] disabled:opacity-50 transition-all cursor-pointer bg-white"
          >
            <RefreshCw size={13} className={loading ? 'animate-spin' : ''} />
            Refrescar
          </button>
        </div>
      </div>

      {loading && stats.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-32 text-pearl-400">
          <Loader2 size={36} className="animate-spin text-brand-500 mb-3" />
          <p className="text-sm">Calculando métricas del negocio...</p>
        </div>
      ) : (
        <>
          {/* Stats Grid */}
          <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-5 gap-4 stagger-children">
            {stats.map((stat) => (
              <StatCard key={stat.title} {...stat} />
            ))}
          </div>

          {/* Diferencial de Ventas por Turno (AM / PM) */}
          {ventasPorTurno && (
            <div className="bg-white rounded-xl border border-pearl-100 p-5 shadow-sm">
              <div className="flex items-center justify-between mb-6">
                <div className="flex items-center gap-2">
                  <Clock className="text-brand-500" size={20} />
                  <h3 className="text-sm font-semibold text-pearl-900">Diferencial de Ventas por Turno</h3>
                </div>
                <span className="text-[11px] text-pearl-400">
                  {desde === hasta ? desde : `${desde} a ${hasta}`}
                </span>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="rounded-xl border border-amber-100 bg-amber-50/40 p-4">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <div className="w-8 h-8 rounded-lg bg-amber-100 text-amber-600 flex items-center justify-center">
                        <Sunrise size={16} />
                      </div>
                      <span className="text-sm font-bold text-pearl-900">Turno AM</span>
                    </div>
                    <span className="text-xs font-semibold text-pearl-500">{ventasPorTurno.am.comandas} comandas</span>
                  </div>
                  <p className="text-2xl font-extrabold text-pearl-900 mt-3">{formatCurrency(ventasPorTurno.am.ventas)}</p>
                  <div className="w-full h-1.5 bg-ice-200 rounded-full overflow-hidden mt-3">
                    <div className="bg-amber-400 h-full rounded-full transition-all duration-500" style={{ width: `${ventasPorTurno.am.porcentaje}%` }} />
                  </div>
                  <span className="text-[11px] text-pearl-400 mt-1 block">{ventasPorTurno.am.porcentaje}% del total del período</span>
                </div>

                <div className="rounded-xl border border-brand-100 bg-brand-50/40 p-4">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <div className="w-8 h-8 rounded-lg bg-brand-100 text-brand-600 flex items-center justify-center">
                        <Sunset size={16} />
                      </div>
                      <span className="text-sm font-bold text-pearl-900">Turno PM</span>
                    </div>
                    <span className="text-xs font-semibold text-pearl-500">{ventasPorTurno.pm.comandas} comandas</span>
                  </div>
                  <p className="text-2xl font-extrabold text-pearl-900 mt-3">{formatCurrency(ventasPorTurno.pm.ventas)}</p>
                  <div className="w-full h-1.5 bg-ice-200 rounded-full overflow-hidden mt-3">
                    <div className="bg-brand-500 h-full rounded-full transition-all duration-500" style={{ width: `${ventasPorTurno.pm.porcentaje}%` }} />
                  </div>
                  <span className="text-[11px] text-pearl-400 mt-1 block">{ventasPorTurno.pm.porcentaje}% del total del período</span>
                </div>
              </div>

              <div className="flex items-center justify-center gap-2 mt-4 pt-4 border-t border-pearl-100">
                {ventasPorTurno.diferencia >= 0 ? (
                  <ArrowUpRight size={14} className="text-success-500" />
                ) : (
                  <ArrowDownRight size={14} className="text-danger-500" />
                )}
                <span className="text-xs font-medium text-pearl-600">
                  AM {ventasPorTurno.diferencia >= 0 ? 'superó' : 'quedó por debajo de'} PM por{' '}
                  <strong>{formatCurrency(Math.abs(ventasPorTurno.diferencia))}</strong>
                  {' '}({ventasPorTurno.diferenciaPorcentual >= 0 ? '+' : ''}{ventasPorTurno.diferenciaPorcentual}%)
                </span>
              </div>

              {ventasPorTurno.sinTurno.comandas > 0 && (
                <p className="text-[11px] text-pearl-400 text-center mt-2">
                  {ventasPorTurno.sinTurno.comandas} comanda(s) por {formatCurrency(ventasPorTurno.sinTurno.ventas)} sin turno asociado, no incluidas en el diferencial.
                </p>
              )}
            </div>
          )}

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Mapa de Calor: Ventas por franja horaria */}
            <div className="lg:col-span-2 bg-white rounded-xl border border-pearl-100 p-5 shadow-sm">
              <div className="flex items-center gap-2 mb-6">
                <Clock className="text-brand-500" size={20} />
                <h3 className="text-sm font-semibold text-pearl-900">Volumen de Ventas por Franja Horaria</h3>
              </div>
              <div className="flex items-end justify-between h-48 gap-2 mt-4 px-2">
                {heatmap.map((d) => (
                  <div key={d.hora} className="flex flex-col items-center flex-1 gap-2 group">
                    <div className="relative w-full bg-ice-100 rounded-t-md h-full overflow-hidden flex flex-col justify-end">
                      <div
                        className="w-full bg-brand-500 rounded-t-md transition-all duration-500 group-hover:bg-brand-400"
                        style={{ height: `${d.valor}%` }}
                      ></div>
                      {/* Tooltip */}
                      <div className="absolute opacity-0 group-hover:opacity-100 transition-opacity -top-8 left-1/2 -translate-x-1/2 bg-pearl-800 text-white text-[10px] py-1 px-2 rounded whitespace-nowrap z-10 pointer-events-none">
                        {d.valor}% del pico
                      </div>
                    </div>
                    <span className="text-[10px] text-pearl-500 font-medium">{d.hora}</span>
                  </div>
                ))}
              </div>
            </div>

            {/* Auditoría de Caja */}
            <div className="bg-white rounded-xl border border-pearl-100 p-5 shadow-sm">
              <div className="flex items-center gap-2 mb-4">
                <ShieldAlert className="text-danger-500" size={20} />
                <h3 className="text-sm font-semibold text-pearl-900">Auditoría de Caja</h3>
              </div>
              <div className="space-y-3 mt-4">
                {auditorias.length === 0 ? (
                  <div className="flex flex-col items-center justify-center py-12 text-pearl-400 bg-ice-50/50 rounded-xl border border-dashed border-pearl-100">
                    <ShieldAlert size={28} className="text-pearl-300 mb-2" />
                    <p className="text-xs font-medium text-pearl-500">No hay alertas de auditoría registradas</p>
                    <p className="text-[10px] text-pearl-400 mt-1">Las anulaciones y descuentos aparecerán aquí</p>
                  </div>
                ) : (
                  auditorias.map((item, i) => (
                    <div key={i} className="p-3 rounded-lg bg-ice-50 border border-pearl-100 hover:bg-white transition-colors">
                      <div className="flex justify-between items-start mb-1">
                        <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${item.tipo === 'Anulación' ? 'bg-danger-50 text-danger-600' :
                          item.tipo === 'Descuento' ? 'bg-warning-50 text-warning-600' :
                            'bg-pearl-200 text-pearl-700'
                          }`}>
                          {item.tipo}
                        </span>
                        <span className="text-xs font-mono text-pearl-500">{item.comanda}</span>
                      </div>
                      <p className="text-sm text-pearl-800 font-medium mt-1">{item.detalle}</p>
                      <div className="flex justify-between items-center mt-2 text-[11px] text-pearl-400">
                        <span>Autoriza: <strong>{item.gerente}</strong></span>
                        <span>{item.hora}</span>
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {/* Medios de Pago */}
            <div className="bg-white rounded-xl border border-pearl-100 p-5 shadow-sm">
              <div className="flex items-center gap-2 mb-4">
                <CreditCard className="text-brand-500" size={20} />
                <h3 className="text-sm font-semibold text-pearl-900">Medios de Pago</h3>
              </div>
              <div className="space-y-3 mt-4">
                {mediosPago.length === 0 ? (
                  <div className="flex flex-col items-center justify-center py-12 text-pearl-400 bg-ice-50/50 rounded-xl border border-dashed border-pearl-100">
                    <CreditCard size={28} className="text-pearl-300 mb-2" />
                    <p className="text-xs font-medium text-pearl-500">No hay pagos registrados en el período</p>
                  </div>
                ) : (
                  (() => {
                    const totalMediosPago = mediosPago.reduce((sum, m) => sum + m.monto, 0);
                    return mediosPago.map((m) => {
                      const porcentaje = totalMediosPago > 0 ? Math.round((m.monto / totalMediosPago) * 100) : 0;
                      return (
                        <div key={m.medioPago} className="p-3 rounded-lg bg-ice-50 border border-pearl-100">
                          <div className="flex justify-between items-baseline mb-1.5">
                            <span className="text-sm font-semibold text-pearl-900">{m.medioPago}</span>
                            <span className="text-sm font-bold text-pearl-900">{formatCurrency(m.monto)}</span>
                          </div>
                          <div className="w-full h-1.5 bg-ice-200 rounded-full overflow-hidden">
                            <div className="bg-brand-500 h-full rounded-full transition-all duration-500" style={{ width: `${porcentaje}%` }} />
                          </div>
                          <div className="flex justify-between items-center mt-1.5 text-[11px] text-pearl-400">
                            <span>{m.operaciones} operaciones</span>
                            <span>{porcentaje}% del total</span>
                          </div>
                        </div>
                      );
                    });
                  })()
                )}
              </div>
            </div>

            {/* Top 10 Productos Más Vendidos */}
            <div className="bg-white rounded-xl border border-pearl-100 p-5 shadow-sm">
              <div className="flex items-center gap-2 mb-4">
                <Trophy className="text-warning-500" size={20} />
                <h3 className="text-sm font-semibold text-pearl-900">Top 10 Productos Más Vendidos</h3>
              </div>
              <div className="overflow-x-auto mt-4">
                <table className="w-full text-sm text-left">
                  <thead>
                    <tr className="border-b border-pearl-100 text-pearl-500 text-[11px] uppercase tracking-wider">
                      <th className="pb-3 font-medium w-8">#</th>
                      <th className="pb-3 font-medium">Producto</th>
                      <th className="pb-3 font-medium text-right">Cantidad</th>
                      <th className="pb-3 font-medium text-right">Monto</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-pearl-50">
                    {topProductos.length === 0 ? (
                      <tr>
                        <td colSpan={4} className="py-12 text-center text-pearl-400 bg-ice-50/50 rounded-b-xl border border-dashed border-pearl-100">
                          <Trophy size={28} className="text-pearl-300 mb-2 mx-auto" />
                          <p className="text-xs font-medium text-pearl-500">No hay ventas registradas en el período</p>
                        </td>
                      </tr>
                    ) : (
                      topProductos.map((p, i) => (
                        <tr key={p.producto} className="hover:bg-ice-50 transition-colors">
                          <td className="py-3 text-pearl-400 font-mono text-xs">{i + 1}</td>
                          <td className="py-3 font-medium text-pearl-800">{p.producto}</td>
                          <td className="py-3 text-right text-pearl-600 tabular-nums">{p.cantidad}</td>
                          <td className="py-3 text-right text-pearl-900 font-mono font-medium">{formatCurrency(p.monto)}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
