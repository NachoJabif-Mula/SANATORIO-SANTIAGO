import { useState, useEffect } from 'react';
import {
  Printer,
  Plus,
  Edit2,
  Trash2,
  X,
  RefreshCw,
  Wifi,
  Usb,
  CheckCircle2,
  FileText,
  Link2,
  Unlink,
  Save,
  ChevronDown,
  ChevronUp,
} from 'lucide-react';
import api from '@/services/api';

// ═══════════════════════════════════════════════════════
// TIPOS
// ═══════════════════════════════════════════════════════

interface Sucursal {
  id: string;
  nombre: string;
}

interface Impresora {
  id: string;
  sucursalId: string;
  nombre: string;
  tipoDispositivo: number; // 0=Comandera, 1=FiscalEpson, 2=FiscalHasar, 3=FiscalMoretti, 4=Electronica
  tipoConexion: 'Red' | 'USB' | number;
  direccion: string;
  puerto: number;
  velocidad: number;
  isActive: boolean;
}

interface TipoTicket {
  id: string;
  codigo: string;
  nombre: string;
  templateContenido: string;
  isActive: boolean;
}

interface ImpresoraTicketTipo {
  id: string;
  impresoraId: string;
  tipoTicketId: string;
  isActive: boolean;
}

// ═══════════════════════════════════════════════════════
// PLACEHOLDERS INFO
// ═══════════════════════════════════════════════════════

const PLACEHOLDERS = [
  { key: '{{NEGOCIO}}', desc: 'Nombre del negocio' },
  { key: '{{SUCURSAL}}', desc: 'Nombre de la sucursal' },
  { key: '{{FECHA}}', desc: 'Fecha actual (dd/MM/yyyy)' },
  { key: '{{HORA}}', desc: 'Hora actual (HH:mm:ss)' },
  { key: '{{COMANDA_ID}}', desc: 'ID corto de la comanda' },
  { key: '{{MESA}}', desc: 'Etiqueta de la mesa' },
  { key: '{{MOZO}}', desc: 'Nombre del mozo/operador' },
  { key: '{{ITEMS}}', desc: 'Lista de ítems del pedido' },
  { key: '{{TOTAL_ITEMS}}', desc: 'Cantidad total de ítems' },
  { key: '{{SUBTOTAL}}', desc: 'Subtotal de la comanda' },
  { key: '{{DESCUENTO}}', desc: 'Descuento aplicado' },
  { key: '{{TOTAL}}', desc: 'Total de la comanda' },
  { key: '{{METODO_PAGO}}', desc: 'Método de pago usado' },
  { key: '{{MONTO_PAGADO}}', desc: 'Monto pagado' },
  { key: '{{CAE}}', desc: 'CAE de AFIP' },
  { key: '{{CAE_VTO}}', desc: 'Vencimiento del CAE' },
  { key: '{{COMPROBANTE_NRO}}', desc: 'Número de comprobante' },
];

// ═══════════════════════════════════════════════════════
// COMPONENTE PRINCIPAL
// ═══════════════════════════════════════════════════════

