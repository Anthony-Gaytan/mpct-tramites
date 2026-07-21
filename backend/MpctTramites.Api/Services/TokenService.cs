using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MpctTramites.Api.Domain;

namespace MpctTramites.Api.Services;
public sealed class TokenService(IConfiguration config)
{
    public string Create(Usuario user, IEnumerable<string> roles)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key no configurada");
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(JwtRegisteredClaimNames.Email, user.Email!), new(ClaimTypes.Name, $"{user.Nombres} {user.Apellidos}") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims, expires: DateTime.UtcNow.AddMinutes(config.GetValue("Jwt:Minutes", 60)), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)));
    }
}
