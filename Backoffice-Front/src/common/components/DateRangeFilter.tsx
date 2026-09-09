import { Calendar } from 'lucide-react';

interface DateRangeFilterProps {
  desde: string;
  hasta: string;
  onDesdeChange: (value: string) => void;
  onHastaChange: (value: string) => void;
  onFiltrar?: () => void;
}

export default function DateRangeFilter({ desde, hasta, onDesdeChange, onHastaChange, onFiltrar }: DateRangeFilterProps) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <div className="relative">
        <Calendar className="absolute left-3 top-2.5 h-4 w-4 text-pearl-400" />
        <input
          type="date"
          value={desde}
          onChange={e => onDesdeChange(e.target.value)}
          className="pl-9 h-9 text-sm bg-white border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
        />
      </div>
      <span className="text-xs text-pearl-400">a</span>
      <div className="relative">
        <Calendar className="absolute left-3 top-2.5 h-4 w-4 text-pearl-400" />
        <input
          type="date"
          value={hasta}
          onChange={e => onHastaChange(e.target.value)}
          className="pl-9 h-9 text-sm bg-white border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
        />
      </div>
      {onFiltrar && (
        <button
          onClick={onFiltrar}
          className="h-9 px-4 text-sm font-semibold text-white bg-brand-600 hover:bg-brand-700 rounded-lg transition-colors cursor-pointer"
        >
          Filtrar
        </button>
      )}
    </div>
  );
}