export default function ImpresorasPage() {
  // Data
  const [sucursales, setSucursales] = useState<Sucursal[]>([]);
  const [impresoras, setImpresoras] = useState<Impresora[]>([]);
  const [tiposTicket, setTiposTicket] = useState<TipoTicket[]>([]);
  const [asignaciones, setAsignaciones] = useState<ImpresoraTicketTipo[]>([]);
  const [loading, setLoading] = useState(true);

  // Modal Impresora
  const [showImpresoraModal, setShowImpresoraModal] = useState(false);
  const [editingImpresora, setEditingImpresora] = useState<Impresora | null>(null);
  const [impNombre, setImpNombre] = useState('');
  const [impSucursalId, setImpSucursalId] = useState('');
  const [impTipoDispositivo, setImpTipoDispositivo] = useState<number>(0); // 0=Comandera, 1=Epson, 2=Hasar, 3=Moretti, 4=Electronica
  const [impTipoConexion, setImpTipoConexion] = useState<'Red' | 'USB'>('Red');
  const [impDireccion, setImpDireccion] = useState('');
  const [impPuerto, setImpPuerto] = useState(9100);
  const [impVelocidad, setImpVelocidad] = useState(9600);
  const [submitting, setSubmitting] = useState(false);
  const [testingPrinterId, setTestingPrinterId] = useState<string | null>(null);
  const [testResults, setTestResults] = useState<Record<string, { ok: boolean; message: string }>>({});

  // Modal Template
  const [showTemplateModal, setShowTemplateModal] = useState(false);
  const [editingTipoTicket, setEditingTipoTicket] = useState<TipoTicket | null>(null);
  const [templateNombre, setTemplateNombre] = useState('');
  const [templateContenido, setTemplateContenido] = useState('');
  const [savingTemplate, setSavingTemplate] = useState(false);

  // Sección colapsada
  const [seccionAbierta, setSeccionAbierta] = useState<'impresoras' | 'templates' | 'asignaciones'>('impresoras');

  // ═══════════════════════════════════════════════════════
  // CARGA DE DATOS
  // ═══════════════════════════════════════════════════════

  const loadData = async () => {
    setLoading(true);
    try {
      const [sucsRes, impRes, tiposRes, asigRes] = await Promise.all([
        api.get('/sucursal'),
        api.get('/impresora'),
        api.get('/tipo-ticket'),
        api.get('/impresora').then(async (res) => {
          const allAsignaciones: ImpresoraTicketTipo[] = [];
          for (const imp of res.data) {
            if (imp.ticketTiposHabilitados) {
              allAsignaciones.push(...imp.ticketTiposHabilitados);
            }
          }
          return allAsignaciones;
        }),
      ]);
      setSucursales(sucsRes.data);
      setImpresoras(impRes.data);
      setTiposTicket(tiposRes.data);
      setAsignaciones(asigRes);
    } catch (err) {
      console.error('Error al cargar datos de impresoras:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  // ═══════════════════════════════════════════════════════
  // HANDLERS — IMPRESORA CRUD & TEST
  // ═══════════════════════════════════════════════════════

  function openCreateImpresora() {
    setEditingImpresora(null);
    setImpNombre('');
    setImpSucursalId(sucursales[0]?.id || '');
    setImpTipoDispositivo(0);
    setImpTipoConexion('Red');
    setImpDireccion('');
    setImpPuerto(9100);
    setImpVelocidad(9600);
    setShowImpresoraModal(true);
  }

  function openEditImpresora(imp: Impresora) {
    setEditingImpresora(imp);
    setImpNombre(imp.nombre);
    setImpSucursalId(imp.sucursalId);
    setImpTipoDispositivo(imp.tipoDispositivo ?? 0);
    const connStr = typeof imp.tipoConexion === 'number'
      ? (imp.tipoConexion === 0 ? 'Red' : 'USB')
      : imp.tipoConexion;
    setImpTipoConexion(connStr as 'Red' | 'USB');
    setImpDireccion(imp.direccion);
    setImpPuerto(imp.puerto || 9100);
    setImpVelocidad(imp.velocidad || 9600);
    setShowImpresoraModal(true);
  }

  function closeImpresoraModal() {
    setShowImpresoraModal(false);
    setEditingImpresora(null);
  }

  async function handleImpresoraSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!impNombre.trim()) return;
    if (impTipoDispositivo !== 4 && !impDireccion.trim()) return;
    setSubmitting(true);

    const payload = {
      nombre: impNombre.trim(),
      tipoDispositivo: Number(impTipoDispositivo),
      tipoConexion: impTipoConexion === 'Red' ? 0 : 1,
      direccion: impDireccion.trim(),
      puerto: Number(impPuerto) || 9100,
      velocidad: Number(impVelocidad) || 9600,
    };

    try {
      if (editingImpresora) {
        await api.put(`/impresora/${editingImpresora.id}`, payload);
      } else {
        await api.post('/impresora', { sucursalId: impSucursalId, ...payload });
      }
      closeImpresoraModal();
      await loadData();
    } catch (err) {
      console.error('Error al guardar impresora:', err);
      alert('Error al guardar la impresora.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleTestConnection(imp: Impresora) {
    setTestingPrinterId(imp.id);
    try {
      const res = await api.post(`/impresora/${imp.id}/test-connection`);
      setTestResults(prev => ({ ...prev, [imp.id]: { ok: true, message: res.data.message || 'Conexión OK' } }));
    } catch (err: any) {
      const errorMsg = err.response?.data?.message || 'Error al conectar con la impresora';
      setTestResults(prev => ({ ...prev, [imp.id]: { ok: false, message: errorMsg } }));
    } finally {
      setTestingPrinterId(null);
    }
  }

  async function handleDeleteImpresora(id: string) {
    if (confirm('¿Estás seguro de que deseas desactivar esta impresora?')) {
      try {
        await api.delete(`/impresora/${id}`);
        await loadData();
      } catch (err) {
        console.error('Error al eliminar impresora:', err);
      }
    }
  }

  // ═══════════════════════════════════════════════════════
  // HANDLERS — TEMPLATE
  // ═══════════════════════════════════════════════════════

  function openTemplateEditor(tipo: TipoTicket) {
    setEditingTipoTicket(tipo);
    setTemplateNombre(tipo.nombre);
    setTemplateContenido(tipo.templateContenido);
    setShowTemplateModal(true);
  }

  function closeTemplateModal() {
    setShowTemplateModal(false);
    setEditingTipoTicket(null);
  }

  async function handleTemplateSave() {
    if (!editingTipoTicket) return;
    setSavingTemplate(true);

    try {
      await api.put(`/tipo-ticket/${editingTipoTicket.id}`, {
        nombre: templateNombre,
        templateContenido: templateContenido,
      });
      closeTemplateModal();
      await loadData();
    } catch (err) {
      console.error('Error al guardar template:', err);
      alert('Error al guardar el template.');
    } finally {
      setSavingTemplate(false);
    }
  }

  // ═══════════════════════════════════════════════════════
  // HANDLERS — ASIGNACIONES
  // ═══════════════════════════════════════════════════════

  function isAsignado(impresoraId: string, tipoTicketId: string) {
    return asignaciones.some(a => a.impresoraId === impresoraId && a.tipoTicketId === tipoTicketId && a.isActive);
  }

  async function toggleAsignacion(impresoraId: string, tipoTicketId: string) {
    try {
      if (isAsignado(impresoraId, tipoTicketId)) {
        await api.delete(`/impresora/${impresoraId}/tipo-ticket/${tipoTicketId}`);
      } else {
        await api.post(`/impresora/${impresoraId}/tipo-ticket/${tipoTicketId}`);
      }
      await loadData();
    } catch (err) {
      console.error('Error al cambiar asignación:', err);
    }
  }

  function getSucursalNombre(sucursalId: string) {
    return sucursales.find(s => s.id === sucursalId)?.nombre || '—';
  }

  // ═══════════════════════════════════════════════════════
  // RENDER
  // ═══════════════════════════════════════════════════════

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Configuración de Impresoras</h3>
          <p className="text-xs text-pearl-400">
            {impresoras.filter(i => i.isActive).length} impresora(s) activas · {tiposTicket.length} tipos de ticket
          </p>
        </div>
        <button
          onClick={loadData}
          className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 rounded-lg hover:bg-ice-50 active:scale-[0.98] transition-all cursor-pointer"
          title="Refrescar"
        >
          <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
        </button>
      </div>

      {loading && impresoras.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <Printer size={40} className="animate-pulse mb-3" />
          <p className="text-sm">Cargando configuración de impresoras...</p>
        </div>
      ) : (
        <>
          {/* ═══════════ SECCIÓN 1: IMPRESORAS ═══════════ */}
          <section className="bg-white rounded-xl border border-pearl-100 overflow-hidden">
            <button
              onClick={() => setSeccionAbierta(seccionAbierta === 'impresoras' ? 'impresoras' : 'impresoras')}
              className="w-full flex items-center justify-between px-5 py-4 cursor-pointer hover:bg-ice-50 transition-colors"
            >
              <div className="flex items-center gap-3">
                <div className="w-9 h-9 rounded-lg bg-brand-50 text-brand-600 flex items-center justify-center">
                  <Printer size={18} />
                </div>
                <div className="text-left">
                  <h4 className="text-sm font-bold text-pearl-900">Impresoras</h4>
                  <p className="text-[11px] text-pearl-400">Gestión de impresoras térmicas por sucursal</p>
                </div>
              </div>
              {seccionAbierta === 'impresoras' ? <ChevronUp size={18} className="text-pearl-400" /> : <ChevronDown size={18} className="text-pearl-400" />}
            </button>

            {seccionAbierta === 'impresoras' && (
              <div className="px-5 pb-5 space-y-4 border-t border-pearl-50 pt-4 animate-fade-in">
                <div className="flex justify-end">
                  <button
                    id="add-impresora-btn"
                    onClick={openCreateImpresora}
                    className="flex items-center gap-1.5 h-9 px-4 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all duration-200 cursor-pointer"
                  >
                    <Plus size={16} /> Nueva Impresora
                  </button>
                </div>

                {impresoras.filter(i => i.isActive).length === 0 ? (
                  <div className="bg-ice-50 rounded-lg p-8 text-center text-pearl-400 text-sm">
                    No hay impresoras configuradas. Presiona "Nueva Impresora" para empezar.
                  </div>
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4 stagger-children">
                    {impresoras.filter(i => i.isActive).map(imp => {
                      const testRes = testResults[imp.id];
                      const isTesting = testingPrinterId === imp.id;
                      const deviceTypeNames = ['Comandera', 'Fiscal Epson', 'Fiscal Hasar', 'Fiscal Moretti', 'Electrónica AFIP'];
                      const deviceTypeLabel = deviceTypeNames[imp.tipoDispositivo] || 'Comandera';

                      return (
                      <div
                        key={imp.id}
                        className="bg-white rounded-xl border border-pearl-100 p-5 transition-all duration-300 hover:shadow-lg hover:-translate-y-0.5 group"
                      >
                        <div className="flex items-start justify-between mb-3">
                          <div className="flex items-center gap-3">
                            <div className="w-11 h-11 rounded-xl flex items-center justify-center bg-brand-50 text-brand-600 transition-transform group-hover:scale-110">
                              {imp.tipoDispositivo === 4 ? <FileText size={20} /> : (imp.tipoConexion === 'Red' || imp.tipoConexion === 0 ? <Wifi size={20} /> : <Usb size={20} />)}
                            </div>
                            <div>
                              <h4 className="text-sm font-bold text-pearl-900">{imp.nombre}</h4>
                              <p className="text-[11px] text-pearl-400">{getSucursalNombre(imp.sucursalId)}</p>
                            </div>
                          </div>
                          <div className="flex flex-col items-end gap-1">
                            <span className={`text-[10px] font-semibold px-2.5 py-0.5 rounded-full ${
                              imp.tipoDispositivo === 0 ? 'bg-brand-50 text-brand-700' :
                              imp.tipoDispositivo === 1 ? 'bg-blue-50 text-blue-700' :
                              imp.tipoDispositivo === 2 ? 'bg-purple-50 text-purple-700' :
                              imp.tipoDispositivo === 3 ? 'bg-pearl-100 text-pearl-500' :
                              'bg-success-50 text-success-700'
                            }`}>
                              {deviceTypeLabel}
                            </span>
                            {imp.tipoDispositivo !== 4 && (
                              <span className="text-[9px] text-pearl-400">
                                {imp.tipoConexion === 'Red' || imp.tipoConexion === 0 ? 'Red' : 'USB/COM'}
                              </span>
                            )}
                          </div>
                        </div>

                        {imp.tipoDispositivo !== 4 && (
                          <div className="grid grid-cols-2 gap-3 mb-3">
                            <div className="bg-ice-50 rounded-lg p-3">
                              <p className="text-[10px] text-pearl-400 uppercase tracking-wider">Dirección / COM</p>
                              <p className="text-xs font-semibold text-pearl-700 mt-0.5 truncate">{imp.direccion || '-'}</p>
                            </div>
                            <div className="bg-ice-50 rounded-lg p-3">
                              <p className="text-[10px] text-pearl-400 uppercase tracking-wider">Puerto / Baud</p>
                              <p className="text-xs font-semibold text-pearl-700 mt-0.5">
                                {imp.tipoDispositivo === 1 || imp.tipoDispositivo === 3 ? `${imp.velocidad || 9600} bps` : (imp.puerto || 9100)}
                              </p>
                            </div>
                          </div>
                        )}

                        {/* Tickets habilitados */}
                        <div className="flex flex-wrap gap-1.5 mb-3">
                          {tiposTicket.filter(t => isAsignado(imp.id, t.id)).map(t => (
                            <span key={t.id} className="text-[10px] font-medium bg-success-50 text-success-600 px-2 py-0.5 rounded-full">
                              {t.nombre}
                            </span>
                          ))}
                          {tiposTicket.filter(t => isAsignado(imp.id, t.id)).length === 0 && (
                            <span className="text-[10px] text-pearl-400 italic">Sin tickets asignados</span>
                          )}
                        </div>

                        {/* Connection Test feedback */}
                        {testRes && (
                          <div className={`text-[11px] p-2 rounded-lg mb-3 ${testRes.ok ? 'bg-success-50 text-success-700 border border-success-200' : 'bg-danger-50 text-danger-700 border border-danger-200'}`}>
                            {testRes.ok ? '✅ ' : '❌ '}{testRes.message}
                          </div>
                        )}

                        <div className="flex items-center justify-between pt-3 border-t border-pearl-50">
                          {imp.tipoDispositivo !== 4 ? (
                            <button
                              onClick={() => handleTestConnection(imp)}
                              disabled={isTesting}
                              className="text-xs text-brand-600 hover:text-brand-800 font-medium transition-colors cursor-pointer flex items-center gap-1 bg-brand-50 px-2.5 py-1 rounded-lg hover:bg-brand-100 disabled:opacity-50"
                            >
                              <RefreshCw size={12} className={isTesting ? 'animate-spin' : ''} />
                              {isTesting ? 'Probando...' : 'Test Conexión'}
                            </button>
                          ) : <div />}

                          <div className="flex items-center gap-3">
                            <button
                              onClick={() => openEditImpresora(imp)}
                              className="text-xs text-pearl-500 hover:text-brand-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                            >
                              <Edit2 size={12} /> Editar
                            </button>
                            <button
                              onClick={() => handleDeleteImpresora(imp.id)}
                              className="text-xs text-pearl-500 hover:text-danger-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                            >
                              <Trash2 size={12} /> Desactivar
                            </button>
                          </div>
                        </div>
                      </div>
                      );
                    })}
                  </div>
                )}
              </div>
            )}
          </section>

          {/* ═══════════ SECCIÓN 2: TIPOS DE TICKET / TEMPLATES ═══════════ */}
          <section className="bg-white rounded-xl border border-pearl-100 overflow-hidden">
            <button
              onClick={() => setSeccionAbierta(seccionAbierta === 'templates' ? 'impresoras' : 'templates')}
              className="w-full flex items-center justify-between px-5 py-4 cursor-pointer hover:bg-ice-50 transition-colors"
            >
              <div className="flex items-center gap-3">
                <div className="w-9 h-9 rounded-lg bg-success-50 text-success-600 flex items-center justify-center">
                  <FileText size={18} />
                </div>
                <div className="text-left">
                  <h4 className="text-sm font-bold text-pearl-900">Templates de Tickets</h4>
                  <p className="text-[11px] text-pearl-400">Editar el contenido de cada tipo de ticket</p>
                </div>
              </div>
              {seccionAbierta === 'templates' ? <ChevronUp size={18} className="text-pearl-400" /> : <ChevronDown size={18} className="text-pearl-400" />}
            </button>

            {seccionAbierta === 'templates' && (
              <div className="px-5 pb-5 border-t border-pearl-50 pt-4 animate-fade-in">
                <div className="space-y-3">
                  {tiposTicket.map(tipo => (
                    <div
                      key={tipo.id}
                      className="flex items-center justify-between bg-ice-50 rounded-lg p-4 hover:bg-ice-100 transition-colors"
                    >
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 rounded-lg bg-pearl-200 text-pearl-600 flex items-center justify-center">
                          <FileText size={16} />
                        </div>
                        <div>
                          <p className="text-sm font-semibold text-pearl-800">{tipo.nombre}</p>
                          <p className="text-[11px] text-pearl-400">Código: <span className="font-mono text-pearl-500">{tipo.codigo}</span></p>
                        </div>
                      </div>
                      <button
                        onClick={() => openTemplateEditor(tipo)}
                        className="flex items-center gap-1.5 h-8 px-3 text-xs font-medium text-brand-600 bg-brand-50 rounded-lg hover:bg-brand-100 transition-colors cursor-pointer"
                      >
                        <Edit2 size={12} /> Editar Template
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>

          {/* ═══════════ SECCIÓN 3: ASIGNACIONES ═══════════ */}
          <section className="bg-white rounded-xl border border-pearl-100 overflow-hidden">
            <button
              onClick={() => setSeccionAbierta(seccionAbierta === 'asignaciones' ? 'impresoras' : 'asignaciones')}
              className="w-full flex items-center justify-between px-5 py-4 cursor-pointer hover:bg-ice-50 transition-colors"
            >
              <div className="flex items-center gap-3">
                <div className="w-9 h-9 rounded-lg bg-warning-50 text-warning-600 flex items-center justify-center">
                  <Link2 size={18} />
                </div>
                <div className="text-left">
                  <h4 className="text-sm font-bold text-pearl-900">Asignaciones Impresora ↔ Ticket</h4>
                  <p className="text-[11px] text-pearl-400">Definir qué tipo de ticket se imprime en cada impresora</p>
                </div>
              </div>
              {seccionAbierta === 'asignaciones' ? <ChevronUp size={18} className="text-pearl-400" /> : <ChevronDown size={18} className="text-pearl-400" />}
            </button>

            {seccionAbierta === 'asignaciones' && (
              <div className="px-5 pb-5 border-t border-pearl-50 pt-4 animate-fade-in">
                {impresoras.filter(i => i.isActive).length === 0 ? (
                  <div className="bg-ice-50 rounded-lg p-8 text-center text-pearl-400 text-sm">
                    No hay impresoras configuradas. Crea una impresora primero.
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-pearl-200">
                          <th className="text-left py-3 px-4 text-[11px] font-semibold text-pearl-500 uppercase tracking-wider">
                            Impresora
                          </th>
                          {tiposTicket.map(t => (
                            <th key={t.id} className="text-center py-3 px-4 text-[11px] font-semibold text-pearl-500 uppercase tracking-wider">
                              {t.nombre}
                            </th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {impresoras.filter(i => i.isActive).map(imp => (
                          <tr key={imp.id} className="border-b border-pearl-50 hover:bg-ice-50 transition-colors">
                            <td className="py-3 px-4">
                              <div className="flex items-center gap-2">
                                {imp.tipoConexion === 'Red'
                                  ? <Wifi size={14} className="text-brand-500" />
                                  : <Usb size={14} className="text-warning-500" />
                                }
                                <div>
                                  <p className="font-semibold text-pearl-800">{imp.nombre}</p>
                                  <p className="text-[10px] text-pearl-400">{getSucursalNombre(imp.sucursalId)}</p>
                                </div>
                              </div>
                            </td>
                            {tiposTicket.map(tipo => {
                              const asignado = isAsignado(imp.id, tipo.id);
                              return (
                                <td key={tipo.id} className="text-center py-3 px-4">
                                  <button
                                    onClick={() => toggleAsignacion(imp.id, tipo.id)}
                                    className={`
                                      inline-flex items-center justify-center w-9 h-9 rounded-lg
                                      transition-all duration-200 cursor-pointer
                                      ${asignado
                                        ? 'bg-success-50 text-success-600 hover:bg-success-500 hover:text-white shadow-sm'
                                        : 'bg-pearl-100 text-pearl-300 hover:bg-pearl-200 hover:text-pearl-500'
                                      }
                                    `}
                                    title={asignado ? 'Desasignar' : 'Asignar'}
                                  >
                                    {asignado ? <CheckCircle2 size={18} /> : <Unlink size={14} />}
                                  </button>
                                </td>
                              );
                            })}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </section>
        </>
      )}

      {/* ═══════════ MODAL: CREAR / EDITAR IMPRESORA ═══════════ */}
      {showImpresoraModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={closeImpresoraModal}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-md mx-4 p-6 animate-fade-in" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-5">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
                  <Printer size={20} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-pearl-900">
                    {editingImpresora ? 'Editar Impresora' : 'Nueva Impresora'}
                  </h4>
                  <p className="text-[11px] text-pearl-400">Configurar la conexión de la impresora</p>
                </div>
              </div>
              <button onClick={closeImpresoraModal} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleImpresoraSubmit} className="space-y-4">
              {/* Sucursal (solo al crear) */}
              {!editingImpresora && (
                <div>
                  <label htmlFor="imp-sucursal" className="block text-xs font-medium text-pearl-600 mb-1.5">Sucursal</label>
                  <select
                    id="imp-sucursal"
                    value={impSucursalId}
                    onChange={e => setImpSucursalId(e.target.value)}
                    className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 cursor-pointer"
                  >
                    {sucursales.map(s => (
                      <option key={s.id} value={s.id}>{s.nombre}</option>
                    ))}
                  </select>
                </div>
              )}

              <div>
                <label htmlFor="imp-nombre" className="block text-xs font-medium text-pearl-600 mb-1.5">Nombre *</label>
                <input
                  id="imp-nombre"
                  type="text"
                  value={impNombre}
                  onChange={e => setImpNombre(e.target.value)}
                  placeholder="Ej: Caja Principal, Fiscal Epson, Cocina"
                  required
                  className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400"
                />
              </div>

              {/* Tipo Dispositivo */}
              <div>
                <label htmlFor="imp-tipo-disp" className="block text-xs font-medium text-pearl-600 mb-1.5">Tipo de Dispositivo</label>
                <select
                  id="imp-tipo-disp"
                  value={impTipoDispositivo}
                  onChange={e => {
                    const val = Number(e.target.value);
                    setImpTipoDispositivo(val);
                    if (val === 1) { setImpTipoConexion('USB'); setImpDireccion('COM3'); setImpVelocidad(9600); }
                    else if (val === 2) { setImpTipoConexion('Red'); setImpDireccion('192.168.1.100'); setImpPuerto(80); }
                    else if (val === 3) { setImpTipoConexion('USB'); setImpDireccion('COM4'); setImpVelocidad(9600); }
                    else if (val === 4) { setImpTipoDireccion('AFIP_ELECTRONICA'); }
                  }}
                  className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 cursor-pointer font-medium"
                >
                  <option value={0}>Comandera (ESC/POS no fiscal)</option>
                  <option value={1}>Fiscal Epson (TM-T900FA / TM-T88 - DLL)</option>
                  <option value={2}>Fiscal Hasar (SMH/PT-250AF 2G - HTTP/XML)</option>
                  <option value={3}>Fiscal Moretti (Genesis - Serie binario)</option>
                  <option value={4}>Factura Electrónica AFIP (Sin hardware)</option>
                </select>
              </div>

              {/* Formulario condicional según Tipo Dispositivo */}
              {impTipoDispositivo === 0 && (
                <>
                  <div>
                    <label className="block text-xs font-medium text-pearl-600 mb-1.5">Tipo de Conexión</label>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => setImpTipoConexion('Red')}
                        className={`flex-1 flex items-center justify-center gap-2 h-9 rounded-lg text-sm font-medium transition-all cursor-pointer ${
                          impTipoConexion === 'Red'
                            ? 'bg-brand-600 text-white'
                            : 'bg-pearl-100 text-pearl-600 hover:bg-pearl-200'
                        }`}
                      >
                        <Wifi size={14} /> Red (TCP/IP)
                      </button>
                      <button
                        type="button"
                        onClick={() => setImpTipoConexion('USB')}
                        className={`flex-1 flex items-center justify-center gap-2 h-9 rounded-lg text-sm font-medium transition-all cursor-pointer ${
                          impTipoConexion === 'USB'
                            ? 'bg-warning-500 text-white'
                            : 'bg-pearl-100 text-pearl-600 hover:bg-pearl-200'
                        }`}
                      >
                        <Usb size={14} /> USB
                      </button>
                    </div>
                  </div>

                  <div>
                    <label htmlFor="imp-direccion" className="block text-xs font-medium text-pearl-600 mb-1.5">
                      {impTipoConexion === 'Red' ? 'Dirección IP' : 'Nombre de Impresora en Spooler'}
                    </label>
                    <input
                      id="imp-direccion"
                      type="text"
                      value={impDireccion}
                      onChange={e => setImpDireccion(e.target.value)}
                      placeholder={impTipoConexion === 'Red' ? 'Ej: 192.168.1.100' : 'Ej: EPSON_TM_T20'}
                      required
                      className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400 font-mono"
                    />
                  </div>

                  {impTipoConexion === 'Red' && (
                    <div>
                      <label htmlFor="imp-puerto" className="block text-xs font-medium text-pearl-600 mb-1.5">Puerto TCP</label>
                      <input
                        id="imp-puerto"
                        type="number"
                        value={impPuerto}
                        onChange={e => setImpPuerto(parseInt(e.target.value) || 9100)}
                        className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 font-mono"
                      />
                    </div>
                  )}
                </>
              )}

              {impTipoDispositivo === 1 && ( // Epson
                <>
                  <div>
                    <label htmlFor="imp-direccion" className="block text-xs font-medium text-pearl-600 mb-1.5">Puerto de Conexión (COM o USB)</label>
                    <input
                      id="imp-direccion"
                      type="text"
                      value={impDireccion}
                      onChange={e => setImpDireccion(e.target.value)}
                      placeholder="Ej: COM3 o USB"
                      required
                      className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 font-mono"
                    />
                  </div>
                  <div>
                    <label htmlFor="imp-velocidad" className="block text-xs font-medium text-pearl-600 mb-1.5">Velocidad (BaudRate)</label>
                    <select
                      id="imp-velocidad"
                      value={impVelocidad}
                      onChange={e => setImpVelocidad(Number(e.target.value))}
                      className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 cursor-pointer font-mono"
                    >
                      <option value={9600}>9600 bps</option>
                      <option value={115200}>115200 bps</option>
                    </select>
                  </div>
                </>
              )}

              {impTipoDispositivo === 2 && ( // Hasar
                <>
                  <div>
                    <label htmlFor="imp-direccion" className="block text-xs font-medium text-pearl-600 mb-1.5">Dirección IP de la Impresora Fiscal</label>
                    <input
                      id="imp-direccion"
                      type="text"
                      value={impDireccion}
                      onChange={e => setImpDireccion(e.target.value)}
                      placeholder="Ej: 192.168.1.100"
                      required
                      className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 font-mono"
                    />
                  </div>
                  <div>
                    <label htmlFor="imp-puerto" className="block text-xs font-medium text-pearl-600 mb-1.5">Puerto HTTP (por defecto 80)</label>
                    <input
                      id="imp-puerto"
                      type="number"
                      value={impPuerto}
                      onChange={e => setImpPuerto(parseInt(e.target.value) || 80)}
                      className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 font-mono"
                    />
                  </div>
                </>
              )}

              {impTipoDispositivo === 3 && ( // Moretti
                <div className="bg-amber-50 p-3 rounded-lg border border-amber-200 text-xs text-amber-800">
                  ⚠️ Driver Moretti Genesis en desarrollo para fases futuras.
                </div>
              )}

              {impTipoDispositivo === 4 && ( // Factura Electrónica
                <div className="bg-info-50 p-3 rounded-lg border border-info-200 text-xs text-brand-800">
                  ℹ️ Facturación electrónica vía AFIP WSFE. Requiere CUIT y Certificado en la configuración de la sucursal.
                </div>
              )}

              <div className="flex gap-2 pt-2">
                <button
                  type="button"
                  onClick={closeImpresoraModal}
                  className="flex-1 h-9 text-sm font-medium text-pearl-600 bg-pearl-100 rounded-lg hover:bg-pearl-200 transition-colors cursor-pointer"
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  disabled={submitting || !impNombre.trim() || (impTipoDispositivo !== 4 && !impDireccion.trim())}
                  className="flex-1 h-9 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed transition-all cursor-pointer"
                >
                  {submitting ? 'Guardando...' : 'Guardar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ═══════════ MODAL: EDITAR TEMPLATE ═══════════ */}
      {showTemplateModal && editingTipoTicket && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={closeTemplateModal}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-3xl mx-4 p-6 animate-fade-in max-h-[90vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-5">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-success-50 text-success-600 flex items-center justify-center">
                  <FileText size={20} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-pearl-900">
                    Editar Template — {editingTipoTicket.nombre}
                  </h4>
                  <p className="text-[11px] text-pearl-400">Código: <span className="font-mono">{editingTipoTicket.codigo}</span></p>
                </div>
              </div>
              <button onClick={closeTemplateModal} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
              {/* Editor */}
              <div className="lg:col-span-2 space-y-3">
                <div>
                  <label htmlFor="template-nombre" className="block text-xs font-medium text-pearl-600 mb-1.5">Nombre del Ticket</label>
                  <input
                    id="template-nombre"
                    type="text"
                    value={templateNombre}
                    onChange={e => setTemplateNombre(e.target.value)}
                    className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
                  />
                </div>
                <div>
                  <label htmlFor="template-contenido" className="block text-xs font-medium text-pearl-600 mb-1.5">Contenido del Template</label>
                  <textarea
                    id="template-contenido"
                    value={templateContenido}
                    onChange={e => setTemplateContenido(e.target.value)}
                    rows={18}
                    className="w-full px-3 py-2 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 font-mono leading-relaxed resize-y"
                    spellCheck={false}
                  />
                </div>
              </div>

              {/* Placeholders */}
              <div>
                <p className="text-xs font-semibold text-pearl-600 mb-2">Placeholders Disponibles</p>
                <div className="bg-ice-50 rounded-lg p-3 space-y-1.5 max-h-[420px] overflow-y-auto">
                  {PLACEHOLDERS.map(p => (
                    <button
                      key={p.key}
                      type="button"
                      onClick={() => {
                        setTemplateContenido(prev => prev + p.key);
                      }}
                      className="w-full text-left px-2 py-1.5 rounded-md hover:bg-brand-50 transition-colors cursor-pointer group"
                    >
                      <p className="text-[11px] font-mono text-brand-600 group-hover:text-brand-700">{p.key}</p>
                      <p className="text-[10px] text-pearl-400">{p.desc}</p>
                    </button>
                  ))}
                </div>
              </div>
            </div>

            <div className="flex gap-2 pt-4 mt-4 border-t border-pearl-100">
              <button
                type="button"
                onClick={closeTemplateModal}
                className="flex-1 h-9 text-sm font-medium text-pearl-600 bg-pearl-100 rounded-lg hover:bg-pearl-200 transition-colors cursor-pointer"
              >
                Cancelar
              </button>
              <button
                onClick={handleTemplateSave}
                disabled={savingTemplate}
                className="flex-1 flex items-center justify-center gap-2 h-9 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed transition-all cursor-pointer"
              >
                <Save size={14} />
                {savingTemplate ? 'Guardando...' : 'Guardar Template'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
