import { useState, useEffect } from 'react';
import {
  ArrowLeft,
  Lock,
  CheckCircle2,
  DollarSign,
  AlertCircle,
  Calendar,
  TrendingUp,
  Wallet,
  TrendingDown,
  MessageSquare,
  LogOut,
  Receipt
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/contexts/AppContext';
import { useCaja, type ResultadoCierreTurnoPOS } from '@/contexts/CajaContext';
import api from '@/services/api';

function formatARS(monto: number): string {
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 0 }).format(monto);
}

export default function CierreCajaScreen() {
  const navigate = useNavigate();
  const { logout } = useAuth();
  const { turnoActivo, cargando, cerrarTurno, cierreDiario } = useCaja();

  const [step, setStep] = useState<'arqueo' | 'resumen-turno' | 'cierre-diario' | 'confirmado-diario'>('arqueo');

  // Turno states
  const [efectivoDeclarado, setEfectivoDeclarado] = useState('');
  const [observaciones, setObservaciones] = useState('');
  const [resultadoTurno, setResultadoTurno] = useState<ResultadoCierreTurnoPOS | null>(null);
  const [resumenTurno, setResumenTurno] = useState<any | null>(null);
  const [loadingResumenTurno, setLoadingResumenTurno] = useState(false);
  const [showMesasAbiertasModal, setShowMesasAbiertasModal] = useState(false);
  const [mesasAbiertasCount, setMesasAbiertasCount] = useState(0);

  // Diario states
  const [resumenDia, setResumenDia] = useState<any | null>(null);
  const [loadingResumen, setLoadingResumen] = useState(false);
  const [observacionesDiario, setObservacionesDiario] = useState('');

  // General states
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  // Cargar resumen parcial del turno activo al iniciar
  useEffect(() => {
    if (turnoActivo) {
      const fetchResumenTurno = async () => {
        setLoadingResumenTurno(true);
        try {
          const res = await api.get(`/caja/resumen-turno/${turnoActivo.turnoId}`);
          setResumenTurno(res.data);
        } catch (err) {
          console.error('Error al obtener resumen parcial del turno:', err);
        } finally {
          setLoadingResumenTurno(false);
        }
      };
      fetchResumenTurno();
    }
  }, [turnoActivo]);

  // Cargar resumen consolidado al ir al paso de cierre diario
  useEffect(() => {
    if (step === 'cierre-diario') {
      const fetchResumen = async () => {
        setLoadingResumen(true);
        setErrorMsg('');
        try {
          const res = await api.get('/caja/resumen-dia');
          setResumenDia(res.data);
        } catch (err: any) {
          console.error('Error al obtener resumen de caja:', err);
          setErrorMsg('No se pudo cargar el resumen diario consolidado.');
        } finally {
          setLoadingResumen(false);
        }
      };
      fetchResumen();
    }
  }, [step]);

  const handleCerrarTurnoClick = async (transferirMesas?: boolean) => {
    if (!turnoActivo) return;
    const cash = parseFloat(efectivoDeclarado);
    if (isNaN(cash) || cash < 0) {
      setErrorMsg('Debe ingresar un monto declarado válido (mayor o igual a cero).');
      return;
    }

    setSubmitting(true);
    setErrorMsg('');
    try {
      const res = await cerrarTurno(cash, observaciones, transferirMesas);
      setResultadoTurno(res);
      setShowMesasAbiertasModal(false);
      setStep('resumen-turno');
    } catch (err: any) {
      console.error('Error al cerrar turno:', err);
      const data = err?.response?.data;
      if (data && (data.error === 'MesasAbiertas' || data.error === 'MesasAbiertasPM')) {
        setMesasAbiertasCount(data.count || 0);
        setShowMesasAbiertasModal(true);
      } else {
        const msg = data?.message || err.message || 'Error de conexión';
        setErrorMsg(`Error al cerrar el turno: ${msg}`);
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleCierreDiario = async () => {
    setSubmitting(true);
    setErrorMsg('');
    try {
      const ok = await cierreDiario(observacionesDiario);
      if (ok) {
        setStep('confirmado-diario');
      } else {
        setErrorMsg('El servidor rechazó el cierre diario. Verifique que no haya turnos abiertos.');
      }
    } catch (err: any) {
      console.error('Error al realizar cierre diario:', err);
      const msg = err?.response?.data?.message || err.message || 'Error de conexión';
      setErrorMsg(`Error al realizar cierre diario: ${msg}`);
    } finally {
      setSubmitting(false);
    }
  };

  const handleFinalizar = () => {
    logout();
    navigate('/login', { replace: true });
  };

  if (cargando) {
    return (
      <div className="h-screen w-screen flex flex-col items-center justify-center bg-slate-950 text-text-primary">
        <div className="w-10 h-10 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
        <p className="mt-4 text-sm font-semibold text-text-secondary">Cargando...</p>
      </div>
    );
  }

  if (!turnoActivo && step === 'arqueo') {
    return (
      <div className="h-screen w-screen flex flex-col items-center justify-center bg-slate-950 text-text-primary px-4 text-center">
        <div className="w-14 h-14 rounded-[var(--radius-btn)] bg-slate-850 border border-border-default flex items-center justify-center mb-6">
          <Lock className="w-6 h-6 text-amber-500" />
        </div>
        <h3 className="text-lg font-bold text-text-primary">No hay turno activo</h3>
        <p className="text-sm text-text-secondary mt-2 max-w-sm">
          No hay una caja abierta en este momento, por lo que no es posible realizar un cierre.
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
    <div className="h-screen w-screen flex flex-col bg-slate-950 overflow-hidden text-text-primary">
      <header className="flex items-center justify-between px-5 py-4 bg-surface-base border-b border-border-default flex-shrink-0">
        <div className="flex items-center gap-4">
          {step === 'arqueo' && (
            <button onClick={() => navigate('/')} className="touch-btn p-3 rounded-[var(--radius-btn)] bg-slate-900 text-text-secondary hover:text-text-primary hover:bg-slate-850 border border-border-default">
              <ArrowLeft className="w-5 h-5" />
            </button>
          )}
          <div>
            <h1 className="text-lg font-bold text-text-primary flex items-center gap-2">
              <Lock className="w-5 h-5 text-amber-500" />
              {step === 'arqueo' && 'Arqueo de Turno (Caja Ciega)'}
              {step === 'resumen-turno' && 'Resumen del Turno Cerrado'}
              {step === 'cierre-diario' && 'Cierre Diario Consolidado'}
              {step === 'confirmado-diario' && 'Jornada Finalizada'}
            </h1>
            <p className="text-sm text-text-muted">
              {step === 'arqueo' && 'Declare el efectivo exacto disponible en la caja física.'}
              {step === 'resumen-turno' && 'Resultados de conciliación y ventas por método.'}
              {step === 'cierre-diario' && 'Consolidación total de la caja operativa del día.'}
              {step === 'confirmado-diario' && 'El sistema ha sido cerrado y bloqueado.'}
            </p>
          </div>
        </div>

        {turnoActivo && (
          <div className="hidden sm:flex items-center gap-3 text-xs bg-slate-900 border border-border-default px-3 py-2 rounded-[var(--radius-btn)]">
            <span className="text-text-muted">Caja: <strong className="text-text-primary">{turnoActivo.cajaNombre}</strong></span>
            <span className="w-px h-3 bg-border-default" />
            <span className="text-text-muted">Operador: <strong className="text-text-primary">{turnoActivo.usuarioNombre}</strong></span>
          </div>
        )}
      </header>

      <main className="flex-1 overflow-y-auto p-6 flex justify-center items-start md:items-center">
        {step === 'arqueo' && (
          <div className="w-full max-w-4xl bg-slate-900 border border-border-default rounded-[var(--radius-card)] p-8 shadow-[var(--shadow-modal)] flex flex-col gap-6 animate-scale-in">
            {/* Header info de fecha contable y turno en arqueo */}
            <div className="flex justify-between items-center border-b border-border-default pb-4">
              <div>
                <h2 className="text-lg font-bold text-text-primary">Arqueo de Turno</h2>
                <p className="text-sm text-text-muted">Fecha Contable: {turnoActivo && new Date(turnoActivo.fechaContable).toLocaleDateString('es-AR')} | Turno: {turnoActivo?.turno}</p>
              </div>
              <span className="px-2.5 py-1 rounded-[3px] text-xs font-semibold uppercase tracking-wider bg-slate-850 text-text-secondary border border-border-default">
                {turnoActivo?.turno}
              </span>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
              {/* Left Column: Resumen Parcial del Turno */}
              <div className="bg-slate-950 border border-border-default rounded-[var(--radius-card)] p-6 flex flex-col justify-between gap-4">
                <div>
                  <h3 className="text-sm font-bold text-text-secondary uppercase tracking-wider flex items-center gap-2 border-b border-border-subtle pb-2 mb-4">
                    <Wallet className="w-4 h-4 text-amber-500" />
                    Resumen Parcial de Caja
                  </h3>
                  {loadingResumenTurno ? (
                    <div className="py-8 flex flex-col items-center justify-center gap-2">
                      <div className="w-6 h-6 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
                      <span className="text-xs text-text-muted">Cargando resumen...</span>
                    </div>
                  ) : resumenTurno ? (
                    <div className="space-y-3 text-sm">
                      <div className="flex justify-between">
                        <span className="text-text-muted">Fondo Inicial:</span>
                        <span className="font-bold text-text-primary">{formatARS(resumenTurno.fondoInicial)}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-text-muted">Ventas Declaradas:</span>
                        <span className="font-bold text-success-400">+ {formatARS(resumenTurno.totalVentas)}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-text-muted">Ingresos (+) :</span>
                        <span className="font-bold text-success-400">+ {formatARS(resumenTurno.totalIngresos)}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-text-muted">Egresos (-) :</span>
                        <span className="font-bold text-danger-400">- {formatARS(resumenTurno.totalEgresos)}</span>
                      </div>
                      <div className="h-px bg-border-subtle my-2" />
                      <div className="flex justify-between text-base border-t border-border-subtle pt-2">
                        <span className="text-text-secondary font-medium">Efectivo Esperado:</span>
                        <span className="font-bold text-text-primary text-lg">{formatARS(resumenTurno.montoEsperadoEfectivo)}</span>
                      </div>

                      {/* Desglose por método de pago rápido */}
                      <div className="mt-4 pt-3 border-t border-border-subtle">
                        <span className="text-[10px] font-bold text-text-muted uppercase tracking-widest block mb-2">Detalle por Método:</span>
                        <div className="space-y-1.5 max-h-[140px] overflow-y-auto no-scrollbar">
                          {resumenTurno.desglosePorMetodo.map((m: any) => (
                            <div key={m.metodoPagoId} className="flex justify-between text-xs py-1 px-2 rounded bg-slate-900 border border-border-subtle">
                              <span className="text-text-muted">{m.metodoPagoNombre}</span>
                              <span className="font-bold text-text-secondary">{formatARS(m.total)}</span>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  ) : (
                    <p className="text-xs text-text-muted">No se pudo cargar el resumen parcial.</p>
                  )}
                </div>
              </div>

              {/* Right Column: Arqueo Form */}
              <div className="flex flex-col gap-6">
                <div className="bg-amber-500/5 border border-amber-500/10 rounded-[var(--radius-card)] p-4 flex gap-3">
                  <AlertCircle className="w-5 h-5 text-amber-500 flex-shrink-0 mt-0.5" />
                  <p className="text-amber-500 text-xs font-medium leading-snug">
                    Ingrese la suma física de efectivo que posee en el cajón para contrastarlo contra el efectivo esperado de {resumenTurno ? formatARS(resumenTurno.montoEsperadoEfectivo) : '$0'}.
                  </p>
                </div>

                <div className="flex flex-col gap-2">
                  <label className="text-xs font-semibold text-text-secondary uppercase tracking-wider">Efectivo Físico Contado ($)</label>
                  <div className="relative flex items-center">
                    <DollarSign className="absolute left-4 text-amber-500 w-5 h-5 stroke-[2.5]" />
                    <input
                      type="number"
                      value={efectivoDeclarado}
                      onChange={e => setEfectivoDeclarado(e.target.value)}
                      className="w-full bg-slate-950/60 border border-border-strong rounded-[var(--radius-input)] p-4 pl-12 text-2xl font-bold text-text-primary focus:outline-none focus:border-amber-500/50 text-center"
                      placeholder="0"
                    />
                  </div>
                </div>

                <div className="flex flex-col gap-2">
                  <label className="text-xs font-semibold text-text-secondary uppercase tracking-wider flex items-center gap-1.5">
                    <MessageSquare className="w-4 h-4 text-text-muted" />
                    Observaciones (Opcional)
                  </label>
                  <textarea
                    rows={2}
                    value={observaciones}
                    onChange={e => setObservaciones(e.target.value)}
                    className="w-full bg-slate-950/60 border border-border-strong rounded-[var(--radius-input)] p-3 text-sm font-medium text-text-primary focus:outline-none focus:border-amber-500/50 resize-none"
                    placeholder="Indicar discrepancias..."
                  />
                </div>

                {errorMsg && (
                  <div className="py-2.5 px-3 bg-danger-500/5 border border-danger-500/20 rounded-[var(--radius-btn)] text-center animate-fade-in">
                    <span className="text-xs font-semibold text-danger-400">{errorMsg}</span>
                  </div>
                )}

                <button
                  onClick={() => handleCerrarTurnoClick()}
                  disabled={efectivoDeclarado === '' || submitting}
                  className={`touch-btn w-full flex items-center justify-center gap-2 py-4 rounded-[var(--radius-btn)] text-base font-bold transition-all duration-200
                    ${efectivoDeclarado !== '' && !submitting
                      ? 'bg-danger-600 text-white hover:bg-danger-500 shadow-sm active:scale-[0.98]'
                      : 'bg-slate-900 text-text-muted cursor-not-allowed border border-border-default'
                    }
                  `}
                >
                  {submitting ? (
                    <>
                      <div className="w-4 h-4 border-2 border-white/60 border-t-transparent rounded-full animate-spin" />
                      Procesando...
                    </>
                  ) : (
                    <>
                      <Lock className="w-4 h-4" />
                      Cerrar Turno
                    </>
                  )}
                </button>
              </div>
            </div>
          </div>
        )}

        {step === 'resumen-turno' && resultadoTurno && (
          <div className="w-full max-w-4xl bg-slate-900 border border-border-default rounded-[var(--radius-card)] p-8 shadow-[var(--shadow-modal)] flex flex-col gap-6 animate-scale-in">
            <div className="text-center pb-4 border-b border-border-default">
              <div className="w-12 h-12 rounded-full bg-success-500/10 border border-success-500/25 flex items-center justify-center mx-auto mb-3">
                <CheckCircle2 className="w-6 h-6 text-success-500" />
              </div>
              <h2 className="text-xl font-bold text-text-primary">Turno Cerrado con Éxito</h2>
              <p className="text-text-secondary text-sm mt-1">
                Fecha Cierre: {new Date(resultadoTurno.fechaCierre).toLocaleString('es-AR')}
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {/* Caja Física Conciliación */}
              <div className="bg-slate-950 border border-border-default rounded-[var(--radius-card)] p-5 flex flex-col justify-between gap-4">
                <h3 className="text-sm font-bold text-text-secondary uppercase tracking-wider flex items-center gap-2 border-b border-border-subtle pb-2">
                  <Wallet className="w-4 h-4 text-amber-500" />
                  Arqueo de Efectivo Físico
                </h3>
                <div className="space-y-2 text-sm">
                  <div className="flex justify-between">
                    <span className="text-text-muted">Fondo Inicial:</span>
                    <span className="font-bold text-text-primary">{formatARS(resultadoTurno.fondoInicial)}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-text-muted">Ventas Efectivo:</span>
                    <span className="font-bold text-success-400">+ {formatARS(resultadoTurno.desglosePorMetodo.find(m => m.metodoPagoNombre.toLowerCase().includes('efectivo'))?.total || 0)}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-text-muted">Ingresos de Caja (+):</span>
                    <span className="font-bold text-success-400">+ {formatARS(resultadoTurno.totalIngresos)}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-text-muted">Egresos de Caja (-):</span>
                    <span className="font-bold text-danger-400">- {formatARS(resultadoTurno.totalEgresos)}</span>
                  </div>
                  <div className="h-px bg-border-subtle my-2" />
                  <div className="flex justify-between text-base">
                    <span className="text-text-secondary font-medium">Efectivo Esperado:</span>
                    <span className="font-bold text-text-primary">{formatARS(resultadoTurno.montoEsperadoEfectivo)}</span>
                  </div>
                  <div className="flex justify-between text-base">
                    <span className="text-text-secondary font-medium">Efectivo Declarado:</span>
                    <span className="font-bold text-text-primary">{formatARS(resultadoTurno.montoDeclarado)}</span>
                  </div>
                </div>

                {/* Diferencia */}
                <div className={`p-4 rounded-[var(--radius-btn)] border flex items-center gap-3 ${
                  resultadoTurno.diferenciaArqueo < 0
                    ? 'bg-danger-500/5 border-danger-500/25 text-danger-400'
                    : resultadoTurno.diferenciaArqueo > 0
                    ? 'bg-info-500/5 border-info-500/25 text-info-400'
                    : 'bg-success-500/5 border-success-500/25 text-success-400'
                }`}>
                  <AlertCircle className="w-5 h-5 flex-shrink-0" />
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider block">Diferencia de Arqueo</span>
                    <span className="text-base font-bold block mt-0.5">
                      {resultadoTurno.diferenciaArqueo === 0
                        ? 'Caja Cuadrada ($0)'
                        : resultadoTurno.diferenciaArqueo < 0
                        ? `${formatARS(resultadoTurno.diferenciaArqueo)} (Faltante)`
                        : `+ ${formatARS(resultadoTurno.diferenciaArqueo)} (Sobrante)`
                      }
                    </span>
                  </div>
                </div>
              </div>

              {/* Ventas Consolidadas por Método */}
              <div className="bg-slate-950 border border-border-default rounded-[var(--radius-card)] p-5 flex flex-col gap-4">
                <h3 className="text-sm font-bold text-text-secondary uppercase tracking-wider flex items-center gap-2 border-b border-border-subtle pb-2">
                  <Receipt className="w-4 h-4 text-amber-500" />
                  Resumen de Ventas por Método
                </h3>
                <div className="flex-1 overflow-y-auto space-y-2 pr-1 max-h-[220px]">
                  {resultadoTurno.desglosePorMetodo.length === 0 ? (
                    <div className="text-center text-text-muted text-xs py-10">
                      No se registraron ventas en este turno.
                    </div>
                  ) : (
                    resultadoTurno.desglosePorMetodo.map(m => (
                      <div key={m.metodoPagoId} className="flex justify-between items-center p-2 rounded-[var(--radius-btn)] bg-slate-900 border border-border-subtle">
                        <div>
                          <span className="text-sm font-bold text-text-primary block">{m.metodoPagoNombre}</span>
                          <span className="text-[10px] text-text-muted">{m.cantidadOperaciones} {m.cantidadOperaciones === 1 ? 'operación' : 'operaciones'}</span>
                        </div>
                        <span className="text-base font-bold text-text-primary">{formatARS(m.total)}</span>
                      </div>
                    ))
                  )}
                </div>
                <div className="border-t border-border-subtle pt-3 flex justify-between items-center">
                  <span className="text-sm font-bold text-text-secondary">Ventas Totales del Turno:</span>
                  <span className="text-lg font-bold text-text-primary">{formatARS(resultadoTurno.totalVentas)}</span>
                </div>
              </div>
            </div>

            {/* Acciones */}
            <div className="mt-4 pt-4 border-t border-border-default flex flex-col sm:flex-row gap-3">
              {resultadoTurno.esUltimoTurnoDia ? (
                <div className="w-full flex flex-col gap-4">
                  <div className="p-4 bg-amber-500/5 border border-amber-500/20 rounded-[var(--radius-card)] flex gap-3 text-amber-500 text-sm">
                    <AlertCircle className="w-5 h-5 flex-shrink-0 mt-0.5" />
                    <div>
                      <strong className="block text-sm font-bold">¡Caja Diaria Completa!</strong>
                      Este ha sido el último turno activo de la jornada de hoy. Debe proceder a realizar el Cierre Diario General para consolidar todas las operaciones e informar a administración.
                    </div>
                  </div>
                  <div className="flex gap-3 justify-end">
                    <button
                      onClick={handleFinalizar}
                      className="touch-btn px-6 py-3.5 rounded-[var(--radius-btn)] bg-slate-900 text-sm font-bold text-text-secondary hover:text-text-primary hover:bg-slate-850 active:scale-95 transition-all cursor-pointer border border-border-default"
                    >
                      Salir a Login (Hacer Cierre Diario más tarde)
                    </button>
                    <button
                      onClick={() => setStep('cierre-diario')}
                      className="touch-btn px-8 py-3.5 rounded-[var(--radius-btn)] bg-amber-500 text-sm font-bold text-slate-950 hover:bg-amber-400 active:scale-95 transition-all cursor-pointer shadow-sm"
                    >
                      Proceder a Cierre Diario
                    </button>
                  </div>
                </div>
              ) : (
                <div className="w-full flex flex-col gap-4">
                  <div className="p-4 bg-info-500/5 border border-info-500/20 rounded-[var(--radius-card)] flex gap-3 text-info-400 text-sm">
                    <AlertCircle className="w-5 h-5 flex-shrink-0 mt-0.5" />
                    <div>
                      El turno de caja se ha cerrado correctamente. Aún quedan otros turnos abiertos o pendientes de operación para esta caja hoy. Puede retirarse con tranquilidad.
                    </div>
                  </div>
                  <div className="flex justify-end">
                    <button
                      onClick={handleFinalizar}
                      className="touch-btn px-8 py-3.5 rounded-[var(--radius-btn)] bg-slate-900 text-sm font-bold text-text-primary hover:bg-slate-850 active:scale-95 transition-all cursor-pointer border border-border-strong flex items-center gap-2"
                    >
                      <LogOut className="w-4 h-4 text-text-muted" />
                      Terminar y Salir
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {step === 'cierre-diario' && (
          <div className="w-full max-w-3xl bg-slate-900 border border-border-default rounded-[var(--radius-card)] p-8 shadow-[var(--shadow-modal)] flex flex-col gap-6 animate-scale-in">
            <div className="text-center pb-4 border-b border-border-default">
              <div className="w-12 h-12 rounded-full bg-amber-500/10 border border-amber-500/25 flex items-center justify-center mx-auto mb-3">
                <Calendar className="w-6 h-6 text-amber-500" />
              </div>
              <h2 className="text-xl font-bold text-text-primary">Cierre Diario General de Caja</h2>
              <p className="text-text-secondary text-sm mt-1">
                Esta acción consolidará los turnos operativos registrados hoy en el sistema.
              </p>
            </div>

            {loadingResumen ? (
              <div className="py-12 flex flex-col items-center justify-center gap-3">
                <div className="w-8 h-8 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
                <span className="text-sm text-text-secondary">Generando resumen consolidado del día...</span>
              </div>
            ) : errorMsg ? (
              <div className="py-6 px-4 bg-danger-500/5 border border-danger-500/20 rounded-[var(--radius-card)] text-center space-y-4">
                <p className="text-sm font-semibold text-danger-400">{errorMsg}</p>
                <button
                  onClick={() => setStep('resumen-turno')}
                  className="touch-btn px-4 py-2 bg-slate-900 hover:bg-slate-850 border border-border-default rounded-[var(--radius-btn)] text-xs font-bold text-text-primary"
                >
                  Volver al Resumen del Turno
                </button>
              </div>
            ) : resumenDia ? (
              <div className="space-y-6">
                {/* Indicadores Clave del Día */}
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  <div className="bg-slate-950 border border-border-default p-4 rounded-[var(--radius-card)] flex flex-col gap-1">
                    <span className="text-[10px] font-bold text-text-muted uppercase tracking-widest block">Total Turnos hoy</span>
                    <span className="text-xl font-bold text-text-primary block mt-1">{resumenDia.totalTurnos} cerrados</span>
                  </div>
                  <div className="bg-slate-950 border border-border-default p-4 rounded-[var(--radius-card)] flex flex-col gap-1">
                    <span className="text-[10px] font-bold text-text-muted uppercase tracking-widest block">Ventas Consolidadas</span>
                    <span className="text-xl font-bold text-success-400 flex items-center gap-1.5 mt-1">
                      <TrendingUp className="w-4 h-4 flex-shrink-0" />
                      {formatARS(resumenDia.totalVentas)}
                    </span>
                  </div>
                  <div className="bg-slate-950 border border-border-default p-4 rounded-[var(--radius-card)] flex flex-col gap-1">
                    <span className="text-[10px] font-bold text-text-muted uppercase tracking-widest block">Egresos Consolidados</span>
                    <span className="text-xl font-bold text-danger-400 flex items-center gap-1.5 mt-1">
                      <TrendingDown className="w-4 h-4 flex-shrink-0" />
                      {formatARS(resumenDia.totalEgresos)}
                    </span>
                  </div>
                </div>

                <div className="p-4 bg-slate-950 border border-border-subtle rounded-[var(--radius-card)]">
                  <div className="flex justify-between items-center text-base font-bold">
                    <span className="text-text-secondary">Ingreso Neto de Caja:</span>
                    <span className="text-text-primary">{formatARS(resumenDia.totalVentas - resumenDia.totalEgresos)}</span>
                  </div>
                </div>

                {/* Comentarios del Cierre Diario */}
                <div className="flex flex-col gap-2">
                  <label className="text-sm font-semibold text-text-secondary uppercase tracking-wider flex items-center gap-1.5">
                    <MessageSquare className="w-4 h-4 text-text-muted" />
                    Observaciones Generales de la Jornada (Opcional)
                  </label>
                  <textarea
                    rows={3}
                    value={observacionesDiario}
                    onChange={e => setObservacionesDiario(e.target.value)}
                    className="w-full bg-slate-950/60 border border-border-strong rounded-[var(--radius-input)] p-4 text-sm font-medium text-text-primary focus:outline-none focus:border-amber-500/50 resize-none"
                    placeholder="Escriba aquí comentarios generales para administración o novedades de la fecha..."
                  />
                </div>

                {/* Botón de Cierre General */}
                <div className="flex flex-col sm:flex-row gap-3 pt-4 border-t border-border-default">
                  <button
                    onClick={() => setStep('resumen-turno')}
                    className="touch-btn flex-1 py-3.5 rounded-[var(--radius-btn)] bg-slate-900 text-sm font-bold text-text-secondary hover:text-text-primary hover:bg-slate-850 active:scale-95 transition-all cursor-pointer border border-border-default text-center"
                  >
                    Atrás
                  </button>

                  <button
                    onClick={handleCierreDiario}
                    disabled={submitting}
                    className="touch-btn flex-[1.5] py-3.5 rounded-[var(--radius-btn)] bg-amber-500 text-sm font-bold text-slate-950 hover:bg-amber-400 active:scale-95 disabled:opacity-50 transition-all cursor-pointer flex items-center justify-center gap-2 shadow-sm"
                  >
                    {submitting ? (
                      <>
                        <div className="w-4 h-4 border-2 border-slate-950/60 border-t-transparent rounded-full animate-spin" />
                        Ejecutando Cierre...
                      </>
                    ) : (
                      <>
                        <CheckCircle2 className="w-4 h-4 stroke-[2.5]" />
                        Confirmar Cierre Diario General
                      </>
                    )}
                  </button>
                </div>
              </div>
            ) : null}
          </div>
        )}

        {step === 'confirmado-diario' && (
          <div className="w-full max-w-xl bg-slate-900 border border-border-default rounded-[var(--radius-card)] p-8 shadow-[var(--shadow-modal)] flex flex-col gap-6 text-center animate-scale-in justify-center items-center py-12">
            <div className="w-16 h-16 rounded-full bg-success-500/10 border border-success-500/25 flex items-center justify-center mb-4">
              <CheckCircle2 className="w-8 h-8 text-success-500" />
            </div>

            <h2 className="text-2xl font-bold text-text-primary">Día Cerrado Exitosamente</h2>
            <p className="text-text-secondary text-base max-w-md">
              La jornada de caja diaria se ha consolidado y guardado en la base de datos local.
              Los reportes consolidados se enviarán automáticamente a la nube en el próximo ciclo de sincronización.
            </p>

            <div className="p-4 bg-slate-950 border border-border-default rounded-[var(--radius-card)] w-full text-left flex gap-3 text-sm text-text-muted my-2">
              <AlertCircle className="w-5 h-5 text-amber-500 flex-shrink-0 mt-0.5" />
              <span>
                El punto de venta permanecerá inhabilitado para facturación hasta la apertura del turno del día de mañana por un encargado.
              </span>
            </div>

            <button
              onClick={handleFinalizar}
              className="touch-btn mt-6 w-full py-4 rounded-[var(--radius-btn)] bg-amber-500 hover:bg-amber-400 text-slate-950 font-bold text-base transition-all active:scale-[0.98] shadow-sm cursor-pointer flex items-center justify-center gap-2"
            >
              <LogOut className="w-4 h-4" />
              Salir al Inicio de Sesión
            </button>
          </div>
        )}
      </main>

      {/* Modal Salvaguarda Mesas Abiertas */}
      {showMesasAbiertasModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm animate-modal-backdrop">
          <div className="bg-slate-900 border border-border-strong w-full max-w-md rounded-[var(--radius-card)] p-6 shadow-[var(--shadow-modal)] flex flex-col gap-5 animate-modal-content">
            <div className="flex flex-col items-center gap-4 text-center">
              <div className="w-12 h-12 rounded-[var(--radius-btn)] bg-amber-500/10 border border-amber-500/25 flex items-center justify-center">
                <AlertCircle className="w-6 h-6 text-amber-500" />
              </div>
              <div>
                <h3 className="text-base font-bold text-text-primary">Mesas Abiertas Pendientes</h3>
                <p className="text-sm text-text-secondary mt-1">
                  Hay <strong className="text-text-primary">{mesasAbiertasCount} mesa(s) abiertas</strong> en este turno.
                  {turnoActivo?.turno === 'PM'
                    ? ' Para realizar el cierre de turno PM (cierre diario), es obligatorio que todas las mesas y órdenes estén cerradas.'
                    : ' Debe resolverlas antes de cerrar la caja o transferirlas al siguiente turno.'
                  }
                </p>
              </div>
            </div>

            <div className="flex flex-col gap-2 pt-2 border-t border-border-default">
              {turnoActivo?.turno !== 'PM' && (
                <button
                  onClick={() => handleCerrarTurnoClick(true)}
                  disabled={submitting}
                  className="touch-btn w-full py-3.5 rounded-[var(--radius-btn)] bg-amber-500 hover:bg-amber-400 text-slate-950 text-sm font-bold transition-all cursor-pointer text-center active:scale-95 shadow-sm disabled:opacity-50"
                >
                  {submitting ? 'Transfiriendo y Cerrando...' : 'Transferir Mesas al Siguiente Turno'}
                </button>
              )}
              <button
                onClick={() => {
                  setShowMesasAbiertasModal(false);
                  navigate('/');
                }}
                className="touch-btn w-full py-3 rounded-[var(--radius-btn)] bg-slate-900 hover:bg-slate-850 text-text-primary text-sm font-bold border border-border-default cursor-pointer text-center active:scale-95"
              >
                Retornar al Salón (Resolver manualmente)
              </button>
              <button
                onClick={() => setShowMesasAbiertasModal(false)}
                className="w-full py-3 text-text-muted hover:text-text-secondary text-xs font-semibold cursor-pointer text-center"
              >
                Cancelar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
