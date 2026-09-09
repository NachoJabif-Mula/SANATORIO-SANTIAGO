import { useState, useEffect } from 'react';
import {
  Wallet,
  RefreshCw,
  Eye,
  X,
  Search,
  ArrowUpCircle,
  ArrowDownCircle
} from 'lucide-react';
import api from '@/services/api';

interface Cliente {
  id: string;
  nombre: string;
  apellido: string;
  telefono?: string | null;
  email?: string | null;
  limiteCredito: number;
  saldoActual: number;
  isActive: boolean;
}

interface MovimientoCuentaCorriente {
  id: string;
  tipo: 'Cargo' | 'Pago';
  monto: number;
  detalle: string;
  comandaId?: string | null;
  fecha: string;
}

export default function CuentasCorrientesPage() {
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');

  const [selectedCliente, setSelectedCliente] = useState<Cliente | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [movimientos, setMovimientos] = useState<MovimientoCuentaCorriente[]>([]);
  const [saldoDetalle, setSaldoDetalle] = useState<number>(0);

  const loadClientes = async () => {
    setLoading(true);
    try {
      const res = await api.get('/cliente');
      const ordenados = (res.data as Cliente[]).sort((a, b) => b.saldoActual - a.saldoActual);
      setClientes(ordenados);
    } catch (err) {
      console.error('Error al cargar clientes:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadClientes();
  }, []);

  const handleVerDetalle = async (cliente: Cliente) => {
    setSelectedCliente(cliente);
    setLoadingDetail(true);
    setMovimientos([]);
    try {
      const res = await api.get(`/cuentacorriente/${cliente.id}/movimientos`);
      setSaldoDetalle(res.data.saldoActual);
      setMovimientos(res.data.movimientos || []);
    } catch (err) {
      console.error('Error al cargar movimientos:', err);
      alert('No se pudo cargar el historial de movimientos de este cliente.');
      setSelectedCliente(null);
    } finally {
      setLoadingDetail(false);
    }
  };

  const handleCerrarDetalle = () => {
    setSelectedCliente(null);
    setMovimientos([]);
  };

  const formatCurrency = (val: number) =>
    new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(val);

  const formatDateTime = (dateStr: string) =>
    new Date(dateStr).toLocaleString('es-AR', {
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit'
    });

  const filteredClientes = clientes.filter(c =>
    `${c.nombre} ${c.apellido}`.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Cuentas Corrientes</h3>
          <p className="text-xs text-pearl-400">
            Reporte de saldos y movimientos de crédito de clientes
          </p>
        </div>
        <div className="flex gap-2">
          <div className="relative">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-pearl-400" />
            <input
              type="text"
              placeholder="Buscar cliente..."
              value={searchTerm}
              onChange={e => setSearchTerm(e.target.value)}
              className="pl-9 h-9 w-60 text-sm bg-white border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400"
            />
          </div>
          <button
            onClick={loadClientes}
            className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 bg-white rounded-lg hover:bg-ice-50 active:scale-[0.98] transition-all cursor-pointer"
            title="Refrescar"
          >
            <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      {loading && clientes.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <Wallet size={40} className="animate-pulse mb-3" />
          <p className="text-sm">Cargando cuentas corrientes...</p>
        </div>
      ) : (
        <div className="bg-white border border-pearl-100 rounded-2xl overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-pearl-700">
              <thead>
                <tr className="bg-ice-50 border-b border-pearl-100 text-pearl-500 font-semibold text-xs uppercase tracking-wider">
                  <th className="py-4.5 px-6">Cliente</th>
                  <th className="py-4.5 px-6">Contacto</th>
                  <th className="py-4.5 px-6 text-right">Límite de Crédito</th>
                  <th className="py-4.5 px-6 text-right">Saldo Actual</th>
                  <th className="py-4.5 px-6 text-center">Acciones</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-pearl-50 text-sm">
                {filteredClientes.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-8 text-center text-pearl-400">
                      No se encontraron clientes.
                    </td>
                  </tr>
                ) : (
                  filteredClientes.map(c => (
                    <tr key={c.id} className="hover:bg-ice-50/50 transition-colors">
                      <td className="py-4 px-6 font-medium text-pearl-900">{c.nombre} {c.apellido}</td>
                      <td className="py-4 px-6 text-pearl-500 text-xs">
                        {c.telefono || c.email || '—'}
                      </td>
                      <td className="py-4 px-6 text-right text-pearl-700">
                        {c.limiteCredito > 0 ? formatCurrency(c.limiteCredito) : 'Sin límite'}
                      </td>
                      <td className={`py-4 px-6 text-right font-bold ${c.saldoActual > 0 ? 'text-danger-600' : 'text-pearl-900'}`}>
                        {formatCurrency(c.saldoActual)}
                      </td>
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

      {/* Modal Detalle de Movimientos */}
      {selectedCliente && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={handleCerrarDetalle}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-2xl mx-4 p-6 animate-fade-in max-h-[85vh] flex flex-col overflow-hidden" onClick={e => e.stopPropagation()}>
            {/* Header */}
            <div className="flex items-center justify-between pb-4 border-b border-pearl-100 flex-shrink-0">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
                  <Wallet size={20} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-pearl-900">
                    {selectedCliente.nombre} {selectedCliente.apellido}
                  </h4>
                  <p className="text-[11px] text-pearl-400">Historial de movimientos de cuenta corriente</p>
                </div>
              </div>
              <button onClick={handleCerrarDetalle} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            {loadingDetail ? (
              <div className="flex-1 py-20 flex flex-col items-center justify-center gap-3 text-pearl-400">
                <RefreshCw size={32} className="animate-spin" />
                <p className="text-sm">Cargando historial...</p>
              </div>
            ) : (
              <div className="flex-1 overflow-y-auto py-5 space-y-4 pr-1">
                <div className="bg-ice-50 rounded-xl p-4 text-center">
                  <span className="text-[10px] font-bold text-pearl-400 uppercase tracking-widest block">Saldo Actual</span>
                  <span className={`text-2xl font-extrabold block mt-1 ${saldoDetalle > 0 ? 'text-danger-600' : 'text-pearl-900'}`}>
                    {formatCurrency(saldoDetalle)}
                  </span>
                </div>

                <div className="space-y-2">
                  {movimientos.length === 0 ? (
                    <p className="text-sm text-pearl-400 text-center py-8">No hay movimientos registrados para este cliente.</p>
                  ) : (
                    movimientos.map(m => (
                      <div key={m.id} className="flex items-center justify-between p-3 rounded-lg bg-ice-50/60 border border-pearl-50">
                        <div className="flex items-center gap-3">
                          {m.tipo === 'Cargo' ? (
                            <ArrowUpCircle size={20} className="text-danger-500 shrink-0" />
                          ) : (
                            <ArrowDownCircle size={20} className="text-success-500 shrink-0" />
                          )}
                          <div>
                            <p className="text-xs font-semibold text-pearl-900">{m.detalle}</p>
                            <p className="text-[10px] text-pearl-400 mt-0.5">{formatDateTime(m.fecha)}</p>
                          </div>
                        </div>
                        <span className={`text-sm font-bold ${m.tipo === 'Cargo' ? 'text-danger-600' : 'text-success-600'}`}>
                          {m.tipo === 'Cargo' ? '+' : '-'}{formatCurrency(m.monto)}
                        </span>
                      </div>
                    ))
                  )}
                </div>
              </div>
            )}

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
