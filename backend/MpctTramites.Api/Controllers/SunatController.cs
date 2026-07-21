using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MpctTramites.Api.Services;
namespace MpctTramites.Api.Controllers;
[ApiController, Route("api/sunat"), EnableRateLimiting("sensitive")]
public sealed class SunatController(SunatService sunat, SunatSyncQueue syncQueue, SunatSyncState syncState) : ControllerBase
{
    [HttpGet("ruc/{ruc}")] public async Task<IActionResult> Validate(string ruc, CancellationToken ct) { var (data,error)=await sunat.ValidateAsync(ruc,ct); return error is null ? Ok(new { data!.Ruc, data.RazonSocial, data.Direccion, data.Ubigeo, data.Estado, data.Condicion, fuente="Padrón Reducido SUNAT", validadoEn=DateTimeOffset.UtcNow }) : BadRequest(new { message=error }); }
    [Authorize(Roles="ADMINISTRADOR"), HttpPost("padron/importar")] [RequestSizeLimit(1_000_000_000)] public async Task<IActionResult> Import(IFormFile archivo, CancellationToken ct) { if (archivo.Length == 0) return BadRequest(); await using var s=archivo.OpenReadStream(); return Ok(new { registros=await sunat.ImportAsync(s,ct) }); }
    [Authorize(Roles="ADMINISTRADOR"), HttpPost("padron/sincronizar")] public IActionResult Synchronize() { var current=syncState.Get(); if(current.Estado is "DESCARGANDO" or "PROCESANDO") return Conflict(new {message="Ya existe una sincronización en curso.",status=current}); return syncQueue.TryQueue()?Accepted(new{message="Sincronización iniciada."}):Conflict(new{message="La sincronización ya está en cola."}); }
    [Authorize(Roles="ADMINISTRADOR"), HttpGet("padron/sincronizar/estado")] public IActionResult SyncStatus()=>Ok(syncState.Get());
}
