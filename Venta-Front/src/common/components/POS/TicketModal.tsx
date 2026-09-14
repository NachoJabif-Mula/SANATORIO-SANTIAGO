import { Printer, X } from 'lucide-react';

interface TicketModalProps {
  titulo: string;
  contenido: string;
  onCerrar: () => void;
}

/**
 * Muestra en pantalla el contenido de un ticket que, en modo simulador
 * (Sucursal.ImpresionSimulada activo en el backoffice), no se envió a
 * ninguna impresora física.
 */
export function TicketModal({ titulo, contenido, onCerrar }: TicketModalProps) {
  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center animate-modal-backdrop"
      style={{ backgroundColor: 'rgba(8, 9, 11, 0.65)' }}>
      <div className="w-full max-w-sm mx-4 rounded-[14px] bg-surface-base border border-border-default shadow-modal animate-modal-content flex flex-col max-h-[85vh] overflow-hidden">
        <div className="flex items-center justify-between px-4 py-3 border-b border-border-default flex-shrink-0">
          <div className="flex items-center gap-2">
            <Printer className="w-4 h-4 text-amber-500" />
            <div>
              <h3 className="text-[13px] font-semibold text-text-primary leading-none">{titulo}</h3>
              <span className="mono-label leading-none">Modo simulador — sin impresora física</span>
            </div>
          </div>
          <button onClick={onCerrar}
            className="touch-btn w-8 h-8 rounded-[var(--radius-btn)] bg-surface-overlay text-text-muted hover:text-text-primary border border-border-default flex items-center justify-center min-h-0 min-w-0">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-4">
          <pre className="whitespace-pre-wrap break-words font-mono text-[12.5px] leading-[1.45] text-text-primary bg-white text-black rounded-[6px] p-4 shadow-inner">
            {contenido}
          </pre>
        </div>

        <div className="p-3 border-t border-border-default flex-shrink-0">
          <button onClick={onCerrar}
            className="touch-btn w-full h-11 rounded-[var(--radius-btn)] bg-amber-500 border border-amber-500 text-xs font-semibold text-white hover:bg-amber-600 active:scale-[0.98] cursor-pointer">
            Cerrar
          </button>
        </div>
      </div>
    </div>
  );
}

export default TicketModal;
