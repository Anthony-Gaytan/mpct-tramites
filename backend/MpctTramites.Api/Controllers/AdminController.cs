using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MpctTramites.Api.Data;
using MpctTramites.Api.Domain;
namespace MpctTramites.Api.Controllers;
[ApiController,Authorize(Roles="ADMINISTRADOR"),Route("api/admin")]
public sealed class AdminController(AppDbContext db):ControllerBase
{
 [HttpGet("dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct)=>Ok(new{solicitudes=await db.Solicitudes.CountAsync(ct),pendientes=await db.Solicitudes.CountAsync(x=>x.Estado==EstadoSolicitud.EnRevision,ct),inspecciones=await db.Inspecciones.CountAsync(x=>x.Estado!="FINALIZADA",ct),recaudacion=await db.Pagos.Where(x=>x.Estado==EstadoPago.Aprobado).SumAsync(x=>(decimal?)x.Monto,ct)??0});
 [HttpPost("inspecciones/asignar")] public async Task<IActionResult> Assign(Guid solicitudId,Guid inspectorId,DateTimeOffset fecha,CancellationToken ct){var item=new Inspeccion{SolicitudId=solicitudId,InspectorId=inspectorId,ProgramadaPara=fecha};db.Inspecciones.Add(item);var s=await db.Solicitudes.FindAsync([solicitudId],ct);if(s is null)return NotFound();s.Estado=EstadoSolicitud.InspeccionProgramada;s.Historial.Add(new HistorialEstado{Estado=s.Estado,Comentario="Inspección asignada"});await db.SaveChangesAsync(ct);return Ok(item);}
}
