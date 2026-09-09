import { useState, useEffect } from 'react';
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
  ShieldCheck
} from 'lucide-react';
import api from '@/services/api';

interface SucursalResumen {
  id: string;
  nombre: string;
  cuit?: string;
  razonSocial?: string;
  puntoDeVenta?: number;
  condicionIva?: number;
}

const CONDICIONES_IVA_MAP: Record<number, string> = {
  1: 'Responsable Inscripto',
  4: 'Exento',
  5: 'Consumidor Final',
  6: 'Monotributista'
};

export default function ConfiguracionFiscalPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [sucursales, setSucursales] = useState<SucursalResumen[]>([]);
  
  // Certificado State
  const [certificadoFile, setCertificadoFile] = useState<File | null>(null);
  const [certificadoPassword, setCertificadoPassword] = useState('');
  const [validatingCert, setValidatingCert] = useState(false);
  const [certStatus, setCertStatus] = useState<{ ok?: boolean; message?: string; fechaVencimiento?: string }>({});

  // Ambiente AFIP State
  const [ambiente, setAmbiente] = useState<'Homologacion' | 'Produccion'>('Homologacion');
  const [testingAfip, setTestingAfip] = useState(false);
  const [afipStatus, setAfipStatus] = useState<{ ok?: boolean; message?: string }>({});

  const urlsAfip = {
    Homologacion: {
      wsaa: 'https://wsaahomo.afip.gov.ar/ws/services/LoginCms',
      wsfe: 'https://wswhomo.afip.gov.ar/wsfev1/service.asmx'
    },
    Produccion: {
      wsaa: 'https://wsaa.afip.gov.ar/ws/services/LoginCms',
      wsfe: 'https://servicios1.afip.gov.ar/wsfev1/service.asmx'
    }
  };

  const loadData = async () => {
    setLoading(true);
    try {
      const [sucsRes, statusRes] = await Promise.all([
        api.get('/sucursal'),
        api.get('/configuracion-fiscal/estado').catch(() => ({ data: null }))
      ]);
      setSucursales(sucsRes.data || []);
      if (statusRes.data) {
        if (statusRes.data.ambiente) setAmbiente(statusRes.data.ambiente);
        if (statusRes.data.certificado) setCertStatus(statusRes.data.certificado);
      }
    } catch (err) {
      console.error('Error al cargar configuración fiscal:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleValidarCertificado = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!certificadoFile && !certStatus.ok) {
      alert('Selecciona un archivo de certificado .pfx');
      return;
    }
    setValidatingCert(true);

    try {
      const formData = new FormData();
      if (certificadoFile) formData.append('file', certificadoFile);
      formData.append('password', certificadoPassword);

      const res = await api.post('/configuracion-fiscal/validar-certificado', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      setCertStatus({ ok: true, message: res.data.message || 'Certificado digital válido y verificado.' });
    } catch (err: any) {
      const msg = err.response?.data?.message || 'Error al validar certificado .pfx. Verifica la contraseña.';
      setCertStatus({ ok: false, message: msg });
    } finally {
      setValidatingCert(false);
    }
  };

  const handleTestAfip = async () => {
    setTestingAfip(true);
    try {
      const res = await api.post('/configuracion-fiscal/test-afip', { ambiente });
      setAfipStatus({ ok: true, message: res.data.message || 'Conexión exitosa con AFIP WSAA/WSFE.' });
    } catch (err: any) {
      const msg = err.response?.data?.message || 'Fallo de autenticación con AFIP WSAA.';
      setAfipStatus({ ok: false, message: msg });
    } finally {
      setTestingAfip(false);
    }
  };

  const handleCambiarAmbiente = async (nuevoAmbiente: 'Homologacion' | 'Produccion') => {
    if (nuevoAmbiente === 'Produccion' && !confirm('⚠️ ¿Estás seguro de cambiar a ambiente PRODUCCIÓN? Las facturas emitidas serán reales ante AFIP.')) {
      return;
    }
    setAmbiente(nuevoAmbiente);
    try {
      await api.put('/configuracion-fiscal/ambiente', { ambiente: nuevoAmbiente });
    } catch (err) {
      console.error('Error al actualizar ambiente:', err);
    }
  };

  return (
    <div className="space-y-6 animate-fade-in pb-8">
      {/* Title */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
            <FileCheck size={22} />
          </div>
          <div>
            <h3 className="text-base font-bold text-pearl-900">Configuración Fiscal (AFIP / ARCA)</h3>
            <p className="text-xs text-pearl-400">Gestión de certificado digital, ambiente de emision y estado de sucursales</p>
          </div>
        </div>
        <button 
          onClick={loadData}
          className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 rounded-lg hover:bg-ice-50 transition-colors cursor-pointer"
          title="Refrescar"
        >
          <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* SECCION 1: CERTIFICADO DIGITAL */}
        <div className="bg-white rounded-xl border border-pearl-100 p-5 shadow-sm space-y-4">
          <div className="flex items-center gap-2 pb-3 border-b border-pearl-100">
            <Key size={18} className="text-brand-600" />
            <h4 className="text-sm font-bold text-pearl-900">Certificado Digital (.pfx)</h4>
          </div>

          {/* Status Badge */}
          <div className={`p-3 rounded-lg flex items-center gap-3 text-xs font-medium ${
            certStatus.ok ? 'bg-success-50 text-success-700 border border-success-200' :
            certStatus.ok === false ? 'bg-danger-50 text-danger-700 border border-danger-200' :
            'bg-ice-50 text-pearl-600 border border-pearl-200'
          }`}>
            {certStatus.ok ? <CheckCircle2 size={18} className="shrink-0 text-success-600" /> :
             certStatus.ok === false ? <XCircle size={18} className="shrink-0 text-danger-600" /> :
             <AlertTriangle size={18} className="shrink-0 text-amber-500" />}
            <div>
              <p className="font-semibold">
                {certStatus.ok ? 'Certificado Digital Activo y Válido' :
                 certStatus.ok === false ? 'Certificado Inválido o Error de Clave' :
                 'Certificado Pendiente de Configuración'}
              </p>
              <p className="text-[11px] opacity-80 mt-0.5">{certStatus.message || 'Sube el archivo .pfx provisto por AFIP con su contraseña'}</p>
            </div>
          </div>

          <form onSubmit={handleValidarCertificado} className="space-y-3 pt-1">
            <div>
              <label className="block text-xs font-medium text-pearl-600 mb-1">Archivo de Certificado (.pfx)</label>
              <div className="relative border-2 border-dashed border-pearl-200 rounded-xl p-4 text-center hover:border-brand-300 transition-colors bg-ice-50/50">
                <input
                  type="file"
                  accept=".pfx,.p12"
                  onChange={e => setCertificadoFile(e.target.files?.[0] || null)}
                  className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
                />
                <Upload size={20} className="mx-auto text-pearl-400 mb-1" />
                <p className="text-xs text-pearl-700 font-medium">
                  {certificadoFile ? certificadoFile.name : 'Haz clic para seleccionar o arrastra el archivo .pfx'}
                </p>
                <p className="text-[10px] text-pearl-400 mt-0.5">Formatos soportados: PKCS#12 (.pfx, .p12)</p>
              </div>
            </div>

            <div>
              <label className="block text-xs font-medium text-pearl-600 mb-1">Contraseña del Certificado</label>
              <input
                type="password"
                value={certificadoPassword}
                onChange={e => setCertificadoPassword(e.target.value)}
                placeholder="Ingresa la clave privada del .pfx"
                className="w-full h-9 px-3 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
              />
            </div>

            <button
              type="submit"
              disabled={validatingCert}
              className="w-full h-9 text-xs font-semibold bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all cursor-pointer flex items-center justify-center gap-1.5 disabled:opacity-50"
            >
              <ShieldCheck size={14} />
              {validatingCert ? 'Validando Certificado...' : 'Validar y Guardar Certificado'}
            </button>
          </form>
        </div>

        {/* SECCION 2: AMBIENTE AFIP */}
        <div className="bg-white rounded-xl border border-pearl-100 p-5 shadow-sm space-y-4">
          <div className="flex items-center gap-2 pb-3 border-b border-pearl-100">
            <Globe size={18} className="text-brand-600" />
            <h4 className="text-sm font-bold text-pearl-900">Ambiente de Emisión AFIP</h4>
          </div>

          <div className="flex gap-2 p-1 bg-ice-50 rounded-xl border border-pearl-200">
            <button
              type="button"
              onClick={() => handleCambiarAmbiente('Homologacion')}
              className={`flex-1 py-2 rounded-lg text-xs font-bold transition-all cursor-pointer ${
                ambiente === 'Homologacion'
                  ? 'bg-brand-600 text-white shadow-sm'
                  : 'text-pearl-600 hover:text-pearl-900'
              }`}
            >
              🧪 Homologación (Pruebas)
            </button>
            <button
              type="button"
              onClick={() => handleCambiarAmbiente('Produccion')}
              className={`flex-1 py-2 rounded-lg text-xs font-bold transition-all cursor-pointer ${
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
                value={urlsAfip[ambiente].wsaa}
                className="w-full h-8 px-2.5 text-[11px] font-mono bg-ice-50 border border-pearl-200 rounded-lg text-pearl-600"
              />
            </div>
            <div>
              <span className="text-[11px] text-pearl-400 font-medium">URL Web Service WSFE v1</span>
              <input
                type="text"
                readOnly
                value={urlsAfip[ambiente].wsfe}
                className="w-full h-8 px-2.5 text-[11px] font-mono bg-ice-50 border border-pearl-200 rounded-lg text-pearl-600"
              />
            </div>
          </div>

          {/* AFIP Status Feedback */}
          {afipStatus.message && (
            <div className={`p-3 rounded-lg text-xs font-medium ${
              afipStatus.ok ? 'bg-success-50 text-success-700 border border-success-200' : 'bg-danger-50 text-danger-700 border border-danger-200'
            }`}>
              {afipStatus.ok ? '✅ ' : '❌ '}{afipStatus.message}
            </div>
          )}

          <button
            onClick={handleTestAfip}
            disabled={testingAfip}
            className="w-full h-9 text-xs font-semibold bg-ice-50 text-brand-700 border border-brand-200 rounded-lg hover:bg-brand-50 transition-all cursor-pointer flex items-center justify-center gap-1.5 disabled:opacity-50"
          >
            <RefreshCw size={14} className={testingAfip ? 'animate-spin' : ''} />
            {testingAfip ? 'Probando AFIP...' : `Probar Conexión AFIP (${ambiente})`}
          </button>
        </div>
      </div>

      {/* SECCION 3: RESUMEN SUCURSALES FISCALES */}
      <div className="bg-white rounded-xl border border-pearl-100 overflow-hidden shadow-sm">
        <div className="px-5 py-4 border-b border-pearl-100 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Building2 size={18} className="text-brand-600" />
            <h4 className="text-sm font-bold text-pearl-900">Estado Fiscal por Sucursal</h4>
          </div>
          <span className="text-xs text-pearl-400">{sucursales.length} sucursales configuradas</span>
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
                <th className="px-4 py-3 text-center">Estado Fiscal</th>
                <th className="px-4 py-3 text-right">Acción</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-pearl-50">
              {sucursales.map(s => {
                const tieneCuit = Boolean(s.cuit);
                return (
                  <tr key={s.id} className="hover:bg-ice-50/50 transition-colors">
                    <td className="px-4 py-3 font-semibold text-pearl-800">{s.nombre}</td>
                    <td className="px-4 py-3 font-mono text-xs text-pearl-700">{s.cuit || '-'}</td>
                    <td className="px-4 py-3 text-xs text-pearl-700">{s.razonSocial || '-'}</td>
                    <td className="px-4 py-3 text-center font-mono text-xs font-semibold text-brand-600">
                      PV {String(s.puntoDeVenta || 1).padStart(4, '0')}
                    </td>
                    <td className="px-4 py-3 text-xs text-pearl-600">
                      {s.condicionIva ? CONDICIONES_IVA_MAP[s.condicionIva] || 'Configurado' : '-'}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <span className={`text-[10px] font-bold px-2.5 py-0.5 rounded-full border ${
                        tieneCuit ? 'bg-success-50 text-success-700 border-success-200' : 'bg-amber-50 text-amber-700 border-amber-200'
                      }`}>
                        {tieneCuit ? '✅ Configurada' : '⚠️ Faltan datos'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        onClick={() => navigate('/sucursales')}
                        className="text-xs text-brand-600 hover:text-brand-800 font-medium inline-flex items-center gap-1 cursor-pointer"
                      >
                        Editar <ExternalLink size={12} />
                      </button>
                    </td>
                  </tr>
                );
              })}
              {sucursales.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-4 py-8 text-center text-pearl-400 italic">
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
