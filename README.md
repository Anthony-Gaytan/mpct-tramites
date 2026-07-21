# MPCT Trámites

Sistema web para licencias de funcionamiento virtuales y presenciales. Incluye portal ciudadano, validación con el Padrón Reducido SUNAT, expedientes, documentos, caja, inspecciones, administración, auditoría y base PostgreSQL.

## Desarrollo local

Requisitos: .NET SDK 8, Node.js 22 y PostgreSQL 16.

1. Copiar `.env.example` a `.env` y cambiar todas las claves.
2. Configurar `ConnectionStrings:Postgres` y `Jwt:Key` mediante variables o user-secrets.
3. Ejecutar `dotnet ef database update --project backend/MpctTramites.Api`.
4. Iniciar la API con `dotnet run --project backend/MpctTramites.Api`.
5. En `frontend`, ejecutar `npm install` y `npm run dev`.

La cuenta administradora de desarrollo solo se crea si se definen `Seed__AdminEmail` y `Seed__AdminPassword`; no hay una contraseña embebida en producción.

## Padrón SUNAT

Descargar legalmente el Padrón Reducido desde SUNAT y cargar su archivo delimitado por `|` mediante `POST /api/sunat/padron/importar` con rol `ADMINISTRADOR`. La validación ocurre en backend y exige RUC 20, estado ACTIVO, condición HABIDO y domicilio en la provincia de Trujillo. No se utiliza scraping.

## Mercado Pago

Las variables están preparadas para credenciales sandbox y producción. Nunca se deben exponer tokens en el frontend. Mercado Pago no cobra costo fijo de integración, pero sí aplica comisión en pagos reales según el acuerdo y país. La integración debe probarse con credenciales TEST antes de habilitar producción.

## Contenedores

Con Docker disponible: `docker compose up --build`. El frontend queda en `http://localhost:8080`; PostgreSQL y la API tienen health checks y los archivos se guardan en un volumen persistente.

## Staging gratuito en Render

El archivo `render.yaml` crea un servicio web gratuito y una base PostgreSQL gratuita. Es apropiado para validación, no para producción permanente: la base gratuita de Render expira 30 días después de su creación, tiene 1 GB y no incluye backups. Exporte los datos antes del vencimiento o migre a una base persistente.

## Verificación

- Backend: `dotnet build MpctTramites.sln` y `dotnet test MpctTramites.sln`.
- Frontend: `npm run build` y `npm run lint`.
- Nunca afirmar que una publicación está lista sin ejecutar estas verificaciones y probar las credenciales externas en su entorno correspondiente.

## Seguridad

JWT de corta duración, Identity con bloqueo, autorización por roles, CORS configurable, límites de archivos, hashes SHA-256, rate limiting y registros de auditoría. Use un proxy TLS en producción, almacenamiento de objetos con antivirus para adjuntos y un gestor de secretos del proveedor cloud.
