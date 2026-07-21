using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MpctTramites.Api.Contracts;
using MpctTramites.Api.Data;
using MpctTramites.Api.Domain;
using MpctTramites.Api.Services;

namespace MpctTramites.Api.Controllers;
[ApiController, Route("api/solicitudes")]
public sealed class SolicitudesController(AppDbContext db, SunatService sunat, IWebHostEnvironment env) : ControllerBase
{
    [Authorize, HttpPost]
    public async Task<IActionResult> Create(CrearSolicitudRequest request, CancellationToken ct)
    {
        var (row,error)=await sunat.ValidateAsync(request.Ruc,ct); if(error is not null) return BadRequest(new {message=error});
        var empresa=await db.Empresas.SingleOrDefaultAsync(x=>x.Ruc==request.Ruc,ct); if(empresa is null) db.Empresas.Add(empresa=new Empresa{Ruc=row!.Ruc});
        empresa.RazonSocial=row!.RazonSocial; empresa.DireccionFiscal=row.Direccion; empresa.Ubigeo=row.Ubigeo; empresa.Estado=row.Estado; empresa.Condicion=row.Condicion; empresa.ValidadoEn=DateTimeOffset.UtcNow; empresa.FuenteValidacion="Padrón Reducido SUNAT";
        var tarifa=await db.Tarifas.AsNoTracking().Where(x=>x.Tipo==request.Tipo&&x.Activa).Select(x=>x.Monto).FirstAsync(ct); var code=Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
        var solicitud=new Solicitud { NumeroExpediente=$"MPCT-{DateTime.UtcNow:yyyy}-{RandomNumberGenerator.GetInt32(100000,999999)}", CodigoSeguimientoHash=Hash(code), Empresa=empresa, CiudadanoId=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), Tipo=request.Tipo, RepresentanteNombre=request.RepresentanteNombre, RepresentanteDocumento=request.RepresentanteDocumento, RepresentanteEmail=request.RepresentanteEmail, Rubro=request.Rubro, Actividad=request.Actividad, AreaMetrosCuadrados=request.AreaMetrosCuadrados, DireccionLocal=request.DireccionLocal, Tarifa=tarifa, Estado=EstadoSolicitud.PendientePago };
        solicitud.Historial.Add(new HistorialEstado{Estado=solicitud.Estado,Comentario="Solicitud registrada"}); db.Solicitudes.Add(solicitud); await db.SaveChangesAsync(ct); return CreatedAtAction(nameof(Get),new{id=solicitud.Id},new{solicitud.Id,solicitud.NumeroExpediente,codigoSeguimiento=code,solicitud.Estado,solicitud.Tarifa});
    }
    [Authorize, HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id,CancellationToken ct) { var x=await db.Solicitudes.AsNoTracking().Include(s=>s.Empresa).Include(s=>s.Historial).SingleOrDefaultAsync(s=>s.Id==id,ct); return x is null?NotFound():Ok(x); }
    [Authorize, HttpGet("mias")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var uid=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await db.Solicitudes.AsNoTracking().Where(x=>x.CiudadanoId==uid).Include(x=>x.Empresa).OrderByDescending(x=>x.CreadoEn).Select(x=>new{x.Id,x.NumeroExpediente,ruc=x.Empresa!.Ruc,razonSocial=x.Empresa.RazonSocial,x.Tipo,x.Estado,x.Tarifa,x.CreadoEn}).ToListAsync(ct));
    }
    [HttpPost("seguimiento"), EnableRateLimiting("sensitive")] public async Task<IActionResult> Track(SeguimientoRequest r,CancellationToken ct) { var x=await db.Solicitudes.AsNoTracking().Include(s=>s.Empresa).Include(s=>s.Historial).SingleOrDefaultAsync(s=>s.Empresa!.Ruc==r.Ruc&&s.CodigoSeguimientoHash==Hash(r.Codigo),ct); return x is null?NotFound(new{message="No se encontró el expediente con los datos indicados."}):Ok(new{x.NumeroExpediente,x.Estado,x.CreadoEn,x.Observaciones,historial=x.Historial.OrderByDescending(h=>h.CreadoEn)}); }
    [Authorize, HttpPost("{id:guid}/documentos")][RequestSizeLimit(10_485_760)] public async Task<IActionResult> Upload(Guid id,IFormFile archivo,string tipo,CancellationToken ct) { var allowed=new[]{"application/pdf","image/jpeg","image/png"}; if(!allowed.Contains(archivo.ContentType)||archivo.Length<=0||archivo.Length>10_485_760)return BadRequest(new{message="Solo PDF, JPG o PNG de hasta 10 MB."}); if(!await db.Solicitudes.AnyAsync(x=>x.Id==id,ct))return NotFound(); var folder=Path.Combine(env.ContentRootPath,"storage",id.ToString());Directory.CreateDirectory(folder);var name=$"{Guid.NewGuid():N}{Path.GetExtension(archivo.FileName).ToLowerInvariant()}";var path=Path.Combine(folder,name);await using(var f=System.IO.File.Create(path))await archivo.CopyToAsync(f,ct);await using var read=System.IO.File.OpenRead(path);var hash=Convert.ToHexString(await SHA256.HashDataAsync(read,ct));var doc=new Documento{SolicitudId=id,Tipo=tipo,NombreOriginal=Path.GetFileName(archivo.FileName),NombreAlmacenado=name,Mime=archivo.ContentType,Tamano=archivo.Length,Sha256=hash};db.Documentos.Add(doc);await db.SaveChangesAsync(ct);return Ok(new{doc.Id,doc.NombreOriginal}); }
    private static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s.Trim().ToUpperInvariant())));
}
