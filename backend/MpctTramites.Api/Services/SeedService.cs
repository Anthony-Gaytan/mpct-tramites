using Microsoft.AspNetCore.Identity;
using MpctTramites.Api.Domain;
namespace MpctTramites.Api.Services;
public static class SeedService
{
 public static async Task SeedAsync(IServiceProvider sp,IConfiguration cfg){using var scope=sp.CreateScope();var roles=scope.ServiceProvider.GetRequiredService<RoleManager<Rol>>();foreach(var name in new[]{"CIUDADANO","CAJERO","INSPECTOR","ADMINISTRADOR"})if(!await roles.RoleExistsAsync(name))await roles.CreateAsync(new Rol{Name=name});var email=cfg["Seed:AdminEmail"];var password=cfg["Seed:AdminPassword"];if(string.IsNullOrWhiteSpace(email)||string.IsNullOrWhiteSpace(password))return;var users=scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();var user=await users.FindByEmailAsync(email);if(user is null){user=new Usuario{UserName=email,Email=email,Nombres="Administrador",Apellidos="Desarrollo",EmailConfirmed=true};var result=await users.CreateAsync(user,password);if(result.Succeeded)await users.AddToRoleAsync(user,"ADMINISTRADOR");}}
}
