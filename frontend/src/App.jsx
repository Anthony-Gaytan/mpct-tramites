import { useCallback, useEffect, useState } from "react";
import "./App.css";
import { CashierPortal, CitizenPortal, InspectorPortal } from "./RolePortals";

const API = import.meta.env.VITE_API_URL || "/api";
const steps = [
  "Registro de solicitud",
  "Validación y pago",
  "Evaluación técnica",
  "Emisión de licencia",
];
const requirements = [
  "Solicitud con datos del representante legal",
  "Vigencia de poder y documento de identidad",
  "Plano o croquis de ubicación",
  "Declaración jurada de seguridad",
];

async function call(path, options = {}) {
  const response = await fetch(`${API}${path}`, {
    ...options,
    headers: { "Content-Type": "application/json", ...(options.headers || {}) },
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok)
    throw new Error(
      data.message || data.title || "No pudimos completar la operación.",
    );
  return data;
}

function App() {
  const [view, setView] = useState("home");
  const [notice, setNotice] = useState(null);
  const [tracking, setTracking] = useState(null);
  const [sunat, setSunat] = useState(null);
  const [loading, setLoading] = useState(false);
  const [session, setSession] = useState(() =>
    JSON.parse(sessionStorage.getItem("mpct-session") || "null"),
  );
  const [adminTab, setAdminTab] = useState("usuarios");
  const [staff, setStaff] = useState([]);
  const [requestPhase, setRequestPhase] = useState("");
  const [businessType, setBusinessType] = useState("");
  const portalNotify = useCallback(
    (text, kind = "error") => setNotice({ text, kind }),
    [],
  );

  const submitLogin = async (event) => {
    event.preventDefault();
    setLoading(true);
    setNotice(null);
    const form = new FormData(event.currentTarget);
    try {
      const data = await call("/auth/login", {
        method: "POST",
        body: JSON.stringify(Object.fromEntries(form)),
      });
      sessionStorage.setItem("mpct-session", JSON.stringify(data));
      setSession(data);
      const role = data.user.roles[0];
      setView(
        role === "ADMINISTRADOR"
          ? "admin"
          : role === "CAJERO"
            ? "cashier"
            : role === "INSPECTOR"
              ? "inspector"
              : "citizen",
      );
      setNotice({
        kind: "success",
        text: `Bienvenido, ${data.user.nombres}. Rol: ${data.user.roles.join(", ")}`,
      });
    } catch (error) {
      setNotice({ kind: "error", text: error.message });
    } finally {
      setLoading(false);
    }
  };

  const submitRegister = async (event) => {
    event.preventDefault();
    setLoading(true);
    setNotice(null);
    try {
      const data = await call("/auth/registro", {
        method: "POST",
        body: JSON.stringify(
          Object.fromEntries(new FormData(event.currentTarget)),
        ),
      });
      sessionStorage.setItem("mpct-session", JSON.stringify(data));
      setSession(data);
      setView("citizen");
      setNotice({
        kind: "success",
        text: "Cuenta ciudadana creada correctamente.",
      });
    } catch (error) {
      setNotice({ kind: "error", text: error.message });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (view !== "admin" || !session?.token) return;
    call("/admin/usuarios", {
      headers: { Authorization: `Bearer ${session.token}` },
    })
      .then(setStaff)
      .catch((error) => setNotice({ kind: "error", text: error.message }));
  }, [view, session?.token]);

  const createStaff = async (event) => {
    event.preventDefault();
    setLoading(true);
    setNotice(null);
    try {
      const item = await call("/admin/usuarios", {
        method: "POST",
        headers: { Authorization: `Bearer ${session.token}` },
        body: JSON.stringify(
          Object.fromEntries(new FormData(event.currentTarget)),
        ),
      });
      setStaff((current) => [...current, item]);
      event.currentTarget.reset();
      setNotice({
        kind: "success",
        text: "Usuario municipal creado correctamente.",
      });
    } catch (error) {
      setNotice({ kind: "error", text: error.message });
    } finally {
      setLoading(false);
    }
  };

  const toggleStaff = async (user) => {
    const motivo = user.activo
      ? window.prompt("Indica el motivo de la suspensión:")
      : null;
    if (user.activo && !motivo?.trim()) return;
    try {
      const result = await call(`/admin/usuarios/${user.id}/estado`, {
        method: "PATCH",
        headers: { Authorization: `Bearer ${session.token}` },
        body: JSON.stringify({ activo: !user.activo, motivo }),
      });
      setStaff((current) =>
        current.map((x) => (x.id === user.id ? { ...x, ...result } : x)),
      );
    } catch (error) {
      setNotice({ kind: "error", text: error.message });
    }
  };

  const resetStaffPassword = async (user) => {
    const passwordNueva = window.prompt(
      `Nueva contraseña temporal para ${user.nombres}:`,
    );
    if (!passwordNueva) return;
    try {
      const result = await call(`/admin/usuarios/${user.id}/password`, {
        method: "PATCH",
        headers: { Authorization: `Bearer ${session.token}` },
        body: JSON.stringify({ passwordNueva }),
      });
      setNotice({ kind: "success", text: result.message });
    } catch (error) {
      setNotice({ kind: "error", text: error.message });
    }
  };

  const updateProfile = async (event) => {
    event.preventDefault();
    setLoading(true);
    setNotice(null);
    try {
      const data = await call("/admin/perfil", {
        method: "PUT",
        headers: { Authorization: `Bearer ${session.token}` },
        body: JSON.stringify(
          Object.fromEntries(new FormData(event.currentTarget)),
        ),
      });
      sessionStorage.removeItem("mpct-session");
      setSession(null);
      setView("login");
      setNotice({ kind: "success", text: data.message });
    } catch (error) {
      setNotice({ kind: "error", text: error.message });
    } finally {
      setLoading(false);
    }
  };

  const submitTracking = async (event) => {
    event.preventDefault();
    setLoading(true);
    setNotice(null);
    const form = new FormData(event.currentTarget);
    try {
      setTracking(
        await call("/solicitudes/seguimiento", {
          method: "POST",
          body: JSON.stringify(Object.fromEntries(form)),
        }),
      );
    } catch (error) {
      setTracking(null);
      setNotice({ kind: "error", text: error.message });
    } finally {
      setLoading(false);
    }
  };

  const validateRuc = async (event) => {
    const ruc = event.target.value.replace(/\D/g, "").slice(0, 11);
    event.target.value = ruc;
    setSunat(null);
    if (ruc.length !== 11) return;
    setLoading(true);
    setNotice(null);
    try {
      setSunat(await call(`/sunat/ruc/${ruc}`));
    } catch (error) {
      setNotice({ kind: "error", text: error.message });
    } finally {
      setLoading(false);
    }
  };

  const submitRequest = async (event) => {
    event.preventDefault();
    if (!sunat)
      return setNotice({
        kind: "error",
        text: "Valida un RUC apto antes de continuar.",
      });
    setLoading(true);
    setRequestPhase("Registrando solicitud…");
    setNotice(null);
    try {
      const formData = new FormData(event.currentTarget);
      const archivo = formData.get("archivo");
      const values = Object.fromEntries(formData);
      delete values.archivo;
      if (values.rubro === "Otro") values.rubro = values.rubroOtro?.trim();
      delete values.rubroOtro;
      values.actividad = values.rubro;
      const result = await call("/solicitudes", {
        method: "POST",
        headers: session?.token ? { Authorization: `Bearer ${session.token}` } : {},
        body: JSON.stringify({
          ...values,
          tipo: Number(values.tipo),
          areaMetrosCuadrados: Number(values.areaMetrosCuadrados),
        }),
      });
      if (archivo?.size) {
        setRequestPhase("Subiendo documentos…");
        const upload = new FormData();
        upload.append("archivo", archivo);
        upload.append("tipo", "PLANO_DISTRIBUCION_RIESGOS");
        const response = await fetch(
          `${API}/solicitudes/${result.id}/documentos?codigo=${encodeURIComponent(result.uploadToken)}`,
          {
            method: "POST",
            headers: session?.token ? { Authorization: `Bearer ${session.token}` } : {},
            body: upload,
          },
        );
        if (!response.ok) {
          const data = await response.json().catch(() => ({}));
          throw new Error(
            data.message ||
              "La solicitud se creó, pero no se pudo adjuntar el documento.",
          );
        }
      }
      setNotice({
        kind: "success",
        text: `Solicitud ${result.numeroExpediente} registrada correctamente. Podrás consultarla usando tu RUC.`,
      });
      setView(session?.user?.roles?.includes("CIUDADANO") ? "citizen" : "track");
    } catch (error) {
      setNotice({ kind: "error", text: error.message });
    } finally {
      setLoading(false);
      setRequestPhase("");
    }
  };

  return (
    <div className="app">
      <header className="topbar">
        <div className="container nav">
          <button
            className="brand"
            onClick={() => setView("home")}
            aria-label="Ir al inicio"
          >
            <span className="crest">M</span>
            <span>
              Municipalidad Provincial<small>Trámites digitales</small>
            </span>
          </button>
          <nav aria-label="Navegación principal">
            <button onClick={() => setView("info")}>Información</button>
            <button onClick={() => setView("track")}>
              Consulta tu trámite
            </button>
            <button
              className="outline"
              onClick={() =>
                setView(
                  session?.user?.roles?.includes("ADMINISTRADOR")
                    ? "admin"
                    : session?.user?.roles?.includes("CAJERO")
                      ? "cashier"
                      : session?.user?.roles?.includes("INSPECTOR")
                        ? "inspector"
                        : session
                          ? "citizen"
                          : "login",
                )
              }
            >
              {session ? session.user.nombres : "Ingresar"}
            </button>
          </nav>
        </div>
      </header>

      {notice && (
        <div className={`notice ${notice.kind}`} role="alert">
          {notice.text}
          <button onClick={() => setNotice(null)}>×</button>
        </div>
      )}

      {view === "home" && (
        <main>
          <section className="hero">
            <div className="container hero-grid">
              <div>
                <span className="eyebrow">Licencias de funcionamiento</span>
                <h1>
                  Abre las puertas de tu negocio con un trámite más simple
                </h1>
                <p>
                  Registra, paga y consulta tu solicitud desde cualquier lugar.
                  Te acompañamos en cada etapa con información clara y segura.
                </p>
                <div className="actions">
                  <button className="primary" onClick={() => setView("apply")}>
                    Iniciar solicitud <span>→</span>
                  </button>
                  <button
                    className="secondary"
                    onClick={() => setView("track")}
                  >
                    Consultar expediente
                  </button>
                </div>
                <div className="trust">
                  <span>✓ Validación con SUNAT</span>
                  <span>✓ Seguimiento en línea</span>
                  <span>✓ Pago seguro</span>
                </div>
              </div>
              <div className="hero-card" aria-hidden="true">
                <div className="card-head">
                  <span>Tu licencia</span>
                  <b>Proceso digital</b>
                </div>
                <div className="illustration">
                  <div className="building">▦</div>
                  <div className="check">✓</div>
                </div>
                <div className="progress">
                  <i></i>
                  <i></i>
                  <i></i>
                  <i></i>
                </div>
                <strong>Todo listo para empezar</strong>
                <small>Información y trazabilidad en un solo lugar</small>
              </div>
            </div>
          </section>
          <section className="quick">
            <div className="container quick-grid">
              <article>
                <span>01</span>
                <div>
                  <b>¿Qué necesitas?</b>
                  <p>Conoce requisitos y documentos.</p>
                </div>
                <button onClick={() => setView("info")}>
                  Ver requisitos →
                </button>
              </article>
              <article>
                <span>02</span>
                <div>
                  <b>¿Ya tienes expediente?</b>
                  <p>Revisa el avance de tu trámite.</p>
                </div>
                <button onClick={() => setView("track")}>
                  Consultar ahora →
                </button>
              </article>
              <article>
                <span>03</span>
                <div>
                  <b>Atención presencial</b>
                  <p>También puedes visitarnos.</p>
                </div>
                <button onClick={() => setView("info")}>Ver horarios →</button>
              </article>
            </div>
          </section>
          <section className="process container">
            <span className="eyebrow">Así de fácil</span>
            <h2>Tu licencia en cuatro etapas</h2>
            <div className="step-grid">
              {steps.map((step, index) => (
                <article key={step}>
                  <span>{String(index + 1).padStart(2, "0")}</span>
                  <div className="step-icon">{["✎", "▣", "⌕", "✓"][index]}</div>
                  <h3>{step}</h3>
                  <p>
                    {
                      [
                        "Completa tus datos y adjunta los requisitos.",
                        "Validamos tu RUC y confirmamos el pago.",
                        "Revisamos e inspeccionamos cuando corresponda.",
                        "Descarga tu licencia desde el portal.",
                      ][index]
                    }
                  </p>
                </article>
              ))}
            </div>
          </section>
          <section className="cta">
            <div className="container">
              <div>
                <span className="eyebrow light">Empieza hoy</span>
                <h2>¿Listo para formalizar tu negocio?</h2>
                <p>
                  Completa tu solicitud en pocos minutos y sigue el avance en
                  línea.
                </p>
              </div>
              <button onClick={() => setView("apply")}>
                Iniciar mi solicitud →
              </button>
            </div>
          </section>
        </main>
      )}

      {view === "info" && (
        <Page
          title="Información del trámite"
          subtitle="Todo lo que debes conocer antes de iniciar."
        >
          <div className="content-grid">
            <section className="panel">
              <h2>Requisitos generales</h2>
              <ul className="requirements">
                {requirements.map((x) => (
                  <li key={x}>
                    <span>✓</span>
                    {x}
                  </li>
                ))}
              </ul>
            </section>
            <aside className="panel facts">
              <div>
                <small>Derecho de trámite desde</small>
                <strong>S/ 180.00</strong>
              </div>
              <div>
                <small>Plazo estimado</small>
                <strong>Hasta 10 días hábiles</strong>
              </div>
              <div>
                <small>Modalidades</small>
                <strong>Nueva, renovación y modificación</strong>
              </div>
            </aside>
          </div>
        </Page>
      )}

      {view === "track" && (
        <Page
          title="Consulta tu trámite"
          subtitle="Ingresa los datos entregados al registrar tu solicitud."
        >
          <form className="form panel compact" onSubmit={submitTracking}>
            <label>
              RUC
              <input
                name="ruc"
                inputMode="numeric"
                pattern="20[0-9]{9}"
                maxLength="11"
                required
                placeholder="20XXXXXXXXX"
              />
            </label>
            <button className="primary" disabled={loading}>
              {loading ? "Consultando…" : "Consultar estado"}
            </button>
          </form>
          {tracking && (
            <section className="panel result">
              <span className="status">{tracking.estado}</span>
              <h2>{tracking.numeroExpediente}</h2>
              <p>
                Registrado el{" "}
                {new Date(tracking.creadoEn).toLocaleDateString("es-PE")}
              </p>
              <div className="timeline">
                {tracking.historial?.map((x) => (
                  <div key={x.id}>
                    <i></i>
                    <b>{x.estado}</b>
                    <small>{x.comentario}</small>
                  </div>
                ))}
              </div>
            </section>
          )}
        </Page>
      )}

      {view === "apply" && (
        <Page
          title="Nueva solicitud"
          subtitle="Completa los datos para iniciar tu Licencia de Funcionamiento. Validaremos tu empresa automáticamente con SUNAT."
        >
          <form
            className="form panel wide request-form"
            onSubmit={submitRequest}
          >
            <div className="form-grid">
              <label>
                RUC de persona jurídica
                <input
                  name="ruc"
                  inputMode="numeric"
                  onChange={validateRuc}
                  required
                  placeholder="20XXXXXXXXX"
                />
              </label>
              <label>
                Tipo de solicitud
                <select name="tipo">
                  <option value="0">Nueva licencia</option>
                  <option value="1">Renovación</option>
                  <option value="2">Modificación</option>
                </select>
              </label>
            </div>
            {sunat && (
              <div className="sunat">
                <span>✓ Datos validados con SUNAT</span>
                <div>
                  <label>
                    Razón social
                    <input readOnly value={sunat.razonSocial} />
                  </label>
                  <label>
                    Estado y condición
                    <input
                      readOnly
                      value={`${sunat.estado} · ${sunat.condicion}`}
                    />
                  </label>
                  <label className="full">
                    Domicilio fiscal
                    <input readOnly value={sunat.direccion} />
                  </label>
                </div>
              </div>
            )}
            {sunat && (
              <>
                <div className="form-grid request-details">
                <label>
                  DNI del representante
                  <input
                    name="representanteDocumento"
                    readOnly
                    value={sunat.representanteDocumento || ""}
                    required
                  />
                  </label>
                  <label>
                    Correo electrónico <small>(opcional)</small>
                    <input
                      name="representanteEmail"
                      type="email"
                      placeholder="correo@ejemplo.com"
                    />
                  </label>
                <label>
                  Nombres y apellidos del titular
                  <input
                    name="representanteNombre"
                    readOnly
                    value={sunat.representanteNombre || ""}
                    required
                  />
                  </label>
                  <label>
                    Rubro del negocio
                    <select name="rubro" required value={businessType} onChange={(event)=>setBusinessType(event.target.value)}>
                      <option value="">Selecciona un rubro</option>
                      <option>Restaurante / Fuente de Soda</option>
                      <option>Bodega / Minimarket</option>
                      <option>Oficina administrativa</option>
                      <option>Comercio minorista</option>
                      <option>Servicios profesionales</option>
                      <option>Hospedaje</option>
                      <option>Otro</option>
                    </select>
                  </label>
                  {businessType === "Otro" && <label>Especifica el rubro<input name="rubroOtro" required placeholder="Escribe la actividad del negocio" /></label>}
                  <label>
                    Área del local (m²)
                    <input
                      name="areaMetrosCuadrados"
                      type="number"
                      min="1"
                      step="0.01"
                      required
                    />
                  </label>
                  <label className="full">
                    Dirección del establecimiento
                    <input
                      name="direccionLocal"
                      required
                      placeholder="Dirección donde funcionará el negocio"
                    />
                  </label>
                </div>
                <section className="document-box">
                  <div>
                    <span className="upload-icon">⇧</span>
                    <div>
                      <b>Documentos adjuntos</b>
                      <small>
                        Plano de distribución y riesgos (PDF o imagen, máximo 10
                        MB)
                      </small>
                    </div>
                  </div>
                  <input
                    name="archivo"
                    type="file"
                    accept="application/pdf,image/jpeg,image/png"
                    required
                  />
                </section>
              </>
            )}
            <button className="primary" disabled={loading}>
              {requestPhase || (loading ? "Validando…" : "Registrar solicitud")}
            </button>
          </form>
        </Page>
      )}

      {view === "login" && (
        <Page
          title="Accede a tu cuenta"
          subtitle="Ciudadanos y personal municipal."
        >
          {session ? (
            <section className="panel compact result">
              <span className="status">{session.user.roles.join(", ")}</span>
              <h2>Hola, {session.user.nombres}</h2>
              <p>Tu sesión está activa y protegida con JWT.</p>
              <button
                className="secondary"
                onClick={() => {
                  sessionStorage.removeItem("mpct-session");
                  setSession(null);
                }}
              >
                Cerrar sesión
              </button>
            </section>
          ) : (
            <form className="form panel compact" onSubmit={submitLogin}>
              <label>
                Correo electrónico
                <input name="email" type="email" required />
              </label>
              <label>
                Contraseña
                <input name="password" type="password" required />
              </label>
              <button className="primary" disabled={loading}>
                {loading ? "Ingresando…" : "Ingresar"}
              </button>
              <button
                type="button"
                className="link"
                onClick={() => setView("register")}
              >
                Crear una cuenta ciudadana
              </button>
            </form>
          )}
        </Page>
      )}

      {view === "register" && (
        <Page
          title="Crea tu cuenta"
          subtitle="Regístrate para presentar y consultar solicitudes."
        >
          <form className="form panel compact" onSubmit={submitRegister}>
            <label>
              Nombres
              <input name="nombres" required />
            </label>
            <label>
              Apellidos
              <input name="apellidos" required />
            </label>
            <label>
              Correo electrónico
              <input name="email" type="email" required />
            </label>
            <label>
              Contraseña
              <input
                name="password"
                type="password"
                minLength="10"
                required
                placeholder="Incluye mayúscula, número y símbolo"
              />
            </label>
            <button className="primary" disabled={loading}>
              {loading ? "Creando…" : "Crear cuenta"}
            </button>
          </form>
        </Page>
      )}

      {view === "citizen" && session?.user?.roles?.includes("CIUDADANO") && (
        <CitizenPortal
          session={session}
          onNew={() => setView("apply")}
          notify={portalNotify}
        />
      )}
      {view === "cashier" && session?.user?.roles?.includes("CAJERO") && (
        <CashierPortal session={session} notify={portalNotify} />
      )}
      {view === "inspector" && session?.user?.roles?.includes("INSPECTOR") && (
        <InspectorPortal session={session} notify={portalNotify} />
      )}

      {view === "admin" && session?.user?.roles?.includes("ADMINISTRADOR") && (
        <Page
          title="Panel administrativo"
          subtitle="Administra al personal y la seguridad de tu cuenta."
        >
          <div className="admin-grid">
            <aside className="panel admin-menu">
              <b>Administración</b>
              <button
                className={adminTab === "usuarios" ? "active" : ""}
                onClick={() => setAdminTab("usuarios")}
              >
                Usuarios y roles
              </button>
              <button
                className={adminTab === "perfil" ? "active" : ""}
                onClick={() => setAdminTab("perfil")}
              >
                Mi cuenta
              </button>
              <button disabled>Solicitudes</button>
              <button disabled>Inspecciones</button>
              <button disabled>Cajas y tarifas</button>
              <button
                onClick={() => {
                  sessionStorage.removeItem("mpct-session");
                  setSession(null);
                  setView("home");
                }}
              >
                Cerrar sesión
              </button>
            </aside>
            <section className="panel admin-content">
              {adminTab === "usuarios" ? (
                <>
                  <span className="eyebrow">Personal municipal</span>
                  <h2>Usuarios y roles</h2>
                  <form className="form staff-form" onSubmit={createStaff}>
                    <div className="form-grid">
                      <label>
                        Nombres
                        <input name="nombres" required />
                      </label>
                      <label>
                        Apellidos
                        <input name="apellidos" required />
                      </label>
                      <label>
                        Correo institucional
                        <input name="email" type="email" required />
                      </label>
                      <label>
                        Rol
                        <select name="rol">
                          <option value="CAJERO">Cajero</option>
                          <option value="INSPECTOR">Inspector</option>
                        </select>
                      </label>
                      <label className="full">
                        Contraseña temporal
                        <input
                          name="password"
                          type="password"
                          minLength="10"
                          required
                          placeholder="Mínimo 10 caracteres"
                        />
                      </label>
                    </div>
                    <button className="primary" disabled={loading}>
                      {loading ? "Creando…" : "Crear usuario"}
                    </button>
                  </form>
                  <div className="staff-list">
                    {staff.map((user) => (
                      <article key={user.id}>
                        <div>
                          <b>
                            {user.nombres} {user.apellidos}
                          </b>
                          <span>{user.email}</span>
                        </div>
                        <span className="role-badge">
                          {user.roles?.join(", ")}
                        </span>
                        <div className="staff-actions">
                          {!user.roles?.includes("ADMINISTRADOR") && (
                            <button
                              className="secondary"
                              onClick={() => resetStaffPassword(user)}
                            >
                              Contraseña
                            </button>
                          )}
                          <button
                            className={
                              user.activo ? "danger-link" : "secondary"
                            }
                            onClick={() => toggleStaff(user)}
                          >
                            {user.activo ? "Desactivar" : "Activar"}
                          </button>
                        </div>
                        {!user.activo && user.motivoSuspension && (
                          <small className="suspension-reason">
                            Suspendido: {user.motivoSuspension}
                          </small>
                        )}
                      </article>
                    ))}
                  </div>
                </>
              ) : (
                <>
                  <span className="eyebrow">Seguridad</span>
                  <h2>Mi cuenta de administrador</h2>
                  <form className="form" onSubmit={updateProfile}>
                    <div className="form-grid">
                      <label>
                        Nombres
                        <input
                          name="nombres"
                          defaultValue={session.user.nombres}
                          required
                        />
                      </label>
                      <label>
                        Apellidos
                        <input
                          name="apellidos"
                          defaultValue={session.user.apellidos || ""}
                          required
                        />
                      </label>
                      <label className="full">
                        Nuevo correo
                        <input
                          name="email"
                          type="email"
                          defaultValue={session.user.email || ""}
                          required
                        />
                      </label>
                      <label>
                        Contraseña actual
                        <input name="passwordActual" type="password" required />
                      </label>
                      <label>
                        Nueva contraseña (opcional)
                        <input
                          name="passwordNueva"
                          type="password"
                          minLength="10"
                        />
                      </label>
                    </div>
                    <button className="primary" disabled={loading}>
                      Guardar cambios
                    </button>
                  </form>
                </>
              )}
            </section>
          </div>
        </Page>
      )}

      <footer>
        <div className="container">
          <div className="brand">
            <span className="crest">M</span>
            <span>
              Portal de trámites<small>Licencias de funcionamiento</small>
            </span>
          </div>
          <p>Servicio digital municipal · Atención segura y transparente</p>
          <div>
            <button>Privacidad</button>
            <button>Accesibilidad</button>
            <button>Ayuda</button>
          </div>
        </div>
      </footer>
    </div>
  );
}

function Page({ title, subtitle, children }) {
  return (
    <main className="page">
      <div className="page-title">
        <div className="container">
          <span className="eyebrow">Portal digital</span>
          <h1>{title}</h1>
          <p>{subtitle}</p>
        </div>
      </div>
      <div className="container page-body">{children}</div>
    </main>
  );
}
export default App;
