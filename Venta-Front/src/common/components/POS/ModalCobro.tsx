import { useState, useEffect } from 'react';
import { CreditCard, X, Check, Trash2, Plus, DollarSign } from 'lucide-react';
import type { Cliente, MetodoPago as ClienteMetodoPago } from '@/common/types';
import { ModalCliente } from './ModalCliente';
import { ModalAutorizacion } from './ModalAutorizacion';
import api from '@/services/api';

interface MetodoPago extends ClienteMetodoPago {}

interface PagoItem {
  metodoPagoId: string;
  nombre: string;
  monto: number;
  vuelto: number;
  clienteId?: string | null;
}

interface ModalCobroProps {
  total: number;
  /** Cliente ya asociado a la comanda (cuenta corriente abierta). Si está presente, al elegir
   * "Cuenta Corriente" como medio de pago se usa directamente en vez de pedir seleccionar uno. */
  clienteAsociado?: { id: string; nombreCompleto: string } | null;
  onConfirmar: (pagos: { metodoPagoId: string; monto: number }[]) => Promise<void>;
  onCancelar: () => void;
}

export default function ModalCobro({ total, clienteAsociado, onConfirmar, onCancelar }: ModalCobroProps) {
  const [metodosPago, setMetodosPago] = useState<MetodoPago[]>([]);
  const [loading, setLoading] = useState(true);

  const [pagosAgregados, setPagosAgregados] = useState<PagoItem[]>([]);
  const [metodoSeleccionado, setMetodoSeleccionado] = useState<MetodoPago | null>(null);

  const [montoText, setMontoText] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  // Nuevos estados para cliente y autorización
  const [showModalCliente, setShowModalCliente] = useState(false);
  const [clienteSeleccionado, setClienteSeleccionado] = useState<Cliente | null>(null);
  const [showAutorizacion, setShowAutorizacion] = useState(false);
  const [metodoEnEspera, setMetodoEnEspera] = useState<MetodoPago | null>(null);
  const [autorizacionModo, setAutorizacionModo] = useState<'cliente' | 'cobro' | null>(null);

  // Cargar métodos de pago disponibles
  useEffect(() => {
    const fetchMetodos = async () => {
      try {
        const res = await api.get('/catalogo/metodos-pago');
        setMetodosPago(res.data);
        if (res.data.length > 0) {
          // Pre-seleccionar efectivo por defecto
          const efectivo = res.data.find((m: MetodoPago) => m.nombre.toLowerCase().includes('efectivo'));
          setMetodoSeleccionado(efectivo || res.data[0]);
        }
      } catch (err) {
        console.error('Error al cargar métodos de pago:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchMetodos();
  }, []);

  const totalPagado = pagosAgregados.reduce((sum, p) => sum + p.monto, 0);
  const totalVuelto = pagosAgregados.reduce((sum, p) => sum + p.vuelto, 0);
  const faltante = Math.max(0, total - totalPagado);

  // Inicializar el input con el monto faltante cuando cambia el método seleccionado
  useEffect(() => {
    if (metodoSeleccionado) {
      setMontoText(String(faltante));
    }
  }, [metodoSeleccionado, totalPagado]);

  const handleNumpadPress = (val: string) => {
    setErrorMsg('');
    if (val === 'C') {
      setMontoText('0');
    } else if (val === '←') {
      setMontoText(prev => (prev.length > 1 ? prev.slice(0, -1) : '0'));
    } else {
      setMontoText(prev => {
        if (prev === '0' || prev === '') return val;
        if (prev.length >= 7) return prev; // Límite de dígitos
        return prev + val;
      });
    }
  };

  const handleAgregarPago = () => {
    if (!metodoSeleccionado) return;
    const montoIngresado = parseFloat(montoText) || 0;
    if (montoIngresado <= 0) {
      setErrorMsg('El monto debe ser mayor a cero.');
      return;
    }

    // Si es Cuenta Corriente: si la comanda ya tiene un cliente asociado (cuenta corriente
    // abierta), saltar la selección y autorizar directo; sino, mostrar ModalCliente primero.
    if (metodoSeleccionado.esCuentaCorriente) {
      setMetodoEnEspera(metodoSeleccionado);
      if (clienteAsociado) {
        setAutorizacionModo('cliente');
        setShowAutorizacion(true);
      } else {
        setShowModalCliente(true);
      }
      return;
    }

    let montoRegistrado = montoIngresado;
    let vuelto = 0;

    // Si es Efectivo y paga de más, calcular vuelto y registrar solo el faltante
    const esEfectivo = metodoSeleccionado.nombre.toLowerCase().includes('efectivo');
    if (esEfectivo && montoIngresado > faltante) {
      vuelto = montoIngresado - faltante;
      montoRegistrado = faltante;
    } else if (montoIngresado > faltante) {
      // Para tarjetas u otros métodos, no se permite pagar más del total de la comanda
      setErrorMsg('Solo se permite pagar de más con Efectivo.');
      return;
    }

    // Verificar si ya se agregó este método para acumularlo o agregar nuevo
    const existenteIdx = pagosAgregados.findIndex(p => p.metodoPagoId === metodoSeleccionado.id);
    if (existenteIdx !== -1) {
      const actualizados = [...pagosAgregados];
      actualizados[existenteIdx].monto += montoRegistrado;
      actualizados[existenteIdx].vuelto += vuelto;
      setPagosAgregados(actualizados);
    } else {
      setPagosAgregados(prev => [
        ...prev,
        {
          metodoPagoId: metodoSeleccionado.id,
          nombre: metodoSeleccionado.nombre,
          monto: montoRegistrado,
          vuelto: vuelto
        }
      ]);
    }

    setMontoText(String(faltante - montoRegistrado));
    setErrorMsg('');
  };

  const handleClienteSeleccionado = async (cliente: Cliente) => {
    setClienteSeleccionado(cliente);
    setShowModalCliente(false);
    // Abrir ModalAutorizacion para confirmar carga a cuenta corriente con PIN
    setAutorizacionModo('cliente');
    setShowAutorizacion(true);
  };

  const handleAutorizacionConfirmada = () => {
    if (!metodoEnEspera) return;

    // El cliente puede venir de la selección manual (ModalCliente) o ya estar
    // asociado de antemano si la comanda es una cuenta corriente abierta.
    const clienteFinal = clienteSeleccionado
      ? { id: clienteSeleccionado.id, nombreCompleto: `${clienteSeleccionado.nombre} ${clienteSeleccionado.apellido}` }
      : clienteAsociado;
    if (!clienteFinal) return;

    const montoIngresado = parseFloat(montoText) || 0;
    if (montoIngresado <= 0) {
      setErrorMsg('El monto debe ser mayor a cero.');
      return;
    }

    // Agregar pago a cuenta corriente
    setPagosAgregados(prev => [
      ...prev,
      {
        metodoPagoId: metodoEnEspera.id,
        nombre: `${metodoEnEspera.nombre} - ${clienteFinal.nombreCompleto}`,
        monto: montoIngresado,
        vuelto: 0,
        clienteId: clienteFinal.id
      }
    ]);

    setShowAutorizacion(false);
    setAutorizacionModo(null);
    setMontoText(String(faltante - montoIngresado));
    setMetodoEnEspera(null);
    setClienteSeleccionado(null);
    setErrorMsg('');
  };

  const handleEliminarPago = (idx: number) => {
    setPagosAgregados(prev => prev.filter((_, i) => i !== idx));
  };

  const handleConfirmarCobro = async () => {
    if (totalPagado < total) {
      setErrorMsg('No se ha cubierto el monto total de la comanda.');
      return;
    }

    // Si hay pagos a cuenta corriente, pedir autorización
    const tieneCuentaCorriente = pagosAgregados.some(p =>
      metodosPago.find(m => m.id === p.metodoPagoId)?.esCuentaCorriente
    );

    if (tieneCuentaCorriente) {
      setAutorizacionModo('cobro');
      setShowAutorizacion(true);
      return;
    }

    // Sino, proceder a confirmar
    await procesarCobro();
  };

  const procesarCobro = async () => {
    setSubmitting(true);
    setErrorMsg('');
    try {
      // Enviar lista de pagos al backend (incluir clienteId cuando aplique)
      const pagosPayload = pagosAgregados.map(p => ({
        metodoPagoId: p.metodoPagoId,
        monto: p.monto,
        clienteId: p.clienteId || undefined
      }));
      await onConfirmar(pagosPayload);
      setShowAutorizacion(false);
      setAutorizacionModo(null);
    } catch (err: any) {
      console.error('Error al registrar cobro:', err);
      setErrorMsg(err?.response?.data?.message || 'Error al registrar el cobro en el servidor.');
      setSubmitting(false);
    }
  };

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat('es-AR', {
      style: 'currency',
      currency: 'ARS',
      minimumFractionDigits: 0
    }).format(val);
  };

  if (loading) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-md">
        <div className="flex flex-col items-center gap-3">
          <div className="w-12 h-12 border-4 border-amber-500 border-t-transparent rounded-full animate-spin" />
          <p className="text-sm font-semibold text-text-secondary">Cargando métodos de pago...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center animate-modal-backdrop"
      style={{ backgroundColor: 'rgba(5, 5, 8, 0.8)', backdropFilter: 'blur(20px)' }}>
      <div className="w-full max-w-4xl mx-4 rounded-[var(--radius-card)] bg-slate-900 border border-border-default shadow-modal animate-modal-content flex flex-col md:flex-row overflow-hidden max-h-[90vh]">
        
        {/* Lado Izquierdo: Métodos, pagos agregados, totalizador */}
        <div className="flex-1 p-6 border-r border-border-default flex flex-col overflow-y-auto">
          {/* Header */}
          <div className="flex items-center justify-between mb-5 flex-shrink-0">
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-lg bg-amber-500/5 border border-amber-500/20 flex items-center justify-center">
                <CreditCard className="w-4.5 h-4.5 text-amber-500" />
              </div>
              <h3 className="text-base font-bold text-text-primary">Registrar Cobro</h3>
            </div>
            <button onClick={onCancelar} className="touch-btn p-1.5 rounded-[var(--radius-btn)] bg-slate-950/60 text-text-muted hover:text-text-primary hover:bg-slate-850 border border-border-default/60 transition-all cursor-pointer">
              <X className="w-4 h-4" />
            </button>
          </div>

          {clienteAsociado && (
            <div className="mb-4 py-2 px-3 bg-cyan-500/5 border border-cyan-500/20 rounded-[var(--radius-btn)] flex items-center gap-2 flex-shrink-0">
              <span className="text-[11px] font-semibold text-cyan-400">
                Cuenta Corriente de <strong>{clienteAsociado.nombreCompleto}</strong>
              </span>
            </div>
          )}

          {/* Resumen Total Comanda */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3 bg-slate-950/50 border border-border-default/80 rounded-[var(--radius-card)] p-4 mb-5 flex-shrink-0">
            <div>
              <span className="text-[9px] font-bold text-text-muted uppercase tracking-wider block">Total Comanda</span>
              <span className="text-lg font-bold text-text-primary block mt-0.5 tabular-nums">{formatCurrency(total)}</span>
            </div>
            <div>
              <span className="text-[9px] font-bold text-text-muted uppercase tracking-wider block">Total Recibido</span>
              <span className="text-lg font-bold text-success-500 block mt-0.5 tabular-nums">{formatCurrency(totalPagado)}</span>
            </div>
            <div>
              <span className="text-[9px] font-bold text-text-muted uppercase tracking-wider block">Vuelto</span>
              <span className="text-lg font-bold text-cyan-500 block mt-0.5 tabular-nums">{formatCurrency(totalVuelto)}</span>
            </div>
            <div>
              <span className="text-[9px] font-bold text-text-muted uppercase tracking-wider block">Faltante</span>
              <span className={`text-lg font-bold block mt-0.5 tabular-nums ${faltante > 0 ? 'text-amber-500' : 'text-success-500'}`}>
                {faltante > 0 ? formatCurrency(faltante) : 'Cubierto ✓'}
              </span>
            </div>
          </div>

          {/* Métodos de Pago disponibles */}
          <div className="mb-5 flex-shrink-0">
            <span className="text-[10px] font-bold text-text-muted uppercase tracking-wider block mb-2">1. Seleccione Medio de Pago</span>
            <div className="grid grid-cols-2 gap-2">
              {metodosPago.map(m => (
                <button
                  key={m.id}
                  type="button"
                  onClick={() => setMetodoSeleccionado(m)}
                  className={`py-3 px-4 rounded-[var(--radius-btn)] text-left border font-semibold text-xs transition-all active:scale-[0.98] cursor-pointer flex items-center justify-between min-h-[42px] ${
                    metodoSeleccionado?.id === m.id
                      ? 'bg-amber-500 text-slate-950 border-amber-500 shadow-sm'
                      : 'bg-slate-950/60 text-text-primary border-border-default/60 hover:bg-slate-850 hover:border-border-default'
                  }`}
                >
                  <span className="truncate">{m.nombre}</span>
                  {m.comisionPorcentaje > 0 && (
                    <span className={`text-[9px] font-bold px-1.5 py-0.5 rounded ${
                      metodoSeleccionado?.id === m.id ? 'bg-slate-900 text-amber-500' : 'bg-slate-900 border border-border-default text-text-muted'
                    }`}>
                      +{m.comisionPorcentaje}%
                    </span>
                  )}
                </button>
              ))}
            </div>
          </div>

          {/* Pagos Agregados */}
          <div className="flex-1 flex flex-col min-h-[140px]">
            <span className="text-[10px] font-bold text-text-muted uppercase tracking-wider block mb-2">Pagos Registrados</span>
            <div className="flex-1 bg-slate-950/40 border border-border-default/60 rounded-[var(--radius-card)] p-3 overflow-y-auto space-y-2 max-h-[180px]">
              {pagosAgregados.length === 0 ? (
                <div className="h-full flex items-center justify-center text-text-muted text-xs py-8">
                  No se han registrado pagos para esta comanda.
                </div>
              ) : (
                pagosAgregados.map((p, idx) => (
                  <div key={idx} className="flex items-center justify-between p-2 rounded-[var(--radius-btn)] bg-slate-900/60 border border-border-default/40 animate-fade-in">
                    <div>
                      <span className="text-xs font-bold text-text-primary block">{p.nombre}</span>
                      {p.vuelto > 0 && (
                        <span className="text-[9px] text-cyan-500 font-semibold mt-0.5 block">
                          Recibido: {formatCurrency(p.monto + p.vuelto)} — Vuelto: {formatCurrency(p.vuelto)}
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-bold text-amber-500 tabular-nums">{formatCurrency(p.monto)}</span>
                      <button
                        onClick={() => handleEliminarPago(idx)}
                        className="p-1 rounded text-text-muted hover:text-danger-400 hover:bg-danger-500/5 transition-colors cursor-pointer"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        {/* Lado Derecho: Teclado numérico táctil para ingresar montos */}
        <div className="w-full md:w-[320px] p-6 bg-slate-950/40 flex flex-col justify-between border-t md:border-t-0 md:border-l border-border-default flex-shrink-0">
          <div className="space-y-4">
            <div>
              <span className="text-[10px] font-bold text-text-muted uppercase tracking-wider block mb-2">2. Ingrese Monto Recibido</span>
              <div className="bg-slate-900/80 border border-border-default/80 rounded-[var(--radius-btn)] p-2.5 flex items-center justify-between">
                <DollarSign className="w-4 h-4 text-amber-500" />
                <span className="text-xl font-bold text-text-primary tabular-nums">
                  {montoText || '0'}
                </span>
              </div>
            </div>

            {/* Teclado */}
            <div className="grid grid-cols-3 gap-1.5">
              {['1', '2', '3', '4', '5', '6', '7', '8', '9', 'C', '0', '←'].map(k => (
                <button
                  key={k}
                  type="button"
                  onClick={() => handleNumpadPress(k)}
                  className={`h-11 rounded-[var(--radius-btn)] text-sm font-bold select-none cursor-pointer active:scale-95 transition-all border ${
                    k === 'C'
                      ? 'bg-danger-500/5 text-danger-400 border-danger-500/25 hover:bg-danger-500/10'
                      : k === '←'
                      ? 'bg-slate-900/60 text-text-secondary border-border-default/60 hover:bg-slate-850'
                      : 'bg-slate-900/60 text-text-primary border-border-default/60 hover:bg-slate-850 hover:border-border-strong'
                  }`}
                >
                  {k}
                </button>
              ))}
            </div>

            {/* Botón Agregar Pago */}
            <button
              onClick={handleAgregarPago}
              disabled={!metodoSeleccionado || faltante === 0}
              className="w-full h-11 rounded-[var(--radius-btn)] bg-slate-900 hover:bg-slate-850 border border-border-strong text-text-primary text-xs font-bold active:scale-[0.98] disabled:opacity-30 disabled:cursor-not-allowed transition-all cursor-pointer flex items-center justify-center gap-1.5 shadow-sm"
            >
              <Plus className="w-3.5 h-3.5 text-amber-500" />
              Agregar Pago
            </button>
          </div>

          <div className="mt-4 pt-4 border-t border-border-default/60 space-y-4">
            {errorMsg && (
              <div className="py-2 px-3 bg-danger-500/5 border border-danger-500/20 rounded-md text-center animate-fade-in flex-shrink-0">
                <span className="text-[11px] font-semibold text-danger-400">{errorMsg}</span>
              </div>
            )}

            {/* Confirmar / Cancelar */}
            <div className="flex gap-2">
              <button
                type="button"
                onClick={onCancelar}
                className="flex-1 h-11 rounded-[var(--radius-btn)] bg-slate-900 border border-border-default text-xs font-bold text-text-secondary hover:text-text-primary hover:bg-slate-850 active:scale-[0.98] transition-all cursor-pointer"
              >
                Atrás
              </button>
              <button
                type="button"
                disabled={submitting || totalPagado < total}
                onClick={handleConfirmarCobro}
                className="flex-[1.5] h-11 rounded-[var(--radius-btn)] bg-amber-500 text-xs font-bold text-slate-950 hover:bg-amber-400 active:scale-[0.98] disabled:opacity-30 disabled:cursor-not-allowed transition-all flex items-center justify-center gap-1.5 cursor-pointer shadow-sm"
              >
                {submitting ? (
                  <>
                    <div className="w-3.5 h-3.5 border-2 border-slate-950 border-t-transparent rounded-full animate-spin" />
                    Procesando...
                  </>
                ) : (
                  <>
                    <Check className="w-3.5 h-3.5 stroke-[2.5]" />
                    Confirmar Cobro
                  </>
                )}
              </button>
            </div>
          </div>
        </div>

      </div>

      {/* Modal de Selección de Cliente */}
      {showModalCliente && (
        <ModalCliente
          onSeleccionar={handleClienteSeleccionado}
          onCancelar={() => setShowModalCliente(false)}
        />
      )}

      {/* Modal de Autorización con PIN */}
      {showAutorizacion && (
        <ModalAutorizacion
          onConfirmar={autorizacionModo === 'cliente' ? handleAutorizacionConfirmada : procesarCobro}
          onCancelar={() => {
            setShowAutorizacion(false);
            setAutorizacionModo(null);
            if (autorizacionModo === 'cliente') {
              setMetodoEnEspera(null);
              setClienteSeleccionado(null);
            }
          }}
        />
      )}
    </div>
  );
}
