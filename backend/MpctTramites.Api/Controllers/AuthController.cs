using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        var roles = await users.GetRolesAsync(user); return Ok(new { token = tokens.Create(user, roles), user = new { user.Nombres, user.Apellidos, roles } });
    }
}
