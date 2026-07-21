using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MpctTramites.Api.Contracts;
using MpctTramites.Api.Data;
using MpctTramites.Api.Domain;
namespace MpctTramites.Api.Controllers;
[ApiController,Authorize(Roles="CAJERO,ADMINISTRADOR"),Route("api/cajas")]
public sealed class CajasController(AppDbContext db):ControllerBase
{
 [HttpPost("abrir")] public async Task<IActionResult> Open(AbrirCajaRequest r,CancellationToken ct){var uid=UserId();if(await db.Cajas.AnyAsync(x=>x.CajeroId==uid&&x.EstaAbierta,ct))return Conflict(new{message="Ya existe una caja abierta."});var caja=new Caja{CajeroId=uid,MontoInicial=r.MontoInicial};db.Cajas.Add(caja);await db.SaveChangesAsync(ct);return Ok(caja);}
 [HttpPost("{id:guid}/pagos")] public async Task<IActionResult> Pay(Guid id,PagoPresencialRequest r,CancellationToken ct){var caja=await db.Cajas.SingleOrDefaultAsync(x=>x.Id==id&&x.EstaAbierta,ct);if(caja is null)return Conflict(new{message="La caja no está abierta."});var solicitud=await db.Solicitudes.FindAsync([r.SolicitudId],ct);if(solicitud is null)return NotFound();if(r.Monto!=solicitud.Tarifa)return BadRequest(new{message="El monto debe coincidir con la tarifa vigente."});var pago=new Pago{SolicitudId=r.SolicitudId,CajaId=id,Medio=r.Medio,Monto=r.Monto,Estado=EstadoPago.Aprobado,ConfirmadoEn=DateTimeOffset.UtcNow};db.Pagos.Add(pago);db.MovimientosCaja.Add(new MovimientoCaja{CajaId=id,PagoId=pago.Id,Medio=r.Medio,Monto=r.Monto});solicitud.Estado=EstadoSolicitud.EnRevision;solicitud.Historial.Add(new HistorialEstado{Estado=solicitud.Estado,Comentario="Pago presencial confirmado",UsuarioId=UserId()});await db.SaveChangesAsync(ct);return Ok(new{pago.Id,pago.Monto,comprobante=$"REC-{pago.Id.ToString()[..8].ToUpperInvariant()}"});}
 [HttpPost("{id:guid}/cerrar")] public async Task<IActionResult> Close(Guid id,CerrarCajaRequest r,CancellationToken ct){var caja=await db.Cajas.SingleOrDefaultAsync(x=>x.Id==id&&x.EstaAbierta,ct);if(caja is null)return Conflict(new{message="La caja no está abierta."});var mov=await db.MovimientosCaja.Where(x=>x.CajaId==id&&!x.Anulado).ToListAsync(ct);var totals=mov.GroupBy(x=>x.Medio).ToDictionary(x=>x.Key.ToString(),x=>x.Sum(y=>y.Monto));var expected=caja.MontoInicial+mov.Sum(x=>x.Monto);var cierre=new CierreCaja{CajaId=id,TotalEsperado=expected,TotalDeclarado=r.TotalDeclarado,TotalesPorMedioJson=JsonSerializer.Serialize(totals)};caja.EstaAbierta=false;caja.CerradaEn=DateTimeOffset.UtcNow;db.CierresCaja.Add(cierre);await db.SaveChangesAsync(ct);return Ok(new{cierre.Id,expected,r.TotalDeclarado,diferencia=r.TotalDeclarado-expected,totals});}
 private Guid UserId()=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
