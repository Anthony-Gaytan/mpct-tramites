using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MpctTramites.Api.Data;
using MpctTramites.Api.Domain;
using MpctTramites.Api.Services;

var builder=WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var databaseConnection = NormalizePostgresConnection(builder.Configuration["DATABASE_URL"] ?? builder.Configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Configure PostgreSQL."));
builder.Services.AddDbContext<AppDbContext>(o=>o.UseNpgsql(databaseConnection));
builder.Services.AddIdentityCore<Usuario>(o=>{o.Password.RequiredLength=10;o.Password.RequireDigit=true;o.Password.RequireUppercase=true;o.Password.RequireNonAlphanumeric=true;o.Lockout.MaxFailedAccessAttempts=5;}).AddRoles<Rol>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager();
var jwtKey=builder.Configuration["Jwt:Key"]??throw new InvalidOperationException("Configure Jwt:Key (mínimo 32 caracteres).");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>o.TokenValidationParameters=new TokenValidationParameters{ValidateIssuer=true,ValidateAudience=true,ValidateLifetime=true,ValidateIssuerSigningKey=true,ValidIssuer=builder.Configuration["Jwt:Issuer"],ValidAudience=builder.Configuration["Jwt:Audience"],IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),ClockSkew=TimeSpan.FromMinutes(1)});
builder.Services.AddAuthorization(); builder.Services.AddScoped<TokenService>(); builder.Services.AddScoped<SunatService>(); builder.Services.AddHttpClient(); builder.Services.AddControllers(); builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.Services.AddCors(o=>o.AddPolicy("frontend",p=>p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()??[]).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("sensitive",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen(o=>{o.AddSecurityDefinition("Bearer",new OpenApiSecurityScheme{Type=SecuritySchemeType.Http,Scheme="bearer",BearerFormat="JWT"});});
var app=builder.Build();if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}app.UseExceptionHandler("/error");app.UseHttpsRedirection();app.UseDefaultFiles();app.UseStaticFiles();app.UseCors("frontend");app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();app.MapControllers();app.MapHealthChecks("/health");app.Map("/error",()=>Results.Problem("Ocurrió un error inesperado."));app.MapFallbackToFile("index.html");
if(app.Configuration.GetValue("Database:MigrateOnStartup",false)){using var scope=app.Services.CreateScope();await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();}await SeedService.SeedAsync(app.Services,app.Configuration);app.Run();
static string NormalizePostgresConnection(string value)
{
    if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) return value;
    var uri = new Uri(value); var credentials = uri.UserInfo.Split(':', 2);
    return $"Host={uri.Host};Port={(uri.IsDefaultPort ? 5432 : uri.Port)};Database={uri.AbsolutePath.TrimStart('/')};Username={Uri.UnescapeDataString(credentials[0])};Password={Uri.UnescapeDataString(credentials.ElementAtOrDefault(1) ?? string.Empty)};SSL Mode=Prefer;Trust Server Certificate=true";
}

public partial class Program { }
