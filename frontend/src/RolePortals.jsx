import { useCallback, useEffect, useState } from "react";
const API = import.meta.env.VITE_API_URL || "/api";
async function request(path, session, options = {}) {
  const response = await fetch(`${API}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${session.token}`,
      ...(options.headers || {}),
    },
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok)
    throw new Error(
      data.message || data.title || "No se pudo completar la operación.",
    );
  return data;
}
export function CitizenPortal({ session, onNew, notify }) {
  const [items, setItems] = useState([]);
  useEffect(() => {
    request("/solicitudes/mias", session)
      .then(setItems)
      .catch((e) => notify(e.message));
  }, [session, notify]);
  return (
    <Portal
      title="Mis trámites"
      subtitle="Consulta tus solicitudes y continúa sus pagos."
    >
      <button className="primary" onClick={onNew}>
        Nueva solicitud
      </button>
      <div className="portal-list">
        {items.length ? (
          items.map((x) => (
            <article key={x.id}>
              <div>
                <b>{x.numeroExpediente}</b>
                <span>
                  {x.razonSocial} · RUC {x.ruc}
                </span>
              </div>
              <span className="status">{x.estado}</span>
              <strong>S/ {Number(x.tarifa).toFixed(2)}</strong>
            </article>
          ))
        ) : (
          <Empty text="Todavía no tienes solicitudes registradas." />
        )}
      </div>
    </Portal>
  );
}
export function CashierPortal({ session, notify }) {
  const [status, setStatus] = useState(null),
    [found, setFound] = useState(null);
  const refresh = useCallback(() =>
    request("/cajas/estado", session)
      .then(setStatus)
      .catch((e) => notify(e.message)), [session, notify]);
  useEffect(() => { refresh(); }, [refresh]);
  const open = async (e) => {
    e.preventDefault();
    try {
      await request("/cajas/abrir", session, {
        method: "POST",
        body: JSON.stringify({
          montoInicial: Number(
            new FormData(e.currentTarget).get("montoInicial"),
          ),
        }),
      });
      refresh();
    } catch (error) {
      notify(error.message);
    }
  };
  const search = async (e) => {
    e.preventDefault();
    try {
      setFound(
        await request(
          `/cajas/solicitud/${new FormData(e.currentTarget).get("ruc")}`,
          session,
        ),
      );
    } catch (error) {
      setFound(null);
      notify(error.message);
    }
  };
  const pay = async () => {
    try {
      await request(`/cajas/${status.caja.id}/pagos`, session, {
        method: "POST",
        body: JSON.stringify({
          solicitudId: found.id,
          medio: 0,
          monto: found.tarifa,
        }),
      });
      setFound(null);
      notify("Pago registrado correctamente.", "success");
    } catch (error) {
      notify(error.message);
    }
  };
  return (
    <Portal
      title="Módulo de caja"
      subtitle="Apertura de caja y cobros presenciales."
    >
      {!status?.abierta ? (
        <form className="form portal-form" onSubmit={open}>
          <label>
            Monto inicial
            <input
              name="montoInicial"
              type="number"
              min="0"
              step="0.01"
              required
            />
          </label>
          <button className="primary">Abrir caja</button>
        </form>
      ) : (
        <>
          <div className="operation-banner">
            Caja abierta desde{" "}
            {new Date(status.caja.abiertaEn).toLocaleString("es-PE")}
          </div>
          <form className="form portal-form" onSubmit={search}>
            <label>
              Buscar solicitud por RUC
              <input name="ruc" pattern="20[0-9]{9}" maxLength="11" required />
            </label>
            <button className="secondary">Buscar</button>
          </form>
          {found && (
            <article className="payment-card">
              <div>
                <b>{found.razonSocial}</b>
                <span>{found.numeroExpediente}</span>
              </div>
              <strong>S/ {Number(found.tarifa).toFixed(2)}</strong>
              <button className="primary" onClick={pay}>
                Registrar pago
              </button>
            </article>
          )}
        </>
      )}
    </Portal>
  );
}
export function InspectorPortal({ session, notify }) {
  const [items, setItems] = useState([]);
  const load = useCallback(() =>
    request("/inspecciones/mias", session)
      .then(setItems)
      .catch((e) => notify(e.message)), [session, notify]);
  useEffect(() => { load(); }, [load]);
  const finish = async (item) => {
    const observaciones =
      window.prompt("Observaciones de la inspección:") ?? "";
    try {
      await request(`/inspecciones/${item.i.id}/resultado`, session, {
        method: "PUT",
        body: JSON.stringify({
          estado: "FINALIZADA",
          observaciones,
          respuestasJson: "{}",
          latitud: null,
          longitud: null,
          firmar: true,
        }),
      });
      load();
      notify("Inspección finalizada.", "success");
    } catch (error) {
      notify(error.message);
    }
  };
  return (
    <Portal
      title="Panel del inspector"
      subtitle="Inspecciones asignadas y evaluación técnica."
    >
      <div className="portal-list">
        {items.length ? (
          items.map((x) => (
            <article key={x.i.id}>
              <div>
                <b>{x.numeroExpediente}</b>
                <span>
                  {x.rubro} · {x.direccionLocal}
                </span>
              </div>
              <span className="status">{x.i.estado}</span>
              <button className="secondary" onClick={() => finish(x)}>
                Evaluar
              </button>
            </article>
          ))
        ) : (
          <Empty text="No tienes inspecciones asignadas." />
        )}
      </div>
    </Portal>
  );
}
function Portal({ title, subtitle, children }) {
  return (
    <main className="page role-portal">
      <div className="page-title">
        <div className="container">
          <span className="eyebrow">Área privada</span>
          <h1>{title}</h1>
          <p>{subtitle}</p>
        </div>
      </div>
      <div className="container page-body">{children}</div>
    </main>
  );
}
function Empty({ text }) {
  return <div className="empty-state">{text}</div>;
}
