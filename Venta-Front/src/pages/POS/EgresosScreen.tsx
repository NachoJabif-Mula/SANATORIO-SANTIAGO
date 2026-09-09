import { useState } from 'react';
import { ArrowLeft, Wallet, Check } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useCaja } from '@/contexts/CajaContext';
import api from '@/services/api';

const CATEGORIAS_EGRESO = [
  'Pago a Proveedor',
  'Adelanto de Sueldo',
  'Compra Insumos (Supermercado)',
  'Gastos de Mantenimiento',
  'Otros Gastos'
];

export default function EgresosScreen() {
  const navigate = useNavigate();
  const { turnoActivo, cargando } = useCaja();
  const [monto, setMonto] = useState('0');
  const [categoria, setCategoria] = useState(CATEGORIAS_EGRESO[0]);
  const [referencia, setReferencia] = useState('');
  const [confirmado, setConfirmado] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const handleRegistrar = async () => {
    if (!turnoActivo) return;
    const montoNum = Number(monto);
    if (montoNum <= 0) return;

    setSubmitting(true);
    setErrorMsg('');
    try {
      await api.post('/caja/egresos', {
        turnoCajaId: turnoActivo.turnoId,
        monto: montoNum,
        concepto: categoria,
        referenciaComprobante: referencia || null
      });
      setConfirmado(true);
      setTimeout(() => navigate('/'), 2000);
    } catch (err: any) {
      console.error('Error al registrar egreso:', err);
      const msg = err?.response?.data?.message || err.message || 'Error de conexión';
      setErrorMsg(`No se pudo registrar el egreso: ${msg}`);
      setSubmitting(false);
    }
  };

  if (cargando) {
    return (
      <div className="h-screen w-screen flex flex-col items-center justify-center bg-slate-950 text-text-primary">
        <div className="w-10 h-10 border-2 border-amber-500 border-t-transparent rounded-full animate-spin" />
        <p className="mt-4 text-sm font-semibold text-text-secondary">Verificando estado de caja...</p>
      </div>
    );
  }

  if (!turnoActivo) {
    return (
      <div className="h-screen w-screen flex flex-col items-center justify-center bg-slate-950 text-text-primary px-4 text-center">
        <div className="w-14 h-14 rounded-[var(--radius-btn)] bg-slate-850 border border-border-default flex items-center justify-center mb-6">
          <Wallet className="w-6 h-6 text-amber-500" />
        </div>
        <h3 className="text-lg font-bold text-text-primary">Caja Cerrada</h3>
        <p className="text-sm text-text-secondary mt-2 max-w-sm">
          No hay un turno de caja activo. Debe abrir la caja para poder registrar egresos.
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
            <Wallet className="w-5 h-5 text-amber-500" />
            Registro de Egresos
          </h1>
          <p className="text-sm text-text-muted">Retiro de dinero de la caja física del turno de {turnoActivo.usuarioNombre}</p>
        </div>
      </header>

      <main className="flex-1 overflow-y-auto p-6 flex justify-center items-center">
        {confirmado ? (
          <div className="text-center animate-scale-in">
            <div className="w-16 h-16 rounded-full bg-success-500/10 border border-success-500/25 flex items-center justify-center mx-auto mb-6">
              <Check className="w-8 h-8 text-success-500" />
            </div>
            <h2 className="text-xl font-bold text-text-primary mb-2">Egreso Registrado</h2>
            <p className="text-text-secondary text-base">El movimiento ha sido guardado con éxito.</p>
          </div>
        ) : (
          <div className="w-full max-w-2xl bg-slate-900 border border-border-default rounded-[var(--radius-card)] p-8 shadow-[var(--shadow-modal)] flex flex-col gap-6">

            <div className="flex flex-col gap-2">
              <label className="text-xs font-semibold text-text-secondary uppercase tracking-wider">Monto a Retirar ($)</label>
              <input
                type="number"
                value={monto}
                onChange={e => setMonto(e.target.value)}
                className="w-full bg-slate-950/60 border border-border-strong rounded-[var(--radius-input)] p-5 text-3xl font-bold text-text-primary focus:outline-none focus:border-amber-500/50 text-center"
                placeholder="0"
              />
            </div>

            <div className="flex flex-col gap-2">
              <label className="text-xs font-semibold text-text-secondary uppercase tracking-wider">Categoría del Gasto</label>
              <select
                value={categoria}
                onChange={e => setCategoria(e.target.value)}
                className="w-full bg-slate-950/60 border border-border-strong rounded-[var(--radius-input)] p-3.5 text-sm font-medium text-text-primary focus:outline-none focus:border-amber-500/50 appearance-none"
              >
                {CATEGORIAS_EGRESO.map(cat => (
                  <option key={cat} value={cat}>{cat}</option>
                ))}
              </select>
            </div>

            <div className="flex flex-col gap-2">
              <label className="text-xs font-semibold text-text-secondary uppercase tracking-wider">Referencia / Comprobante</label>
              <input
                type="text"
                value={referencia}
                onChange={e => setReferencia(e.target.value)}
                className="w-full bg-slate-950/60 border border-border-strong rounded-[var(--radius-input)] p-3.5 text-sm font-medium text-text-primary focus:outline-none focus:border-amber-500/50 placeholder:text-text-muted"
                placeholder="Ej. Factura A-0012, Remito..."
              />
            </div>

            {errorMsg && (
              <div className="py-2.5 px-3 bg-danger-500/5 border border-danger-500/20 rounded-[var(--radius-btn)] text-center">
                <span className="text-xs font-semibold text-danger-400">{errorMsg}</span>
              </div>
            )}

            <button
              onClick={handleRegistrar}
              disabled={Number(monto) <= 0 || submitting}
              className={`touch-btn mt-2 w-full flex items-center justify-center gap-2 py-4 rounded-[var(--radius-btn)] text-base font-bold transition-all duration-200
                ${Number(monto) > 0 && !submitting
                  ? 'bg-amber-500 text-slate-950 hover:bg-amber-400 shadow-sm active:scale-[0.98]'
                  : 'bg-slate-900 text-text-muted cursor-not-allowed border border-border-default'
                }
              `}
            >
              {submitting ? (
                <>
                  <div className="w-4 h-4 border-2 border-slate-950/60 border-t-transparent rounded-full animate-spin" />
                  Registrando egreso...
                </>
              ) : (
                'Registrar Egreso'
              )}
            </button>
          </div>
        )}
      </main>
    </div>
  );
}
