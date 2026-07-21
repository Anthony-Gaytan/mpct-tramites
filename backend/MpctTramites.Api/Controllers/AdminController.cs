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
