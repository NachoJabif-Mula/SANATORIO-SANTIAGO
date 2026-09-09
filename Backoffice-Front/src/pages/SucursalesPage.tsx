import { useState, useEffect } from 'react';
import { Building2, MapPin, CheckCircle2, XCircle, RefreshCw, Plus, Edit2, Trash2, X, FileText, ChevronDown, ChevronUp } from 'lucide-react';
import api from '@/services/api';

interface Sucursal {
  id: string;
  nombre: string;
  direccion: string;
  cuit?: string;
  razonSocial?: string;
  domicilioFiscal?: string;
  condicionIva?: number;
  puntoDeVenta?: number;
  numeroIIBB?: string;
  fechaInicioActividades?: string;
  isActive: boolean;
}

interface Dispositivo {
  id: string;
  sucursal: string;
  isActivado: boolean;
}

const CONDICIONES_IVA = [
  { value: 1, label: 'Responsable Inscripto' },
  { value: 4, label: 'Exento' },
  { value: 5, label: 'Consumidor Final' },
  { value: 6, label: 'Monotributista' },
];

export default function SucursalesPage() {
  const [sucursales, setSucursales] = useState<Sucursal[]>([]);
  const [devices, setDevices] = useState<Dispositivo[]>([]);
  const [loading, setLoading] = useState(true);

  // Modal States
  const [showModal, setShowModal] = useState(false);
  const [editingSucursal, setEditingSucursal] = useState<Sucursal | null>(null);
  const [nombre, setNombre] = useState('');
  const [direccion, setDireccion] = useState('');
  
  // Fiscal States
  const [cuit, setCuit] = useState('');
  const [razonSocial, setRazonSocial] = useState('');
  const [domicilioFiscal, setDomicilioFiscal] = useState('');
  const [condicionIva, setCondicionIva] = useState<number>(1);
  const [puntoDeVenta, setPuntoDeVenta] = useState<number>(1);
  const [numeroIIBB, setNumeroIIBB] = useState('');
  const [fechaInicioActividades, setFechaInicioActividades] = useState('');
  const [showFiscalSection, setShowFiscalSection] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const loadData = async () => {
    setLoading(true);
    try {
      const [sucsRes, disposRes] = await Promise.all([
        api.get('/sucursal'),
        api.get('/dispositivos').catch(() => ({ data: [] }))
      ]);
      setSucursales(sucsRes.data);
      setDevices(disposRes.data || []);
    } catch (err) {
      console.error('Error al cargar sucursales:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  function getDispositivosCount(sucursalNombre: string) {
    return devices.filter(d => d.sucursal === sucursalNombre && d.isActivado).length;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!nombre.trim()) return;
    setSubmitting(true);

    const payload = {
      nombre: nombre.trim(),
      direccion: direccion.trim(),
      cuit: cuit.trim() || null,
      razonSocial: razonSocial.trim() || null,
      domicilioFiscal: domicilioFiscal.trim() || null,
      condicionIva: cuit.trim() ? Number(condicionIva) : null,
      puntoDeVenta: Number(puntoDeVenta) || 1,
      numeroIIBB: numeroIIBB.trim() || null,
      fechaInicioActividades: fechaInicioActividades || null
    };

    try {
      if (editingSucursal) {
        await api.put(`/sucursal/${editingSucursal.id}`, payload);
      } else {
        await api.post('/sucursal', payload);
      }
      closeModal();
      await loadData();
    } catch (err) {
      console.error('Error al guardar sucursal:', err);
      alert('Error al guardar la sucursal en el servidor.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(id: string) {
    if (confirm('¿Estás seguro de que deseas desactivar esta sucursal?')) {
      try {
        await api.delete(`/sucursal/${id}`);
        await loadData();
      } catch (err) {
        console.error('Error al eliminar sucursal:', err);
        alert('Error al desactivar la sucursal.');
      }
    }
  }

  function openCreateModal() {
    setEditingSucursal(null);
    setNombre('');
    setDireccion('');
    setCuit('');
    setRazonSocial('');
    setDomicilioFiscal('');
    setCondicionIva(1);
    setPuntoDeVenta(1);
    setNumeroIIBB('');
    setFechaInicioActividades('');
    setShowFiscalSection(false);
    setShowModal(true);
  }

  function openEditModal(sucursal: Sucursal) {
    setEditingSucursal(sucursal);
    setNombre(sucursal.nombre || '');
    setDireccion(sucursal.direccion || '');
    setCuit(sucursal.cuit || '');
    setRazonSocial(sucursal.razonSocial || '');
    setDomicilioFiscal(sucursal.domicilioFiscal || '');
    setCondicionIva(sucursal.condicionIva || 1);
    setPuntoDeVenta(sucursal.puntoDeVenta || 1);
    setNumeroIIBB(sucursal.numeroIIBB || '');
    setFechaInicioActividades(sucursal.fechaInicioActividades ? sucursal.fechaInicioActividades.split('T')[0] : '');
    setShowFiscalSection(Boolean(sucursal.cuit));
    setShowModal(true);
  }

  function closeModal() {
    setShowModal(false);
    setEditingSucursal(null);
  }

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-bold text-pearl-900">Gestión de Sucursales</h3>
          <p className="text-xs text-pearl-400">
            {sucursales.filter(s => s.isActive).length} activas de {sucursales.length} registradas
          </p>
        </div>
        <div className="flex gap-2">
          <button 
            onClick={loadData}
            className="flex items-center justify-center w-9 h-9 border border-pearl-200 text-pearl-500 rounded-lg hover:bg-ice-50 active:scale-[0.98] transition-all cursor-pointer"
            title="Refrescar"
          >
            <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
          </button>
          <button 
            id="add-sucursal-btn" 
            onClick={openCreateModal}
            className="flex items-center gap-1.5 h-9 px-4 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] transition-all duration-200 cursor-pointer"
          >
            <Plus size={16} /> Nueva Sucursal
          </button>
        </div>
      </div>

      {loading && sucursales.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-pearl-400">
          <Building2 size={40} className="animate-pulse mb-3" />
          <p className="text-sm">Cargando sucursales...</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 stagger-children">
          {sucursales.map(s => (
            <div key={s.id} className={`bg-white rounded-xl border p-5 transition-all duration-300 hover:shadow-lg hover:-translate-y-0.5 group ${s.isActive ? 'border-pearl-100' : 'border-pearl-200 opacity-60'}`}>
              <div className="flex items-start justify-between mb-4">
                <div className="flex items-center gap-3">
                  <div className={`w-11 h-11 rounded-xl flex items-center justify-center ${s.isActive ? 'bg-brand-50 text-brand-600' : 'bg-pearl-100 text-pearl-400'} transition-transform group-hover:scale-110`}>
                    <Building2 size={20} />
                  </div>
                  <div>
                    <h4 className="text-sm font-bold text-pearl-900">{s.nombre}</h4>
                    <div className="flex items-center gap-1 text-xs text-pearl-400 mt-0.5">
                      <MapPin size={12} />
                      <span>{s.direccion || 'Sin dirección registrada'}</span>
                    </div>
                  </div>
                </div>
                <div className="flex flex-col items-end gap-1">
                  <span className={`text-[10px] font-semibold px-2.5 py-1 rounded-full ${s.isActive ? 'bg-success-50 text-success-600' : 'bg-pearl-100 text-pearl-500'}`}>
                    {s.isActive ? 'Activa' : 'Inactiva'}
                  </span>
                  {s.cuit ? (
                    <span className="text-[10px] font-medium bg-brand-50 text-brand-700 px-2 py-0.5 rounded border border-brand-200">
                      CUIT: {s.cuit} (PV: {String(s.puntoDeVenta || 1).padStart(4, '0')})
                    </span>
                  ) : (
                    <span className="text-[10px] text-pearl-400 italic">Sin datos fiscales</span>
                  )}
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div className="bg-ice-50 rounded-lg p-3 text-center">
                  <p className="text-[10px] text-pearl-400 uppercase tracking-wider">Sincronización</p>
                  <div className="flex items-center justify-center gap-1 mt-1">
                    {s.isActive ? <CheckCircle2 size={14} className="text-success-500" /> : <XCircle size={14} className="text-warning-500" />}
                    <span className={`text-xs font-semibold ${s.isActive ? 'text-success-600' : 'text-warning-600'}`}>
                      {s.isActive ? 'OK' : 'Pendiente'}
                    </span>
                  </div>
                </div>
                <div className="bg-ice-50 rounded-lg p-3 text-center">
                  <p className="text-[10px] text-pearl-400 uppercase tracking-wider">Punto de Venta</p>
                  <p className="text-xs font-semibold text-pearl-700 mt-1">PV {String(s.puntoDeVenta || 1).padStart(4, '0')}</p>
                </div>
                <div className="bg-ice-50 rounded-lg p-3 text-center">
                  <p className="text-[10px] text-pearl-400 uppercase tracking-wider">Dispositivos</p>
                  <p className="text-xs font-semibold text-pearl-700 mt-1">{getDispositivosCount(s.nombre)} POS</p>
                </div>
              </div>

              <div className="flex items-center justify-end gap-3 mt-4 pt-3 border-t border-pearl-50">
                <button 
                  onClick={() => openEditModal(s)}
                  className="text-xs text-pearl-500 hover:text-brand-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                >
                  <Edit2 size={12} /> Editar
                </button>
                {s.isActive && (
                  <button 
                    onClick={() => handleDelete(s.id)}
                    className="text-xs text-pearl-500 hover:text-danger-600 font-medium transition-colors cursor-pointer flex items-center gap-1"
                  >
                    <Trash2 size={12} /> Desactivar
                  </button>
                )}
              </div>
            </div>
          ))}
          {sucursales.length === 0 && !loading && (
            <div className="col-span-2 bg-white rounded-xl border border-pearl-100 p-8 text-center text-pearl-400">
              No hay sucursales registradas. Presiona "Nueva Sucursal" para empezar.
            </div>
          )}
        </div>
      )}

      {/* Modal Crear / Editar */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm animate-fade-in" onClick={closeModal}>
          <div className="bg-white rounded-2xl shadow-2xl border border-pearl-100 w-full max-w-lg mx-4 p-6 animate-fade-in max-h-[90vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-5">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center">
                  <Building2 size={20} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-pearl-900">
                    {editingSucursal ? 'Editar Sucursal' : 'Nueva Sucursal'}
                  </h4>
                  <p className="text-[11px] text-pearl-400">Completa los datos generales y fiscales</p>
                </div>
              </div>
              <button onClick={closeModal} className="text-pearl-400 hover:text-pearl-600 transition-colors cursor-pointer">
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label htmlFor="modal-nombre" className="block text-xs font-medium text-pearl-600 mb-1.5">Nombre de la sucursal *</label>
                <input 
                  id="modal-nombre" 
                  type="text" 
                  value={nombre} 
                  onChange={e => setNombre(e.target.value)} 
                  placeholder="Ej: Palermo Soho" 
                  required
                  className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400" 
                />
              </div>
              <div>
                <label htmlFor="modal-direccion" className="block text-xs font-medium text-pearl-600 mb-1.5">Dirección</label>
                <input 
                  id="modal-direccion" 
                  type="text" 
                  value={direccion} 
                  onChange={e => setDireccion(e.target.value)} 
                  placeholder="Ej: Av. Honduras 4200, CABA" 
                  className="w-full h-9 px-3 text-sm bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 placeholder:text-pearl-400" 
                />
              </div>

              {/* Colapsable Datos Fiscales */}
              <div className="border border-pearl-200 rounded-xl overflow-hidden mt-4">
                <button
                  type="button"
                  onClick={() => setShowFiscalSection(!showFiscalSection)}
                  className="w-full flex items-center justify-between px-4 py-3 bg-ice-50 text-left cursor-pointer hover:bg-pearl-100/50 transition-colors"
                >
                  <div className="flex items-center gap-2">
                    <FileText size={16} className="text-brand-600" />
                    <span className="text-xs font-bold text-pearl-800">Datos Fiscales del Emisor (AFIP/ARCA)</span>
                  </div>
                  {showFiscalSection ? <ChevronUp size={16} className="text-pearl-500" /> : <ChevronDown size={16} className="text-pearl-500" />}
                </button>

                {showFiscalSection && (
                  <div className="p-4 space-y-3 bg-white border-t border-pearl-100">
                    <div className="grid grid-cols-2 gap-3">
                      <div>
                        <label className="block text-[11px] font-medium text-pearl-600 mb-1">CUIT</label>
                        <input
                          type="text"
                          value={cuit}
                          onChange={e => setCuit(e.target.value)}
                          placeholder="20-12345678-9"
                          className="w-full h-8 px-2.5 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
                        />
                      </div>
                      <div>
                        <label className="block text-[11px] font-medium text-pearl-600 mb-1">Punto de Venta</label>
                        <input
                          type="number"
                          min={1}
                          max={99999}
                          value={puntoDeVenta}
                          onChange={e => setPuntoDeVenta(Number(e.target.value))}
                          placeholder="1"
                          className="w-full h-8 px-2.5 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
                        />
                      </div>
                    </div>

                    <div>
                      <label className="block text-[11px] font-medium text-pearl-600 mb-1">Razón Social</label>
                      <input
                        type="text"
                        value={razonSocial}
                        onChange={e => setRazonSocial(e.target.value)}
                        placeholder="Ej: BARES FAMILIA S.R.L."
                        className="w-full h-8 px-2.5 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
                      />
                    </div>

                    <div>
                      <label className="block text-[11px] font-medium text-pearl-600 mb-1">Domicilio Fiscal</label>
                      <input
                        type="text"
                        value={domicilioFiscal}
                        onChange={e => setDomicilioFiscal(e.target.value)}
                        placeholder="Ej: Av. Santa Fe 1234, CABA"
                        className="w-full h-8 px-2.5 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
                      />
                    </div>

                    <div className="grid grid-cols-2 gap-3">
                      <div>
                        <label className="block text-[11px] font-medium text-pearl-600 mb-1">Condición IVA</label>
                        <select
                          value={condicionIva}
                          onChange={e => setCondicionIva(Number(e.target.value))}
                          className="w-full h-8 px-2.5 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400 cursor-pointer"
                        >
                          {CONDICIONES_IVA.map(c => (
                            <option key={c.value} value={c.value}>{c.label}</option>
                          ))}
                        </select>
                      </div>
                      <div>
                        <label className="block text-[11px] font-medium text-pearl-600 mb-1">Nº IIBB</label>
                        <input
                          type="text"
                          value={numeroIIBB}
                          onChange={e => setNumeroIIBB(e.target.value)}
                          placeholder="901-123456-7"
                          className="w-full h-8 px-2.5 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
                        />
                      </div>
                    </div>

                    <div>
                      <label className="block text-[11px] font-medium text-pearl-600 mb-1">Fecha Inicio Actividades</label>
                      <input
                        type="date"
                        value={fechaInicioActividades}
                        onChange={e => setFechaInicioActividades(e.target.value)}
                        className="w-full h-8 px-2.5 text-xs bg-ice-50 border border-pearl-200 rounded-lg outline-none focus:border-brand-400"
                      />
                    </div>
                  </div>
                )}
              </div>

              <div className="flex gap-2 pt-2">
                <button 
                  type="button"
                  onClick={closeModal} 
                  className="flex-1 h-9 text-sm font-medium text-pearl-600 bg-pearl-100 rounded-lg hover:bg-pearl-200 transition-colors cursor-pointer"
                >
                  Cancelar
                </button>
                <button 
                  type="submit"
                  disabled={submitting || !nombre.trim()}
                  className="flex-1 h-9 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed transition-all cursor-pointer"
                >
                  {submitting ? 'Guardando...' : 'Guardar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

