using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MpctTramites.Api.Domain;

namespace MpctTramites.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<Usuario, Rol, Guid>(options)
{
    public DbSet<Empresa> Empresas => Set<Empresa>(); public DbSet<ValidacionSunat> ValidacionesSunat => Set<ValidacionSunat>(); public DbSet<RegistroPadronSunat> PadronSunat => Set<RegistroPadronSunat>();
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>(); public DbSet<Documento> Documentos => Set<Documento>(); public DbSet<HistorialEstado> HistorialEstados => Set<HistorialEstado>(); public DbSet<Pago> Pagos => Set<Pago>(); public DbSet<WebhookProcesado> WebhooksProcesados => Set<WebhookProcesado>();
    public DbSet<Caja> Cajas => Set<Caja>(); public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>(); public DbSet<CierreCaja> CierresCaja => Set<CierreCaja>(); public DbSet<Inspeccion> Inspecciones => Set<Inspeccion>(); public DbSet<Evidencia> Evidencias => Set<Evidencia>(); public DbSet<ItemListaVerificacion> ItemsListaVerificacion => Set<ItemListaVerificacion>(); public DbSet<Notificacion> Notificaciones => Set<Notificacion>(); public DbSet<Auditoria> Auditorias => Set<Auditoria>(); public DbSet<Tarifa> Tarifas => Set<Tarifa>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Empresa>().HasIndex(x => x.Ruc).IsUnique(); b.Entity<RegistroPadronSunat>().HasIndex(x => x.Ruc).IsUnique(); b.Entity<Solicitud>().HasIndex(x => x.NumeroExpediente).IsUnique(); b.Entity<WebhookProcesado>().HasIndex(x => new { x.Proveedor, x.EventoId }).IsUnique();
        b.Entity<Pago>().Property(x => x.Monto).HasPrecision(12,2); b.Entity<Solicitud>().Property(x => x.Tarifa).HasPrecision(12,2); b.Entity<Solicitud>().Property(x => x.AreaMetrosCuadrados).HasPrecision(10,2);
        b.Entity<Tarifa>().HasData(new Tarifa { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Tipo = TipoSolicitud.Nueva, Monto = 180m }, new Tarifa { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Tipo = TipoSolicitud.Renovacion, Monto = 120m }, new Tarifa { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Tipo = TipoSolicitud.Modificacion, Monto = 90m });
    }
}
