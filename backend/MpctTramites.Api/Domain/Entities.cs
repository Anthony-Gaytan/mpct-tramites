using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace MpctTramites.Api.Domain;

public enum EstadoSolicitud { Borrador, PendientePago, EnRevision, InspeccionProgramada, Observada, Subsanada, Aprobada, Rechazada, Cancelada }
public enum EstadoPago { Pendiente, Aprobado, Rechazado, Cancelado, Reembolsado }
public enum TipoSolicitud { Nueva, Renovacion, Modificacion }
public enum MedioPago { MercadoPago, Efectivo, Tarjeta, Transferencia, Otro, Yape }

public sealed class Usuario : IdentityUser<Guid>
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public string? MotivoSuspension { get; set; }
    public DateTimeOffset? SuspendidoEn { get; set; }
    public Guid? SuspendidoPorId { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Rol : IdentityRole<Guid> { }

public sealed class Empresa
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string DireccionFiscal { get; set; } = string.Empty;
    public string Ubigeo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Condicion { get; set; } = string.Empty;
    public DateTimeOffset ValidadoEn { get; set; }
    public string FuenteValidacion { get; set; } = string.Empty;
}

public sealed class ValidacionSunat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ruc { get; set; } = string.Empty;
    public bool EsValida { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string Fuente { get; set; } = "Padrón Reducido SUNAT";
    public DateTimeOffset ConsultadoEn { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RegistroPadronSunat
{
    public long Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Condicion { get; set; } = string.Empty;
    public string Ubigeo { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public DateTimeOffset ImportadoEn { get; set; } = DateTimeOffset.UtcNow;
    [NotMapped] public string RepresentanteDocumento { get; set; } = string.Empty;
    [NotMapped] public string RepresentanteNombre { get; set; } = string.Empty;
}

public sealed class Solicitud
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NumeroExpediente { get; set; } = string.Empty;
    public string CodigoSeguimientoHash { get; set; } = string.Empty;
    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public Guid? CiudadanoId { get; set; }
    public TipoSolicitud Tipo { get; set; }
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Borrador;
    public string RepresentanteNombre { get; set; } = string.Empty;
    public string RepresentanteDocumento { get; set; } = string.Empty;
    public string RepresentanteEmail { get; set; } = string.Empty;
    public string Rubro { get; set; } = string.Empty;
    public string Actividad { get; set; } = string.Empty;
    public decimal AreaMetrosCuadrados { get; set; }
    public string DireccionLocal { get; set; } = string.Empty;
    public decimal Tarifa { get; set; }
    public string? Observaciones { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public List<HistorialEstado> Historial { get; set; } = [];
    public List<Documento> Documentos { get; set; } = [];
}

public sealed class HistorialEstado
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SolicitudId { get; set; }
    public EstadoSolicitud Estado { get; set; }
    public string Comentario { get; set; } = string.Empty;
    public Guid? UsuarioId { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Documento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SolicitudId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string NombreOriginal { get; set; } = string.Empty;
    public string NombreAlmacenado { get; set; } = string.Empty;
    public string Mime { get; set; } = string.Empty;
    public long Tamano { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Pago
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SolicitudId { get; set; }
    public Guid? CajaId { get; set; }
    public MedioPago Medio { get; set; }
    public EstadoPago Estado { get; set; } = EstadoPago.Pendiente;
    public decimal Monto { get; set; }
    public string? IdentificadorExterno { get; set; }
    public string? RespuestaAuditada { get; set; }
    public string? VoucherNombre { get; set; }
    public string? VoucherRuta { get; set; }
    public string? MotivoRevision { get; set; }
    public Guid? RevisadoPorId { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmadoEn { get; set; }
}

public sealed class WebhookProcesado { public Guid Id { get; set; } = Guid.NewGuid(); public string Proveedor { get; set; } = string.Empty; public string EventoId { get; set; } = string.Empty; public string HashContenido { get; set; } = string.Empty; public DateTimeOffset ProcesadoEn { get; set; } = DateTimeOffset.UtcNow; }
public sealed class Caja { public Guid Id { get; set; } = Guid.NewGuid(); public Guid CajeroId { get; set; } public decimal MontoInicial { get; set; } public DateTimeOffset AbiertaEn { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset? CerradaEn { get; set; } public bool EstaAbierta { get; set; } = true; }
public sealed class MovimientoCaja { public Guid Id { get; set; } = Guid.NewGuid(); public Guid CajaId { get; set; } public Guid? PagoId { get; set; } public MedioPago Medio { get; set; } public decimal Monto { get; set; } public bool Anulado { get; set; } public string? MotivoAnulacion { get; set; } public Guid? AutorizadoPorId { get; set; } public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow; }
public sealed class CierreCaja { public Guid Id { get; set; } = Guid.NewGuid(); public Guid CajaId { get; set; } public decimal TotalEsperado { get; set; } public decimal TotalDeclarado { get; set; } public string TotalesPorMedioJson { get; set; } = "{}"; public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow; }
public sealed class Inspeccion { public Guid Id { get; set; } = Guid.NewGuid(); public Guid SolicitudId { get; set; } public Guid InspectorId { get; set; } public DateTimeOffset? ProgramadaPara { get; set; } public string Estado { get; set; } = "ASIGNADA"; public string? Observaciones { get; set; } public string? Resultado { get; set; } public string RespuestasJson { get; set; } = "{}"; public decimal? Latitud { get; set; } public decimal? Longitud { get; set; } public DateTimeOffset? FirmadaEn { get; set; } }
public sealed class Evidencia { public Guid Id { get; set; } = Guid.NewGuid(); public Guid InspeccionId { get; set; } public string Ruta { get; set; } = string.Empty; public string Mime { get; set; } = string.Empty; public string Sha256 { get; set; } = string.Empty; public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow; }
public sealed class ItemListaVerificacion { public Guid Id { get; set; } = Guid.NewGuid(); public string Texto { get; set; } = string.Empty; public string Rubro { get; set; } = "GENERAL"; public bool Activo { get; set; } = true; public int Orden { get; set; } }
public sealed class Notificacion { public Guid Id { get; set; } = Guid.NewGuid(); public Guid? UsuarioId { get; set; } public string Destinatario { get; set; } = string.Empty; public string Asunto { get; set; } = string.Empty; public string Cuerpo { get; set; } = string.Empty; public string Estado { get; set; } = "PENDIENTE"; public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow; }
public sealed class Auditoria { public Guid Id { get; set; } = Guid.NewGuid(); public Guid? UsuarioId { get; set; } public string Accion { get; set; } = string.Empty; public string Entidad { get; set; } = string.Empty; public string? EntidadId { get; set; } public string DetalleJson { get; set; } = "{}"; public string? Ip { get; set; } public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow; }
public sealed class Tarifa { public Guid Id { get; set; } = Guid.NewGuid(); public TipoSolicitud Tipo { get; set; } public decimal Monto { get; set; } public bool Activa { get; set; } = true; }
