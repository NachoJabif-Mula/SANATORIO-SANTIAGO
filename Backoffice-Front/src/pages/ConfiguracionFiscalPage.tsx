import { useState, useEffect, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  FileCheck,
  Upload,
  Key,
  CheckCircle2,
  AlertTriangle,
  XCircle,
  RefreshCw,
  Globe,
  Building2,
  ExternalLink,
  ShieldCheck,
  Download
} from 'lucide-react';
import api from '@/services/api';

type Ambiente = 'Homologacion' | 'Produccion';

interface EstadoFiscalSucursal {
  sucursalId: string;
  nombre: string;
  cuit?: string;
  razonSocial?: string;
  puntoDeVenta: number;
  condicionIva?: number;
  ambiente: Ambiente;
  certificadoCargado: boolean;
  certificadoNombreArchivo?: string;
  certificadoSubject?: string;
  certificadoVence?: string;
  certificadoVencido: boolean;
  certificadoCargadoEn?: string;
  ultimaValidacion?: string;
  ultimaValidacionOk: boolean;
  ultimaValidacionMensaje?: string;
  datosFiscalesCompletos: boolean;
  faltantes: string[];
  solicitudGenerada: boolean;
  csrGeneradoEn?: string;
}

const CONDICIONES_IVA_MAP: Record<number, string> = {
  1: 'Responsable Inscripto',
  4: 'Exento',
  5: 'Consumidor Final',
  6: 'Monotributista'
};

const URLS_AFIP: Record<Ambiente, { wsaa: string; wsfe: string }> = {
  Homologacion: {
    wsaa: 'https://wsaahomo.afip.gov.ar/ws/services/LoginCms',
    wsfe: 'https://wswhomo.afip.gov.ar/wsfev1/service.asmx'
  },
  Produccion: {
    wsaa: 'https://wsaa.afip.gov.ar/ws/services/LoginCms',
    wsfe: 'https://servicios1.afip.gov.ar/wsfev1/service.asmx'
  }
};

const formatearFecha = (valor?: string) =>
  valor ? new Date(valor).toLocaleDateString('es-AR') : '—';

