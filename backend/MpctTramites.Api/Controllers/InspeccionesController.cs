using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MpctTramites.Api.Contracts;
using MpctTramites.Api.Data;
namespace MpctTramites.Api.Controllers;
[ApiController,Authorize(Roles="INSPECTOR,ADMINISTRADOR"),Route("api/inspecciones")]
public sealed class InspeccionesController(AppDbContext db):ControllerBase
{
 [HttpGet("mias")] public async Task<IActionResult> Mine(CancellationToken ct){var uid=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);return Ok(await db.Inspecciones.AsNoTracking().Where(x=>x.InspectorId==uid).Join(db.Solicitudes,i=>i.SolicitudId,s=>s.Id,(i,s)=>new{i,s.NumeroExpediente,s.Rubro,s.DireccionLocal}).ToListAsync(ct));}
 [HttpPut("{id:guid}/resultado")] public async Task<IActionResult> Result(Guid id,ResultadoInspeccionRequest r,CancellationToken ct){var x=await db.Inspecciones.FindAsync([id],ct);if(x is null)return NotFound();x.Estado=r.Estado;x.Observaciones=r.Observaciones;x.RespuestasJson=r.RespuestasJson;x.Latitud=r.Latitud;x.Longitud=r.Longitud;if(r.Firmar)x.FirmadaEn=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);return Ok(x);}
}
