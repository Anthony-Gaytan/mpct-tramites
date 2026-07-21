using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MpctTramites.Api.Data;
using MpctTramites.Api.Domain;

namespace MpctTramites.Api.Controllers;

[ApiController, Route("api/pagos")]
public sealed class PagosController(AppDbContext db, IHttpClientFactory clients, IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("voucher/{solicitudId:guid}")][RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadVoucher(Guid solicitudId,IFormFile archivo,string codigo,MedioPago medio,CancellationToken ct)
    {
        if(medio is not (MedioPago.Yape or MedioPago.Transferencia or MedioPago.Tarjeta))return BadRequest(new{message="Medio de pago no válido para voucher."});
        if(archivo.Length<=0||archivo.Length>10_485_760||!new[]{"application/pdf","image/jpeg","image/png"}.Contains(archivo.ContentType))return BadRequest(new{message="Adjunta un voucher PDF, JPG o PNG de hasta 10 MB."});
        var solicitud=await db.Solicitudes.SingleOrDefaultAsync(x=>x.Id==solicitudId,ct);if(solicitud is null)return NotFound();
        var expected=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo.Trim().ToUpperInvariant())));if(solicitud.CodigoSeguimientoHash!=expected)return Unauthorized(new{message="La autorización de pago no es válida."});
        var folder=Path.Combine(env.ContentRootPath,"storage","vouchers");Directory.CreateDirectory(folder);var name=$"{Guid.NewGuid():N}{Path.GetExtension(archivo.FileName).ToLowerInvariant()}";var path=Path.Combine(folder,name);await using(var stream=System.IO.File.Create(path))await archivo.CopyToAsync(stream,ct);
        var pago=new Pago{SolicitudId=solicitudId,Medio=medio,Monto=solicitud.Tarifa,VoucherNombre=Path.GetFileName(archivo.FileName),VoucherRuta=path};db.Pagos.Add(pago);solicitud.Historial.Add(new HistorialEstado{Estado=solicitud.Estado,Comentario="Voucher enviado para revisión"});await db.SaveChangesAsync(ct);return Ok(new{pago.Id,pago.Estado,pago.Monto,message="Voucher enviado para revisión."});
    }
    [HttpPost("mercadopago/preferencia/{solicitudId:guid}")]
    public async Task<IActionResult> CreatePreference(Guid solicitudId, [FromQuery]string? codigo, CancellationToken ct)
    {
        var solicitud = await db.Solicitudes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == solicitudId, ct);
        if (solicitud is null) return NotFound();
        var authenticated=Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var uid)&&(solicitud.CiudadanoId==uid||User.IsInRole("ADMINISTRADOR"));var expected=string.IsNullOrWhiteSpace(codigo)?null:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo.Trim().ToUpperInvariant())));if(!authenticated&&solicitud.CodigoSeguimientoHash!=expected)return Unauthorized(new{message="La autorización de pago no es válida."});
        if (await db.Pagos.AnyAsync(x => x.SolicitudId == solicitudId && x.Estado == EstadoPago.Aprobado, ct)) return Conflict(new { message = "La solicitud ya está pagada." });
        var token = config["MercadoPago:AccessToken"]; if (string.IsNullOrWhiteSpace(token)) return Problem("Mercado Pago no está configurado.", statusCode: 503);
        var pago = new Pago { SolicitudId = solicitudId, Medio = MedioPago.MercadoPago, Monto = solicitud.Tarifa }; db.Pagos.Add(pago); await db.SaveChangesAsync(ct);
        var origin = config["PublicUrl"]?.TrimEnd('/') ?? "http://localhost:8080";
        var client = clients.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var response = await client.PostAsJsonAsync("https://api.mercadopago.com/checkout/preferences", new { items = new[] { new { title = $"Licencia {solicitud.NumeroExpediente}", quantity = 1, currency_id = "PEN", unit_price = solicitud.Tarifa } }, external_reference = pago.Id.ToString(), notification_url = $"{origin}/api/pagos/mercadopago/webhook", back_urls = new { success = origin, pending = origin, failure = origin }, auto_return = "approved" }, ct);
        var json = await response.Content.ReadAsStringAsync(ct); pago.RespuestaAuditada = json.Length > 10000 ? json[..10000] : json; await db.SaveChangesAsync(ct);
        if (!response.IsSuccessStatusCode) return Problem("Mercado Pago rechazó la creación de la preferencia.", statusCode: 502);
        using var doc = JsonDocument.Parse(json); pago.IdentificadorExterno = doc.RootElement.GetProperty("id").GetString(); await db.SaveChangesAsync(ct);
        return Ok(new { preferenceId = pago.IdentificadorExterno, checkoutUrl = doc.RootElement.TryGetProperty("sandbox_init_point", out var sandbox) ? sandbox.GetString() : doc.RootElement.GetProperty("init_point").GetString() });
    }

    [HttpPost("mercadopago/webhook")]
    public async Task<IActionResult> Webhook([FromQuery(Name="data.id")] string paymentId, CancellationToken ct)
    {
        var requestId = Request.Headers["x-request-id"].ToString(); var signature = Request.Headers["x-signature"].ToString();
        if (!ValidSignature(paymentId, requestId, signature, config["MercadoPago:WebhookSecret"])) return Unauthorized();
        if (await db.WebhooksProcesados.AnyAsync(x => x.Proveedor == "MERCADOPAGO" && x.EventoId == paymentId, ct)) return Ok();
        var token = config["MercadoPago:AccessToken"]!; var client = clients.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var response = await client.GetAsync($"https://api.mercadopago.com/v1/payments/{Uri.EscapeDataString(paymentId)}", ct); if (!response.IsSuccessStatusCode) return StatusCode(502);
        var json = await response.Content.ReadAsStringAsync(ct); using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        if (!Guid.TryParse(root.GetProperty("external_reference").GetString(), out var localId)) return BadRequest();
        var pago = await db.Pagos.SingleOrDefaultAsync(x => x.Id == localId, ct); if (pago is null) return NotFound();
        var amount = root.GetProperty("transaction_amount").GetDecimal(); if (amount != pago.Monto) return BadRequest();
        pago.Estado = root.GetProperty("status").GetString() switch { "approved" => EstadoPago.Aprobado, "rejected" => EstadoPago.Rechazado, "cancelled" => EstadoPago.Cancelado, "refunded" => EstadoPago.Reembolsado, _ => EstadoPago.Pendiente };
        pago.IdentificadorExterno = paymentId; pago.RespuestaAuditada = json.Length > 10000 ? json[..10000] : json; if (pago.Estado == EstadoPago.Aprobado) pago.ConfirmadoEn = DateTimeOffset.UtcNow;
        db.WebhooksProcesados.Add(new WebhookProcesado { Proveedor = "MERCADOPAGO", EventoId = paymentId, HashContenido = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))) }); await db.SaveChangesAsync(ct); return Ok();
    }

    private static bool ValidSignature(string id, string requestId, string header, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return false; var parts = header.Split(',').Select(x => x.Trim().Split('=', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0], x => x[1]);
        if (!parts.TryGetValue("ts", out var ts) || !parts.TryGetValue("v1", out var sent)) return false;
        var manifest = $"id:{id.ToLowerInvariant()};request-id:{requestId};ts:{ts};"; var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(sent.ToLowerInvariant()));
    }
}