export default function ConfiguracionFiscalPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [estados, setEstados] = useState<EstadoFiscalSucursal[]>([]);
  const [sucursalId, setSucursalId] = useState<string>('');

  const [certificadoFile, setCertificadoFile] = useState<File | null>(null);
  const [certificadoPassword, setCertificadoPassword] = useState('');
  const [subiendoCert, setSubiendoCert] = useState(false);
  const [verificando, setVerificando] = useState(false);
  const [generandoCsr, setGenerandoCsr] = useState(false);
  const [crtFile, setCrtFile] = useState<File | null>(null);
  const [subiendoCrt, setSubiendoCrt] = useState(false);
  const [modoAvanzado, setModoAvanzado] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const seleccionada = useMemo(
    () => estados.find(e => e.sucursalId === sucursalId),
    [estados, sucursalId]
  );

  const cargarEstados = async (mantenerSeleccion = true) => {
    setLoading(true);
    try {
      const res = await api.get<EstadoFiscalSucursal[]>('/configuracion-fiscal');
      const data = res.data || [];
      setEstados(data);
      setSucursalId(prev => (mantenerSeleccion && prev ? prev : data[0]?.sucursalId ?? ''));
    } catch (err) {
      console.error('Error al cargar configuración fiscal:', err);
      setError('No se pudo cargar la configuración fiscal.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    cargarEstados(false);
  }, []);

  // El estado devuelto por cada acción ya viene actualizado: se reemplaza solo esa fila.
  const aplicarEstado = (estado: EstadoFiscalSucursal) =>
    setEstados(prev => prev.map(e => (e.sucursalId === estado.sucursalId ? estado : e)));

  const handleGenerarCsr = async () => {
    if (!seleccionada) return;
    setGenerandoCsr(true);
    setError(null);
    try {
      const res = await api.post(
        `/configuracion-fiscal/${seleccionada.sucursalId}/solicitud`,
        null,
        { responseType: 'blob' }
      );

      const url = URL.createObjectURL(new Blob([res.data]));
      const enlace = document.createElement('a');
      enlace.href = url;
      enlace.download = `solicitud-${seleccionada.nombre.replace(/\s+/g, '-').toLowerCase()}.csr`;
      enlace.click();
      URL.revokeObjectURL(url);

      await cargarEstados();
    } catch (err: any) {
      // El interceptor devuelve un blob incluso en el error, hay que leerlo como texto.
      const texto = err.response?.data instanceof Blob ? await err.response.data.text() : null;
      const mensaje = texto ? JSON.parse(texto).message : null;
      setError(mensaje || 'No se pudo generar la solicitud de certificado.');
    } finally {
      setGenerandoCsr(false);
    }
  };

  const handleSubirCrt = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!seleccionada || !crtFile) return;

    setSubiendoCrt(true);
    setError(null);
    try {
      const formData = new FormData();
      formData.append('file', crtFile);

      const res = await api.post<EstadoFiscalSucursal>(
        `/configuracion-fiscal/${seleccionada.sucursalId}/certificado-emitido`,
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } }
      );
      aplicarEstado(res.data);
      setCrtFile(null);
    } catch (err: any) {
      setError(err.response?.data?.message || 'No se pudo cargar el certificado.');
    } finally {
      setSubiendoCrt(false);
    }
  };

  const handleSubirCertificado = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!seleccionada || !certificadoFile) return;

    setSubiendoCert(true);
    setError(null);
    try {
      const formData = new FormData();
      formData.append('file', certificadoFile);
      formData.append('password', certificadoPassword);

      const res = await api.post<EstadoFiscalSucursal>(
        `/configuracion-fiscal/${seleccionada.sucursalId}/certificado`,
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } }
      );
      aplicarEstado(res.data);
      setCertificadoFile(null);
      setCertificadoPassword('');
    } catch (err: any) {
      setError(err.response?.data?.message || 'No se pudo cargar el certificado.');
    } finally {
      setSubiendoCert(false);
    }
  };

  const handleVerificar = async () => {
    if (!seleccionada) return;
    setVerificando(true);
    setError(null);
    try {
      const res = await api.post<EstadoFiscalSucursal>(
        `/configuracion-fiscal/${seleccionada.sucursalId}/verificar`
      );
      aplicarEstado(res.data);
    } catch (err: any) {
      setError(err.response?.data?.message || 'No se pudo verificar la configuración fiscal.');
    } finally {
      setVerificando(false);
    }
  };

  const handleCambiarAmbiente = async (ambiente: Ambiente) => {
    if (!seleccionada || seleccionada.ambiente === ambiente) return;
    if (
      ambiente === 'Produccion' &&
      !confirm(
        `⚠️ ¿Pasar la sucursal "${seleccionada.nombre}" a PRODUCCIÓN? Los comprobantes que emita serán reales ante ARCA.`
      )
    ) {
      return;
    }

    setError(null);
    try {
      const res = await api.put<EstadoFiscalSucursal>(
        `/configuracion-fiscal/${seleccionada.sucursalId}/ambiente`,
        { ambiente }
      );
      aplicarEstado(res.data);
    } catch (err: any) {
      setError(err.response?.data?.message || 'No se pudo cambiar el ambiente.');
    }
  };

  const ambiente: Ambiente = seleccionada?.ambiente ?? 'Homologacion';

  return (
    <div className="space-y-6 animate-fade-in pb-8">
      {/* Title */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
            <FileCheck size={22} />
          </div>
          <div>
            <h3 className="text-base font-bold text-pearl-900">Configuración Fiscal (ARCA)</h3>
            <p className="text-xs text-pearl-400">
              Certificado digital y ambiente de emisión, propios de cada sucursal
            </p>
          </div>
        </div>
        <button
          onClick={() => cargarEstados()}
          className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 rounded-lg hover:bg-ice-50 transition-colors cursor-pointer"
          title="Refrescar"
        >
          <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
        </button>
      </div>

      {error && (
        <div className="p-3 rounded-lg text-xs font-medium bg-danger-50 text-danger-700 border border-danger-200">
          {error}
        </div>
      )}

      {/* Selector de sucursal */}
      <div className="bg-white rounded-xl border border-pearl-100 p-4 shadow-sm">
        <label className="block text-xs font-medium text-pearl-600 mb-1.5">
          Sucursal a configurar
        </label>
        <select
          value={sucursalId}
          onChange={e => setSucursalId(e.target.value)}
          className="w-full h-10 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 cursor-pointer"
        >
          {estados.length === 0 && <option value="">No hay sucursales registradas</option>}
          {estados.map(e => (
            <option key={e.sucursalId} value={e.sucursalId}>
              {e.nombre} — {e.cuit || 'sin CUIT'} — PV {String(e.puntoDeVenta).padStart(4, '0')}
            </option>
          ))}
        </select>
        {seleccionada && !seleccionada.datosFiscalesCompletos && (
          <p className="mt-2 text-[11px] text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-2.5 py-1.5">
            Faltan datos del emisor: {seleccionada.faltantes.join(', ')}. Completalos en la ficha de la sucursal.
          </p>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* SECCION 1: CERTIFICADO DIGITAL */}
        <div className="bg-white rounded-xl border border-pearl-100 p-5 shadow-sm space-y-4">
          <div className="flex items-center gap-2 pb-3 border-b border-pearl-100">
            <Key size={18} className="text-brand-600" />
            <h4 className="text-sm font-bold text-pearl-900">Certificado Digital</h4>
          </div>

          <div
            className={`p-3 rounded-lg flex items-center gap-3 text-xs font-medium ${
              seleccionada?.ultimaValidacionOk
                ? 'bg-success-50 text-success-700 border border-success-200'
                : seleccionada?.certificadoCargado
                  ? 'bg-danger-50 text-danger-700 border border-danger-200'
                  : 'bg-ice-50 text-pearl-600 border border-pearl-200'
            }`}
          >
            {seleccionada?.ultimaValidacionOk ? (
              <CheckCircle2 size={18} className="shrink-0 text-success-600" />
            ) : seleccionada?.certificadoCargado ? (
              <XCircle size={18} className="shrink-0 text-danger-600" />
            ) : (
              <AlertTriangle size={18} className="shrink-0 text-amber-500" />
            )}
            <div>
              <p className="font-semibold">
                {!seleccionada?.certificadoCargado
                  ? 'Certificado pendiente de carga'
                  : seleccionada.certificadoVencido
                    ? 'Certificado vencido'
                    : seleccionada.ultimaValidacionOk
                      ? 'Certificado cargado y válido'
                      : 'Certificado con observaciones'}
              </p>
              <p className="text-[11px] opacity-80 mt-0.5">
                {seleccionada?.ultimaValidacionMensaje ||
                  'Seguí los pasos de abajo para obtener el certificado de ARCA.'}
              </p>
              {seleccionada?.certificadoCargado && (
                <p className="text-[11px] opacity-70 mt-1">
                  {seleccionada.certificadoNombreArchivo} · vence {formatearFecha(seleccionada.certificadoVence)}
                </p>
              )}
            </div>
          </div>

          {/* PASO 1 — Solicitud */}
          <div className="space-y-2 pt-1">
            <div className="flex items-center gap-2">
              <span className="w-5 h-5 rounded-full bg-brand-600 text-white text-[10px] font-bold flex items-center justify-center shrink-0">
                1
              </span>
              <p className="text-xs font-semibold text-pearl-800">Generar la solicitud</p>
            </div>
            <p className="text-[11px] text-pearl-500 pl-7">
              Se genera la clave privada de la sucursal (queda guardada acá, cifrada) y se descarga
              el archivo <span className="font-mono">.csr</span> para subir a ARCA.
            </p>
            <div className="pl-7">
              <button
                onClick={handleGenerarCsr}
                disabled={generandoCsr || !seleccionada?.datosFiscalesCompletos}
                className="w-full h-9 text-xs font-semibold bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all cursor-pointer flex items-center justify-center gap-1.5 disabled:opacity-50"
              >
                <Download size={14} />
                {generandoCsr
                  ? 'Generando...'
                  : seleccionada?.solicitudGenerada
                    ? 'Generar una solicitud nueva'
                    : 'Generar y descargar solicitud (.csr)'}
              </button>
              {seleccionada?.solicitudGenerada && (
                <p className="text-[10px] text-pearl-400 mt-1">
                  Última solicitud: {formatearFecha(seleccionada.csrGeneradoEn)}. Generar una nueva
                  invalida el certificado que ARCA hubiera emitido para la anterior.
                </p>
              )}
            </div>
          </div>

          {/* PASO 2 — Portal de ARCA */}
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <span className="w-5 h-5 rounded-full bg-pearl-300 text-white text-[10px] font-bold flex items-center justify-center shrink-0">
                2
              </span>
              <p className="text-xs font-semibold text-pearl-800">Subirla en el portal de ARCA</p>
            </div>
            <p className="text-[11px] text-pearl-500 pl-7">
              Con tu clave fiscal: <span className="font-medium">Administración de Certificados Digitales</span> →
              subís el <span className="font-mono">.csr</span> y descargás el certificado. Después asociá ese
              certificado al Web Service de Facturación Electrónica y dá de alta el punto de venta.
            </p>
          </div>

          {/* PASO 3 — Carga del certificado */}
          <form onSubmit={handleSubirCrt} className="space-y-2">
            <div className="flex items-center gap-2">
              <span className="w-5 h-5 rounded-full bg-brand-600 text-white text-[10px] font-bold flex items-center justify-center shrink-0">
                3
              </span>
              <p className="text-xs font-semibold text-pearl-800">Cargar el certificado emitido</p>
            </div>

            <div className="pl-7 space-y-2">
              <div className="relative border-2 border-dashed border-pearl-200 rounded-xl p-4 text-center hover:border-brand-300 transition-colors bg-ice-50/50">
                <input
                  type="file"
                  accept=".crt,.pem,.cer"
                  onChange={e => setCrtFile(e.target.files?.[0] || null)}
                  className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
                />
                <Upload size={20} className="mx-auto text-pearl-400 mb-1" />
                <p className="text-xs text-pearl-700 font-medium">
                  {crtFile ? crtFile.name : 'Seleccioná el certificado que descargaste de ARCA'}
                </p>
                <p className="text-[10px] text-pearl-400 mt-0.5">Formatos: .crt, .pem, .cer</p>
              </div>

              <button
                type="submit"
                disabled={subiendoCrt || !crtFile || !seleccionada?.solicitudGenerada}
                className="w-full h-9 text-xs font-semibold bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all cursor-pointer flex items-center justify-center gap-1.5 disabled:opacity-50"
              >
                <ShieldCheck size={14} />
                {subiendoCrt ? 'Cargando certificado...' : 'Cargar certificado'}
              </button>
            </div>
          </form>

          {/* Alternativa: PKCS#12 ya armado */}
          <div className="pt-2 border-t border-pearl-100">
            <button
              type="button"
              onClick={() => setModoAvanzado(v => !v)}
              className="text-[11px] text-pearl-500 hover:text-pearl-800 cursor-pointer"
            >
              {modoAvanzado ? '− Ocultar' : '+ Ya tengo un certificado .pfx armado'}
            </button>

            {modoAvanzado && (
              <form onSubmit={handleSubirCertificado} className="space-y-2 pt-3">
                <p className="text-[11px] text-pearl-500">
                  Usá esta opción solo si el certificado y su clave privada te los entregaron ya
                  combinados en un PKCS#12, por ejemplo desde tu contador.
                </p>

                <div className="relative border-2 border-dashed border-pearl-200 rounded-xl p-3 text-center hover:border-brand-300 transition-colors bg-ice-50/50">
                  <input
                    type="file"
                    accept=".pfx,.p12"
                    onChange={e => setCertificadoFile(e.target.files?.[0] || null)}
                    className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
                  />
                  <p className="text-xs text-pearl-700 font-medium">
                    {certificadoFile ? certificadoFile.name : 'Seleccionar archivo .pfx o .p12'}
                  </p>
                </div>

                <input
                  type="password"
                  value={certificadoPassword}
                  onChange={e => setCertificadoPassword(e.target.value)}
                  placeholder="Contraseña del .pfx"
                  className="w-full h-9 px-3 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
                />

                <button
                  type="submit"
                  disabled={subiendoCert || !certificadoFile || !seleccionada}
                  className="w-full h-9 text-xs font-semibold bg-ice-50 text-brand-700 border border-brand-200 rounded-lg hover:bg-brand-50 transition-all cursor-pointer flex items-center justify-center gap-1.5 disabled:opacity-50"
                >
                  {subiendoCert ? 'Validando...' : 'Cargar .pfx'}
                </button>
              </form>
            )}
          </div>
        </div>

        {/* SECCION 2: AMBIENTE */}
        <div className="bg-white rounded-xl border border-pearl-100 p-5 shadow-sm space-y-4">
          <div className="flex items-center gap-2 pb-3 border-b border-pearl-100">
            <Globe size={18} className="text-brand-600" />
            <h4 className="text-sm font-bold text-pearl-900">Ambiente de Emisión</h4>
          </div>

          <div className="flex gap-2 p-1 bg-ice-50 rounded-xl border border-pearl-200">
            <button
              type="button"
              disabled={!seleccionada}
              onClick={() => handleCambiarAmbiente('Homologacion')}
              className={`flex-1 py-2 rounded-lg text-xs font-bold transition-all cursor-pointer disabled:opacity-50 ${
                ambiente === 'Homologacion'
                  ? 'bg-brand-600 text-white shadow-sm'
                  : 'text-pearl-600 hover:text-pearl-900'
              }`}
            >
              🧪 Homologación (Pruebas)
            </button>
            <button
              type="button"
              disabled={!seleccionada}
              onClick={() => handleCambiarAmbiente('Produccion')}
              className={`flex-1 py-2 rounded-lg text-xs font-bold transition-all cursor-pointer disabled:opacity-50 ${
                ambiente === 'Produccion'
                  ? 'bg-amber-600 text-white shadow-sm'
                  : 'text-pearl-600 hover:text-pearl-900'
              }`}
            >
              🚀 Producción (Real)
            </button>
          </div>

          <div className="space-y-2 pt-1 text-xs">
            <div>
              <span className="text-[11px] text-pearl-400 font-medium">URL Autenticación WSAA</span>
              <input
                type="text"
                readOnly
                value={URLS_AFIP[ambiente].wsaa}
                className="w-full h-8 px-2.5 text-[11px] font-mono bg-ice-50 border border-pearl-200 rounded-lg text-pearl-600"
              />
            </div>
            <div>
              <span className="text-[11px] text-pearl-400 font-medium">URL Web Service WSFE v1</span>
              <input
                type="text"
                readOnly
                value={URLS_AFIP[ambiente].wsfe}
                className="w-full h-8 px-2.5 text-[11px] font-mono bg-ice-50 border border-pearl-200 rounded-lg text-pearl-600"
              />
            </div>
          </div>

          <button
            onClick={handleVerificar}
            disabled={verificando || !seleccionada?.certificadoCargado}
            className="w-full h-9 text-xs font-semibold bg-ice-50 text-brand-700 border border-brand-200 rounded-lg hover:bg-brand-50 transition-all cursor-pointer flex items-center justify-center gap-1.5 disabled:opacity-50"
          >
            <RefreshCw size={14} className={verificando ? 'animate-spin' : ''} />
            {verificando ? 'Autenticando contra WSAA...' : 'Probar conexión con ARCA'}
          </button>
          <p className="text-[10px] text-pearl-400 text-center">
            Valida el certificado, los datos del emisor y la autenticación real contra WSAA.
            El Ticket de Acceso obtenido queda vigente unas 12 horas.
          </p>
        </div>
      </div>

      {/* SECCION 3: RESUMEN POR SUCURSAL */}
      <div className="bg-white rounded-xl border border-pearl-100 overflow-hidden shadow-sm">
        <div className="px-5 py-4 border-b border-pearl-100 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Building2 size={18} className="text-brand-600" />
            <h4 className="text-sm font-bold text-pearl-900">Estado Fiscal por Sucursal</h4>
          </div>
          <span className="text-xs text-pearl-400">{estados.length} sucursales</span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-sm text-left">
            <thead>
              <tr className="bg-ice-50 border-b border-pearl-100 text-pearl-500 text-[10px] font-semibold uppercase tracking-wider">
                <th className="px-4 py-3">Sucursal</th>
                <th className="px-4 py-3">CUIT Emisor</th>
                <th className="px-4 py-3">Razón Social</th>
                <th className="px-4 py-3 text-center">Punto de Venta</th>
                <th className="px-4 py-3">Condición IVA</th>
                <th className="px-4 py-3 text-center">Ambiente</th>
                <th className="px-4 py-3 text-center">Certificado</th>
                <th className="px-4 py-3 text-right">Acción</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-pearl-50">
              {estados.map(e => (
                <tr
                  key={e.sucursalId}
                  onClick={() => setSucursalId(e.sucursalId)}
                  className={`hover:bg-ice-50/50 transition-colors cursor-pointer ${
                    e.sucursalId === sucursalId ? 'bg-brand-50/40' : ''
                  }`}
                >
                  <td className="px-4 py-3 font-semibold text-pearl-800">{e.nombre}</td>
                  <td className="px-4 py-3 font-mono text-xs text-pearl-700">{e.cuit || '-'}</td>
                  <td className="px-4 py-3 text-xs text-pearl-700">{e.razonSocial || '-'}</td>
                  <td className="px-4 py-3 text-center font-mono text-xs font-semibold text-brand-600">
                    PV {String(e.puntoDeVenta).padStart(4, '0')}
                  </td>
                  <td className="px-4 py-3 text-xs text-pearl-600">
                    {e.condicionIva ? CONDICIONES_IVA_MAP[e.condicionIva] || 'Configurado' : '-'}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span
                      className={`text-[10px] font-bold px-2.5 py-0.5 rounded-full border ${
                        e.ambiente === 'Produccion'
                          ? 'bg-amber-50 text-amber-700 border-amber-200'
                          : 'bg-ice-50 text-pearl-600 border-pearl-200'
                      }`}
                    >
                      {e.ambiente === 'Produccion' ? '🚀 Producción' : '🧪 Homologación'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span
                      className={`text-[10px] font-bold px-2.5 py-0.5 rounded-full border ${
                        !e.certificadoCargado
                          ? 'bg-amber-50 text-amber-700 border-amber-200'
                          : e.certificadoVencido
                            ? 'bg-danger-50 text-danger-700 border-danger-200'
                            : 'bg-success-50 text-success-700 border-success-200'
                      }`}
                    >
                      {!e.certificadoCargado
                        ? '⚠️ Sin certificado'
                        : e.certificadoVencido
                          ? '❌ Vencido'
                          : `✅ Vence ${formatearFecha(e.certificadoVence)}`}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button
                      onClick={event => {
                        event.stopPropagation();
                        navigate('/sucursales');
                      }}
                      className="text-xs text-brand-600 hover:text-brand-800 font-medium inline-flex items-center gap-1 cursor-pointer"
                    >
                      Editar <ExternalLink size={12} />
                    </button>
                  </td>
                </tr>
              ))}
              {estados.length === 0 && (
                <tr>
                  <td colSpan={8} className="px-4 py-8 text-center text-pearl-400 italic">
                    No hay sucursales registradas.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
