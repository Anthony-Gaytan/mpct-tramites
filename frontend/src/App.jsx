import { useState } from 'react'
import './App.css'

const API = import.meta.env.VITE_API_URL || '/api'
const steps = ['Registro de solicitud', 'Validación y pago', 'Evaluación técnica', 'Emisión de licencia']
const requirements = ['Solicitud con datos del representante legal', 'Vigencia de poder y documento de identidad', 'Plano o croquis de ubicación', 'Declaración jurada de seguridad']

async function call(path, options = {}) {
  const response = await fetch(`${API}${path}`, { ...options, headers: { 'Content-Type': 'application/json', ...(options.headers || {}) } })
  const data = await response.json().catch(() => ({}))
  if (!response.ok) throw new Error(data.message || data.title || 'No pudimos completar la operación.')
  return data
}

function App() {
  const [view, setView] = useState('home')
  const [notice, setNotice] = useState(null)
  const [tracking, setTracking] = useState(null)
  const [sunat, setSunat] = useState(null)
  const [loading, setLoading] = useState(false)
  const [session, setSession] = useState(() => JSON.parse(sessionStorage.getItem('mpct-session') || 'null'))
  const [importResult, setImportResult] = useState(null)

  const submitLogin = async (event) => {
    event.preventDefault(); setLoading(true); setNotice(null)
    const form = new FormData(event.currentTarget)
    try {
      const data = await call('/auth/login', { method: 'POST', body: JSON.stringify(Object.fromEntries(form)) })
      sessionStorage.setItem('mpct-session', JSON.stringify(data)); setSession(data)
      setNotice({ kind: 'success', text: `Bienvenido, ${data.user.nombres}. Rol: ${data.user.roles.join(', ')}` })
    } catch (error) { setNotice({ kind: 'error', text: error.message }) }
    finally { setLoading(false) }
  }

  const importSunat = async (event) => {
    event.preventDefault(); setLoading(true); setNotice(null); setImportResult(null)
    const form = new FormData(event.currentTarget)
    const file = form.get('archivo')
    if (!file?.size) { setNotice({ kind: 'error', text: 'Selecciona un archivo TXT del Padrón Reducido SUNAT.' }); setLoading(false); return }
    try {
      const response = await fetch(`${API}/sunat/padron/importar`, { method: 'POST', headers: { Authorization: `Bearer ${session.token}` }, body: form })
      const data = await response.json().catch(() => ({}))
      if (!response.ok) throw new Error(data.message || data.title || 'No se pudo importar el padrón.')
      setImportResult(data); setNotice({ kind: 'success', text: `Importación completada: ${data.registros} registro(s).` }); event.currentTarget.reset()
    } catch (error) { setNotice({ kind: 'error', text: error.message }) }
    finally { setLoading(false) }
  }

  const submitTracking = async (event) => {
    event.preventDefault(); setLoading(true); setNotice(null)
    const form = new FormData(event.currentTarget)
    try { setTracking(await call('/solicitudes/seguimiento', { method: 'POST', body: JSON.stringify(Object.fromEntries(form)) })) }
    catch (error) { setTracking(null); setNotice({ kind: 'error', text: error.message }) }
    finally { setLoading(false) }
  }

  const validateRuc = async (event) => {
    const ruc = event.target.value.replace(/\D/g, '').slice(0, 11); event.target.value = ruc; setSunat(null)
    if (ruc.length !== 11) return
    setLoading(true); setNotice(null)
    try { setSunat(await call(`/sunat/ruc/${ruc}`)) }
    catch (error) { setNotice({ kind: 'error', text: error.message }) }
    finally { setLoading(false) }
  }

  return <div className="app">
    <header className="topbar"><div className="container nav">
      <button className="brand" onClick={() => setView('home')} aria-label="Ir al inicio"><span className="crest">M</span><span>Municipalidad Provincial<small>Trámites digitales</small></span></button>
      <nav aria-label="Navegación principal"><button onClick={() => setView('info')}>Información</button><button onClick={() => setView('track')}>Consulta tu trámite</button><button className="outline" onClick={() => setView(session?.user?.roles?.includes('ADMINISTRADOR') ? 'admin' : 'login')}>{session ? session.user.nombres : 'Ingresar'}</button></nav>
    </div></header>

    {notice && <div className={`notice ${notice.kind}`} role="alert">{notice.text}<button onClick={() => setNotice(null)}>×</button></div>}

    {view === 'home' && <main>
      <section className="hero"><div className="container hero-grid"><div>
        <span className="eyebrow">Licencias de funcionamiento</span><h1>Abre las puertas de tu negocio con un trámite más simple</h1>
        <p>Registra, paga y consulta tu solicitud desde cualquier lugar. Te acompañamos en cada etapa con información clara y segura.</p>
        <div className="actions"><button className="primary" onClick={() => setView('apply')}>Iniciar solicitud <span>→</span></button><button className="secondary" onClick={() => setView('track')}>Consultar expediente</button></div>
        <div className="trust"><span>✓ Validación con SUNAT</span><span>✓ Seguimiento en línea</span><span>✓ Pago seguro</span></div>
      </div><div className="hero-card" aria-hidden="true"><div className="card-head"><span>Tu licencia</span><b>Proceso digital</b></div><div className="illustration"><div className="building">▦</div><div className="check">✓</div></div><div className="progress"><i></i><i></i><i></i><i></i></div><strong>Todo listo para empezar</strong><small>Información y trazabilidad en un solo lugar</small></div></div></section>
      <section className="quick"><div className="container quick-grid"><article><span>01</span><div><b>¿Qué necesitas?</b><p>Conoce requisitos y documentos.</p></div><button onClick={() => setView('info')}>Ver requisitos →</button></article><article><span>02</span><div><b>¿Ya tienes expediente?</b><p>Revisa el avance de tu trámite.</p></div><button onClick={() => setView('track')}>Consultar ahora →</button></article><article><span>03</span><div><b>Atención presencial</b><p>También puedes visitarnos.</p></div><button onClick={() => setView('info')}>Ver horarios →</button></article></div></section>
      <section className="process container"><span className="eyebrow">Así de fácil</span><h2>Tu licencia en cuatro etapas</h2><div className="step-grid">{steps.map((step, index) => <article key={step}><span>{String(index + 1).padStart(2, '0')}</span><div className="step-icon">{['✎','▣','⌕','✓'][index]}</div><h3>{step}</h3><p>{['Completa tus datos y adjunta los requisitos.','Validamos tu RUC y confirmamos el pago.','Revisamos e inspeccionamos cuando corresponda.','Descarga tu licencia desde el portal.'][index]}</p></article>)}</div></section>
      <section className="cta"><div className="container"><div><span className="eyebrow light">Empieza hoy</span><h2>¿Listo para formalizar tu negocio?</h2><p>Completa tu solicitud en pocos minutos y sigue el avance en línea.</p></div><button onClick={() => setView('apply')}>Iniciar mi solicitud →</button></div></section>
    </main>}

    {view === 'info' && <Page title="Información del trámite" subtitle="Todo lo que debes conocer antes de iniciar."><div className="content-grid"><section className="panel"><h2>Requisitos generales</h2><ul className="requirements">{requirements.map(x => <li key={x}><span>✓</span>{x}</li>)}</ul></section><aside className="panel facts"><div><small>Derecho de trámite desde</small><strong>S/ 180.00</strong></div><div><small>Plazo estimado</small><strong>Hasta 10 días hábiles</strong></div><div><small>Modalidades</small><strong>Nueva, renovación y modificación</strong></div></aside></div></Page>}

    {view === 'track' && <Page title="Consulta tu trámite" subtitle="Ingresa los datos entregados al registrar tu solicitud."><form className="form panel compact" onSubmit={submitTracking}><label>RUC<input name="ruc" inputMode="numeric" pattern="20[0-9]{9}" maxLength="11" required placeholder="20XXXXXXXXX" /></label><label>Código seguro de expediente<input name="codigo" required placeholder="Ej. A1B2C3D4" /></label><button className="primary" disabled={loading}>{loading ? 'Consultando…' : 'Consultar estado'}</button></form>{tracking && <section className="panel result"><span className="status">{tracking.estado}</span><h2>{tracking.numeroExpediente}</h2><p>Registrado el {new Date(tracking.creadoEn).toLocaleDateString('es-PE')}</p><div className="timeline">{tracking.historial?.map(x => <div key={x.id}><i></i><b>{x.estado}</b><small>{x.comentario}</small></div>)}</div></section>}</Page>}

    {view === 'apply' && <Page title="Nueva solicitud" subtitle="Primero validaremos los datos oficiales de tu empresa."><form className="form panel wide" onSubmit={(e) => { e.preventDefault(); if (!sunat) setNotice({kind:'error', text:'Valida un RUC apto antes de continuar.'}); else setNotice({kind:'success', text:'RUC validado. Para guardar el expediente, ingresa o crea una cuenta.'}) }}><div className="form-grid"><label>RUC de persona jurídica<input name="ruc" inputMode="numeric" onChange={validateRuc} required placeholder="20XXXXXXXXX" /></label><label>Tipo de solicitud<select name="tipo"><option>Nueva licencia</option><option>Renovación</option><option>Modificación</option></select></label></div>{sunat && <div className="sunat"><span>✓ Datos validados con SUNAT</span><div><label>Razón social<input readOnly value={sunat.razonSocial} /></label><label>Estado y condición<input readOnly value={`${sunat.estado} · ${sunat.condicion}`} /></label><label className="full">Domicilio fiscal<input readOnly value={sunat.direccion} /></label></div></div>}<button className="primary" disabled={loading}>{loading ? 'Validando…' : 'Continuar'}</button></form></Page>}

    {view === 'login' && <Page title="Accede a tu cuenta" subtitle="Ciudadanos y personal municipal.">{session ? <section className="panel compact result"><span className="status">{session.user.roles.join(', ')}</span><h2>Hola, {session.user.nombres}</h2><p>Tu sesión está activa y protegida con JWT.</p><button className="secondary" onClick={() => { sessionStorage.removeItem('mpct-session'); setSession(null) }}>Cerrar sesión</button></section> : <form className="form panel compact" onSubmit={submitLogin}><label>Correo electrónico<input name="email" type="email" required /></label><label>Contraseña<input name="password" type="password" required /></label><button className="primary" disabled={loading}>{loading ? 'Ingresando…' : 'Ingresar'}</button><button type="button" className="link">Crear una cuenta ciudadana</button></form>}</Page>}

    {view === 'admin' && session?.user?.roles?.includes('ADMINISTRADOR') && <Page title="Panel administrativo" subtitle="Configuración y operación del sistema municipal."><div className="admin-grid"><aside className="panel admin-menu"><b>Administración</b><button className="active">Padrón SUNAT</button><button disabled>Solicitudes</button><button disabled>Usuarios y roles</button><button disabled>Inspecciones</button><button disabled>Cajas y tarifas</button><button onClick={() => { sessionStorage.removeItem('mpct-session'); setSession(null); setView('home') }}>Cerrar sesión</button></aside><section className="panel admin-content"><span className="eyebrow">Fuente oficial</span><h2>Importar Padrón Reducido SUNAT</h2><p>Selecciona un archivo TXT delimitado por <code>|</code>. La importación actualiza razón social, estado, condición, ubigeo y domicilio fiscal.</p><div className="warning-box"><b>Importante</b><span>Para Render gratuito usa un archivo filtrado de Trujillo. No subas directamente el ZIP nacional de 389 MB.</span></div><form className="form upload-form" onSubmit={importSunat}><label>Archivo oficial TXT<input name="archivo" type="file" accept=".txt,text/plain" required /></label><button className="primary" disabled={loading}>{loading ? 'Importando… no cierres esta página' : 'Importar padrón'}</button></form>{importResult && <div className="import-result"><span>✓</span><div><b>Importación terminada</b><p>{importResult.registros} registro(s) procesados correctamente.</p></div></div>}</section></div></Page>}

    <footer><div className="container"><div className="brand"><span className="crest">M</span><span>Portal de trámites<small>Licencias de funcionamiento</small></span></div><p>Servicio digital municipal · Atención segura y transparente</p><div><button>Privacidad</button><button>Accesibilidad</button><button>Ayuda</button></div></div></footer>
  </div>
}

function Page({ title, subtitle, children }) { return <main className="page"><div className="page-title"><div className="container"><span className="eyebrow">Portal digital</span><h1>{title}</h1><p>{subtitle}</p></div></div><div className="container page-body">{children}</div></main> }
export default App
