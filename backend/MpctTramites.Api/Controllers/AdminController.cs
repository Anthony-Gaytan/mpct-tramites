using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MpctTramites.Api.Contracts;
using MpctTramites.Api.Data;
using MpctTramites.Api.Domain;
namespace MpctTramites.Api.Controllers;
[ApiController,Authorize(Roles="ADMINISTRADOR"),Route("api/admin")]
public sealed class AdminController(AppDbContext db, UserManager<Usuario> users):ControllerBase
{
 [HttpGet("dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct)=>Ok(new{solicitudes=await db.Solicitudes.CountAsync(ct),pendientes=await db.Solicitudes.CountAsync(x=>x.Estado==EstadoSolicitud.EnRevision,ct),inspecciones=await db.Inspecciones.CountAsync(x=>x.Estado!="FINALIZADA",ct),recaudacion=await db.Pagos.Where(x=>x.Estado==EstadoPago.Aprobado).SumAsync(x=>(decimal?)x.Monto,ct)??0});
 [HttpGet("solicitudes")] public async Task<IActionResult> Requests(CancellationToken ct)=>Ok(await db.Solicitudes.AsNoTracking().Include(x=>x.Empresa).OrderByDescending(x=>x.CreadoEn).Select(x=>new{x.Id,x.NumeroExpediente,ruc=x.Empresa!.Ruc,razonSocial=x.Empresa.RazonSocial,x.Estado,x.Tipo,x.Tarifa,x.CreadoEn}).Take(100).ToListAsync(ct));
 [HttpGet("tarifas")] public async Task<IActionResult> Tariffs(CancellationToken ct)=>Ok(await db.Tarifas.AsNoTracking().Where(x=>x.Activa).OrderBy(x=>x.Tipo).Select(x=>new{x.Id,x.Tipo,x.Monto}).ToListAsync(ct));
 [HttpPut("tarifas/{tipo}")] public async Task<IActionResult> UpdateTariff(TipoSolicitud tipo,ActualizarTarifaRequest request,CancellationToken ct){if(request.Monto<=0||request.Monto>10000)return BadRequest(new{message="El importe debe ser mayor que cero y menor o igual a S/ 10,000."});var tarifa=await db.Tarifas.SingleOrDefaultAsync(x=>x.Tipo==tipo&&x.Activa,ct);if(tarifa is null)return NotFound();tarifa.Monto=decimal.Round(request.Monto,2);db.Auditorias.Add(new Auditoria{UsuarioId=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),Accion="ACTUALIZAR_TARIFA",Entidad="Tarifa",EntidadId=tarifa.Id.ToString(),DetalleJson=System.Text.Json.JsonSerializer.Serialize(new{tipo,monto=tarifa.Monto})});await db.SaveChangesAsync(ct);return Ok(new{tarifa.Id,tarifa.Tipo,tarifa.Monto});}
 [HttpGet("pagos/pendientes")] public async Task<IActionResult> PendingPayments(CancellationToken ct)=>Ok(await db.Pagos.AsNoTracking().Where(x=>x.Estado==EstadoPago.Pendiente&&x.VoucherRuta!=null).Join(db.Solicitudes.Include(x=>x.Empresa),p=>p.SolicitudId,s=>s.Id,(p,s)=>new{p.Id,p.Medio,p.Monto,p.VoucherNombre,p.CreadoEn,s.NumeroExpediente,ruc=s.Empresa!.Ruc,razonSocial=s.Empresa.RazonSocial}).ToListAsync(ct));
 [HttpGet("pagos/{id:guid}/voucher")] public async Task<IActionResult> Voucher(Guid id,CancellationToken ct){var pago=await db.Pagos.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);if(pago?.VoucherRuta is null||!System.IO.File.Exists(pago.VoucherRuta))return NotFound();return PhysicalFile(pago.VoucherRuta,"application/octet-stream",pago.VoucherNombre??"voucher");}
 [HttpPost("pagos/{id:guid}/revisar")] public async Task<IActionResult> ReviewPayment(Guid id,RevisarPagoRequest request,CancellationToken ct){var pago=await db.Pagos.FindAsync([id],ct);if(pago is null)return NotFound();var solicitud=await db.Solicitudes.Include(x=>x.Historial).SingleAsync(x=>x.Id==pago.SolicitudId,ct);pago.Estado=request.Aprobado?EstadoPago.Aprobado:EstadoPago.Rechazado;pago.MotivoRevision=request.Motivo;pago.RevisadoPorId=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);pago.ConfirmadoEn=request.Aprobado?DateTimeOffset.UtcNow:null;solicitud.Estado=request.Aprobado?EstadoSolicitud.Aprobada:EstadoSolicitud.PendientePago;solicitud.Historial.Add(new HistorialEstado{Estado=solicitud.Estado,Comentario=request.Aprobado?"Pago aprobado y licencia autorizada":$"Voucher rechazado: {request.Motivo}",UsuarioId=pago.RevisadoPorId});await db.SaveChangesAsync(ct);return Ok(new{pago.Id,estadoPago=pago.Estado,estadoSolicitud=solicitud.Estado});}
 [HttpPost("inspecciones/asignar")] public async Task<IActionResult> Assign(Guid solicitudId,Guid inspectorId,DateTimeOffset fecha,CancellationToken ct){var item=new Inspeccion{SolicitudId=solicitudId,InspectorId=inspectorId,ProgramadaPara=fecha};db.Inspecciones.Add(item);var s=await db.Solicitudes.FindAsync([solicitudId],ct);if(s is null)return NotFound();s.Estado=EstadoSolicitud.InspeccionProgramada;s.Historial.Add(new HistorialEstado{Estado=s.Estado,Comentario="Inspección asignada"});await db.SaveChangesAsync(ct);return Ok(item);}

 [HttpGet("usuarios")]
 public async Task<IActionResult> ListUsers(CancellationToken ct)
 {
   var staff=await users.Users.Where(x=>!x.UserName!.Contains("@") || x.Email!=null).OrderBy(x=>x.Nombres).ToListAsync(ct);
   var result=new List<object>();
   foreach(var user in staff){var roles=await users.GetRolesAsync(user);if(roles.Any(x=>x is "ADMINISTRADOR" or "CAJERO" or "INSPECTOR"))result.Add(new{user.Id,user.Nombres,user.Apellidos,user.Email,user.Activo,user.MotivoSuspension,user.SuspendidoEn,roles,user.CreadoEn});}
   return Ok(result);
 }

 [HttpPost("usuarios")]
 public async Task<IActionResult> CreateUser(CrearPersonalRequest request)
 {
   var role=request.Rol.Trim().ToUpperInvariant();
   if(role is not ("CAJERO" or "INSPECTOR"))return BadRequest(new{message="Solo se puede crear personal CAJERO o INSPECTOR."});
   var email=request.Email.Trim().ToLowerInvariant();
   var user=new Usuario{UserName=email,Email=email,Nombres=request.Nombres.Trim(),Apellidos=request.Apellidos.Trim(),EmailConfirmed=true};
   var created=await users.CreateAsync(user,request.Password);
   if(!created.Succeeded)return ValidationProblem(new ValidationProblemDetails(created.Errors.GroupBy(x=>x.Code).ToDictionary(x=>x.Key,x=>x.Select(y=>y.Description).ToArray())));
   await users.AddToRoleAsync(user,role);return Ok(new{user.Id,user.Email,user.Nombres,user.Apellidos,user.Activo,roles=new[]{role}});
 }

 [HttpPatch("usuarios/{id:guid}/estado")]
 public async Task<IActionResult> ChangeStatus(Guid id,CambiarEstadoUsuarioRequest request)
 {
   var user=await users.FindByIdAsync(id.ToString());if(user is null)return NotFound();
   if(User.FindFirstValue(ClaimTypes.NameIdentifier)==id.ToString()&&!request.Activo)return BadRequest(new{message="No puedes desactivar tu propia cuenta."});
   if(!request.Activo&&string.IsNullOrWhiteSpace(request.Motivo))return BadRequest(new{message="Indica el motivo de la suspensión."});
   user.Activo=request.Activo;user.MotivoSuspension=request.Activo?null:request.Motivo!.Trim();user.SuspendidoEn=request.Activo?null:DateTimeOffset.UtcNow;user.SuspendidoPorId=request.Activo?null:Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
   var result=await users.UpdateAsync(user);return result.Succeeded?Ok(new{user.Id,user.Activo,user.MotivoSuspension,user.SuspendidoEn}):BadRequest(result.Errors);
 }

 [HttpPatch("usuarios/{id:guid}/password")]
 public async Task<IActionResult> ResetPassword(Guid id,RestablecerPasswordRequest request)
 {
   var user=await users.FindByIdAsync(id.ToString());if(user is null)return NotFound();
   var roles=await users.GetRolesAsync(user);if(!roles.Any(x=>x is "CAJERO" or "INSPECTOR"))return BadRequest(new{message="Solo se puede restablecer la contraseña del personal municipal."});
   var token=await users.GeneratePasswordResetTokenAsync(user);var result=await users.ResetPasswordAsync(user,token,request.PasswordNueva);
   return result.Succeeded?Ok(new{message="Contraseña restablecida correctamente."}):BadRequest(new{message=string.Join(" ",result.Errors.Select(x=>x.Description))});
 }

 [HttpPut("perfil")]
 public async Task<IActionResult> UpdateProfile(ActualizarPerfilRequest request)
 {
   var id=User.FindFirstValue(ClaimTypes.NameIdentifier);var user=id is null?null:await users.FindByIdAsync(id);if(user is null)return Unauthorized();
   if(!await users.CheckPasswordAsync(user,request.PasswordActual))return BadRequest(new{message="La contraseña actual es incorrecta."});
   var email=request.Email.Trim().ToLowerInvariant();user.Email=email;user.UserName=email;user.Nombres=request.Nombres.Trim();user.Apellidos=request.Apellidos.Trim();
   var updated=await users.UpdateAsync(user);if(!updated.Succeeded)return BadRequest(new{message=string.Join(" ",updated.Errors.Select(x=>x.Description))});
   if(!string.IsNullOrWhiteSpace(request.PasswordNueva)){var changed=await users.ChangePasswordAsync(user,request.PasswordActual,request.PasswordNueva);if(!changed.Succeeded)return BadRequest(new{message=string.Join(" ",changed.Errors.Select(x=>x.Description))});}
   return Ok(new{message="Perfil actualizado. Vuelve a iniciar sesión.",user.Email,user.Nombres,user.Apellidos});
 }
}
