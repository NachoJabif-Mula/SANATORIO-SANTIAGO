import { useState, useEffect, useMemo } from 'react';
import { ArrowLeft, Wallet, Search, Check, DollarSign } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useCaja } from '@/contexts/CajaContext';
import { ModalAutorizacion } from '@/common/components/POS/ModalAutorizacion';
import type { Cliente, MetodoPago } from '@/common/types';
import api from '@/services/api';

export default function CuentasCorrientesScreen() {
  const navigate = useNavigate();
  const { turnoActivo, cargando } = useCaja();

  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [metodosPago, setMetodosPago] = useState<MetodoPago[]>([]);
  const [loadingDatos, setLoadingDatos] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');

  const [clienteSeleccionado, setClienteSeleccionado] = useState<Cliente | null>(null);
  const [montoText, setMontoText] = useState('');
  const [metodoPagoId, setMetodoPagoId] = useState('');
  const [showAutorizacion, setShowAutorizacion] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [confirmado, setConfirmado] = useState(false);

  const cargarDatos = async () => {
    setLoadingDatos(true);
    try {
      const [clientesRes, metodosRes] = await Promise.all([
        api.get('/cliente'),
        api.get('/catalogo/metodos-pago')
      ]);
      const conSaldo = (clientesRes.data as Cliente[])
        .filter(c => c.saldoActual > 0)
        .sort((a, b) => b.saldoActual - a.saldoActual);
      setClientes(conSaldo);

      const metodosSinCtaCte = (metodosRes.data as MetodoPago[]).filter(m => !m.esCuentaCorriente);
      setMetodosPago(metodosSinCtaCte);
      if (metodosSinCtaCte.length > 0) setMetodoPagoId(metodosSinCtaCte[0].id);
    } catch (err) {
      console.error('Error al cargar cuentas corrientes:', err);
    } finally {
      setLoadingDatos(false);
    }
  };

  useEffect(() => {
    cargarDatos();
  }, []);

  const clientesFiltrados = useMemo(() => {
    if (!searchTerm.trim()) return clientes;
    const q = searchTerm.toLowerCase();
    return clientes.filter(c =>
      c.nombre.toLowerCase().includes(q) || c.apellido.toLowerCase().includes(q)
    );
  }, [searchTerm, clientes]);

  const formatARS = (monto: number) =>
    new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(monto);

  const handleSeleccionarCliente = (cliente: Cliente) => {
    setClienteSeleccionado(cliente);
    setMontoText(String(cliente.saldoActual));
    setErrorMsg('');
    setConfirmado(false);
  };

  const montoValido = () => {
    const monto = parseFloat(montoText) || 0;
    return clienteSeleccionado && monto > 0 && monto <= clienteSeleccionado.saldoActual && !!metodoPagoId;
  };

  const handleRegistrarAbono = () => {
    if (!montoValido()) {
      setErrorMsg('Verifique el monto (debe ser mayor a cero y no superar el saldo) y el método de pago.');
      return;
    }
    setErrorMsg('');
    setShowAutorizacion(true);
  };

  const handleAutorizacionConfirmada = async () => {
    if (!clienteSeleccionado || !turnoActivo) return;
    const monto = parseFloat(montoText) || 0;

    setSubmitting(true);
    setErrorMsg('');
    try {
      await api.post(`/cuentacorriente/${clienteSeleccionado.id}/abonar`, {
        turnoCajaId: turnoActivo.turnoId,
        metodoPagoId,
        monto
      });
      setShowAutorizacion(false);
      setConfirmado(true);
      await cargarDatos();
      setClienteSeleccionado(null);
      setMontoText('');
    } catch (err: any) {
      console.error('Error al registrar abono:', err);
      setShowAutorizacion(false);
      setErrorMsg(err?.response?.data?.message || 'Error al registrar el abono en el servidor.');
    } finally {
      setSubmitting(false);
    }
  };

  if (cargando) {
    return (
      <div className="h-screen w-screen flex flex-col items-center justify-center bg-slate-950 text-text-primary">
        <div className="w-10 h-10 border-2 border-cyan-500 border-t-transparent rounded-full animate-spin" />
        <p className="mt-4 text-sm font-semibold text-text-secondary">Verificando estado de caja...</p>
      </div>
    );
  }

  if (!turnoActivo) {
    return (
      <div className="h-screen w-screen flex flex-col items-center justify-center bg-slate-950 text-text-primary px-4 text-center">
        <div className="w-14 h-14 rounded-[var(--radius-btn)] bg-slate-850 border border-border-default flex items-center justify-center mb-6">
          <Wallet className="w-6 h-6 text-cyan-500" />
        </div>
        <h3 className="text-lg font-bold text-text-primary">Caja Cerrada</h3>
        <p className="text-sm text-text-secondary mt-2 max-w-sm">
          No hay un turno de caja activo. Debe abrir la caja para poder registrar abonos de cuenta corriente.
        </p>
        <button
          onClick={() => navigate('/')}
          className="touch-btn mt-6 flex items-center gap-2 px-6 py-3 rounded-[var(--radius-btn)] bg-slate-900 text-text-secondary hover:text-text-primary hover:bg-slate-850 border border-border-default transition-all font-semibold cursor-pointer active:scale-95"
        >
          <ArrowLeft className="w-4 h-4" />
          Volver al Inicio
        </button>
      </div>
    );
  }

  return (
    <div className="h-screen w-screen flex flex-col bg-slate-950 overflow-hidden">
      <header className="flex items-center gap-4 px-5 py-4 bg-surface-base border-b border-border-default flex-shrink-0">
        <button onClick={() => navigate('/')} className="touch-btn p-3 rounded-[var(--radius-btn)] bg-slate-900 text-text-secondary hover:text-text-primary hover:bg-slate-850 border border-border-default">
          <ArrowLeft className="w-5 h-5" />
        </button>
        <div>
          <h1 className="text-lg font-bold text-text-primary flex items-center gap-2">
            <Wallet className="w-5 h-5 text-cyan-500" />
            Cuentas Corrientes
          </h1>
          <p className="text-sm text-text-muted">Cobranza y liquidación de saldos de clientes — turno de {turnoActivo.usuarioNombre}</p>
        </div>
      </header>

      <main className="flex-1 overflow-hidden flex">
        {/* Panel Izquierdo: Lista de Clientes */}
        <div className="w-full md:w-[420px] border-r border-border-default flex flex-col overflow-hidden">
          <div className="p-4 border-b border-border-default">
            <div className="relative">
              <Search size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-text-muted" />
              <input
                type="text"
                placeholder="Buscar cliente..."
                value={searchTerm}
                onChange={e => setSearchTerm(e.target.value)}
                className="w-full h-11 pl-10 pr-3 bg-slate-950/60 border border-border-default rounded-[var(--radius-input)] text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-cyan-500/40 focus:ring-1 focus:ring-cyan-500/20 transition-all"
              />
            </div>
          </div>

          <div className="flex-1 overflow-y-auto p-3 space-y-2">
            {loadingDatos ? (
              <div className="flex flex-col items-center py-10">
                <div className="w-7 h-7 border-2 border-cyan-500 border-t-transparent rounded-full animate-spin" />
              </div>
            ) : clientesFiltrados.length === 0 ? (
              <div className="text-center py-10 text-sm text-text-muted">
                No hay clientes con saldo pendiente.
              </div>
            ) : (
              clientesFiltrados.map(c => (
                <button
                  key={c.id}
                  onClick={() => handleSeleccionarCliente(c)}
                  className={`touch-btn w-full p-4 rounded-[var(--radius-btn)] text-left border transition-all cursor-pointer active:scale-[0.98] ${
                    clienteSeleccionado?.id === c.id
                      ? 'bg-cyan-500/5 border-cyan-500/50'
                      : 'bg-slate-900/50 border-border-default/60 hover:bg-slate-850 hover:border-border-strong'
                  }`}
                >
                  <div className="font-bold text-sm text-text-primary">{c.nombre} {c.apellido}</div>
                  <div className="text-xs text-text-secondary font-semibold mt-1">Saldo: {formatARS(c.saldoActual)}</div>
                </button>
              ))
            )}
          </div>
        </div>

        {/* Panel Derecho: Abono */}
        <div className="flex-1 overflow-y-auto p-8 flex justify-center items-center">
          {confirmado ? (
            <div className="text-center animate-scale-in">
              <div className="w-16 h-16 rounded-full bg-success-500/10 border border-success-500/25 flex items-center justify-center mx-auto mb-6">
                <Check className="w-8 h-8 text-success-500" />
              </div>
              <h2 className="text-xl font-bold text-text-primary mb-2">Abono Registrado</h2>
              <p className="text-text-secondary text-base">El pago fue aplicado a la cuenta del cliente.</p>
              <button
                onClick={() => setConfirmado(false)}
                className="touch-btn mt-6 px-6 py-3 rounded-[var(--radius-btn)] bg-slate-900 text-text-primary border border-border-default font-semibold cursor-pointer hover:bg-slate-850"
              >
                Registrar otro abono
              </button>
            </div>
          ) : !clienteSeleccionado ? (
            <div className="text-center text-text-muted">
              <Wallet className="w-10 h-10 mx-auto mb-4 opacity-30" />
              <p className="text-sm">Seleccione un cliente de la lista para registrar un abono.</p>
            </div>
          ) : (
            <div className="w-full max-w-lg bg-slate-900 border border-border-default rounded-[var(--radius-card)] p-8 shadow-[var(--shadow-modal)] flex flex-col gap-6">
              <div className="text-center">
                <span className="text-xs font-semibold text-text-muted uppercase tracking-wider">Cliente</span>
                <h2 className="text-xl font-bold text-text-primary mt-1">{clienteSeleccionado.nombre} {clienteSeleccionado.apellido}</h2>
                <p className="text-text-secondary font-semibold mt-1 text-sm">Saldo actual: {formatARS(clienteSeleccionado.saldoActual)}</p>
              </div>

              <div className="flex flex-col gap-2">
                <label className="text-xs font-semibold text-text-secondary uppercase tracking-wider">Monto a Abonar ($)</label>
                <div className="relative">
                  <DollarSign className="absolute left-4 top-1/2 -translate-y-1/2 text-cyan-500 w-5 h-5" />
                  <input
                    type="number"
                    value={montoText}
                    onChange={e => setMontoText(e.target.value)}
                    className="w-full bg-slate-950/60 border border-border-strong rounded-[var(--radius-input)] p-5 pl-11 text-2xl font-bold text-text-primary focus:outline-none focus:border-cyan-500/50"
                    placeholder="0"
                  />
                </div>
              </div>

              <div className="flex flex-col gap-2">
                <label className="text-xs font-semibold text-text-secondary uppercase tracking-wider">Método de Pago</label>
                <select
                  value={metodoPagoId}
                  onChange={e => setMetodoPagoId(e.target.value)}
                  className="w-full bg-slate-950/60 border border-border-strong rounded-[var(--radius-input)] p-3.5 text-sm font-medium text-text-primary focus:outline-none focus:border-cyan-500/50 appearance-none"
                >
                  {metodosPago.map(m => (
                    <option key={m.id} value={m.id}>{m.nombre}</option>
                  ))}
                </select>
              </div>

              {errorMsg && (
                <div className="py-2.5 px-3 bg-danger-500/5 border border-danger-500/20 rounded-[var(--radius-btn)] text-center">
                  <span className="text-xs font-semibold text-danger-400">{errorMsg}</span>
                </div>
              )}

              <button
                onClick={handleRegistrarAbono}
                disabled={!montoValido() || submitting}
                className={`touch-btn w-full flex items-center justify-center gap-2 py-4 rounded-[var(--radius-btn)] text-base font-bold transition-all duration-200 ${
                  montoValido() && !submitting
                    ? 'bg-cyan-600 text-white hover:bg-cyan-500 shadow-sm active:scale-[0.98]'
                    : 'bg-slate-900 text-text-muted cursor-not-allowed border border-border-default'
                }`}
              >
                {submitting ? (
                  <>
                    <div className="w-4 h-4 border-2 border-white/60 border-t-transparent rounded-full animate-spin" />
                    Registrando...
                  </>
                ) : (
                  'Registrar Abono'
                )}
              </button>
            </div>
          )}
        </div>
      </main>

      {showAutorizacion && (
        <ModalAutorizacion
          onConfirmar={handleAutorizacionConfirmada}
          onCancelar={() => setShowAutorizacion(false)}
        />
      )}
    </div>
  );
}
