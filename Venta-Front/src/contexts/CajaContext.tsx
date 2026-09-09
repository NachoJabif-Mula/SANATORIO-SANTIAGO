import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import { useAuth } from '@/contexts/AppContext';
import api from '@/services/api';

export interface TurnoCajaPOS {
  turnoId: string;
  cajaId: string;
  cajaNombre: string;
  usuarioId: string;
  usuarioNombre: string;
  fechaApertura: string;
  fondoInicial: number;
  fechaContable: string;
  turno: string;
}

export interface DesglosePorMetodoPOS {
  metodoPagoId: string;
  metodoPagoNombre: string;
  cantidadOperaciones: number;
  total: number;
}

export interface ResultadoCierreTurnoPOS {
  turnoCajaId: string;
  fechaCierre: string;
  fondoInicial: number;
  totalVentas: number;
  totalIngresos: number;
  totalEgresos: number;
  montoEsperadoEfectivo: number;
  montoDeclarado: number;
  diferenciaArqueo: number;
  desglosePorMetodo: DesglosePorMetodoPOS[];
  observaciones?: string;
  esUltimoTurnoDia: boolean;
  turnosAbiertosDia: number;
}

interface CajaContextType {
  turnoActivo: TurnoCajaPOS | null;
  cargando: boolean;
  abrirTurno: (fondoInicial: number) => Promise<boolean>;
  cerrarTurno: (montoDeclarado: number, observaciones?: string, transferirMesasAbiertas?: boolean) => Promise<ResultadoCierreTurnoPOS>;
  cierreDiario: (observaciones?: string) => Promise<boolean>;
  refrescarTurno: () => Promise<void>;
}

const CajaContext = createContext<CajaContextType | null>(null);

export function CajaProvider({ children }: { children: ReactNode }) {
  const { usuario } = useAuth();
  const [turnoActivo, setTurnoActivo] = useState<TurnoCajaPOS | null>(null);
  const [cargando, setCargando] = useState(true);

  const refrescarTurno = useCallback(async () => {
    if (!usuario) {
      setTurnoActivo(null);
      setCargando(false);
      return;
    }
    setCargando(true);
    try {
      const res = await api.get('/caja/turno-activo');
      if (res.status === 200 && res.data) {
        setTurnoActivo(res.data);
      } else {
        setTurnoActivo(null);
      }
    } catch (err) {
      console.error('Error al obtener turno activo:', err);
      setTurnoActivo(null);
    } finally {
      setCargando(false);
    }
  }, [usuario]);

  useEffect(() => {
    refrescarTurno();
  }, [refrescarTurno]);

  const abrirTurno = useCallback(async (fondoInicial: number): Promise<boolean> => {
    if (!usuario) return false;
    try {
      const res = await api.post('/caja/abrir-turno', {
        usuarioId: usuario.id,
        fondoInicial
      });
      if (res.status === 201 && res.data) {
        setTurnoActivo(res.data);
        return true;
      }
      return false;
    } catch (err) {
      console.error('Error al abrir turno de caja:', err);
      return false;
    }
  }, [usuario]);

  const cerrarTurno = useCallback(async (montoDeclarado: number, observaciones?: string, transferirMesasAbiertas?: boolean): Promise<ResultadoCierreTurnoPOS> => {
    if (!turnoActivo) throw new Error('No hay un turno activo para cerrar.');
    try {
      const res = await api.post('/caja/cerrar-turno', {
        turnoCajaId: turnoActivo.turnoId,
        montoDeclarado,
        observaciones,
        transferirMesasAbiertas
      });
      if (res.status === 200 && res.data) {
        setTurnoActivo(null);
        return res.data;
      }
      throw new Error('No se pudo procesar el cierre de turno.');
    } catch (err) {
      console.error('Error al cerrar turno de caja:', err);
      throw err;
    }
  }, [turnoActivo]);

  const cierreDiario = useCallback(async (observaciones?: string): Promise<boolean> => {
    if (!usuario) return false;
    try {
      const res = await api.post('/caja/cierre-diario', {
        usuarioId: usuario.id,
        observaciones
      });
      return res.status === 200;
    } catch (err) {
      console.error('Error al realizar cierre diario:', err);
      throw err;
    }
  }, [usuario]);

  return (
    <CajaContext.Provider value={{ turnoActivo, cargando, abrirTurno, cerrarTurno, cierreDiario, refrescarTurno }}>
      {children}
    </CajaContext.Provider>
  );
}

export function useCaja() {
  const ctx = useContext(CajaContext);
  if (!ctx) throw new Error('useCaja debe usarse dentro de CajaProvider');
  return ctx;
}
