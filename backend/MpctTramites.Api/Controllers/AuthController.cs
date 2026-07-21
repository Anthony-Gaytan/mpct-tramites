using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using MpctTramites.Api.Contracts;
using MpctTramites.Api.Domain;
using MpctTramites.Api.Services;

namespace MpctTramites.Api.Controllers;
[ApiController, Route("api/auth"), EnableRateLimiting("sensitive")]
public sealed class AuthController(UserManager<Usuario> users, TokenService tokens) : ControllerBase
{
    [HttpPost("registro")]
    public async Task<IActionResult> Register(RegistroRequest request)
    {
        var user = new Usuario { UserName = request.Email.Trim().ToLowerInvariant(), Email = request.Email.Trim().ToLowerInvariant(), Nombres = request.Nombres.Trim(), Apellidos = request.Apellidos.Trim() };
        var result = await users.CreateAsync(user, request.Password); if (!result.Succeeded) return ValidationProblem(new ValidationProblemDetails(result.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(y => y.Description).ToArray())));
        await users.AddToRoleAsync(user, "CIUDADANO"); return Ok(new { token = tokens.Create(user, ["CIUDADANO"]), user = new { user.Nombres, user.Apellidos, roles = new[] { "CIUDADANO" } } });
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email); if (user is null || !user.Activo || !await users.CheckPasswordAsync(user, request.Password)) return Unauthorized(new { message = "Credenciales inválidas." });
        var roles = await users.GetRolesAsync(user); return Ok(new { token = tokens.Create(user, roles), user = new { user.Nombres, user.Apellidos, user.Email, roles } });
    }
    [Authorize, HttpPut("perfil")]
    public async Task<IActionResult> UpdateProfile(ActualizarPerfilRequest request)
    {
        var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");var user=raw is null?null:await users.FindByIdAsync(raw);if(user is null)return Unauthorized();
        if(!await users.CheckPasswordAsync(user,request.PasswordActual))return BadRequest(new{message="La contraseña actual es incorrecta."});
        var email=request.Email.Trim().ToLowerInvariant();var existing=await users.FindByEmailAsync(email);if(existing is not null&&existing.Id!=user.Id)return BadRequest(new{message="El correo ya pertenece a otra cuenta."});
        user.Email=email;user.UserName=email;user.Nombres=request.Nombres.Trim();user.Apellidos=request.Apellidos.Trim();var updated=await users.UpdateAsync(user);if(!updated.Succeeded)return BadRequest(new{message=string.Join(" ",updated.Errors.Select(x=>x.Description))});
        if(!string.IsNullOrWhiteSpace(request.PasswordNueva)){var changed=await users.ChangePasswordAsync(user,request.PasswordActual,request.PasswordNueva);if(!changed.Succeeded)return BadRequest(new{message=string.Join(" ",changed.Errors.Select(x=>x.Description))});}
        return Ok(new{message="Datos actualizados. Inicia sesión nuevamente."});
    }
}
