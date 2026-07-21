using System.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MpctTramites.Api.Data;
using MpctTramites.Api.Domain;

namespace MpctTramites.Api.Services;
public sealed class SunatService(AppDbContext db, SunatSyncState syncState, IHttpClientFactory clients, IConfiguration config, ILogger<SunatService> logger)
{
    public static string? ValidateRucFormat(string ruc) =>
        ruc.Length != 11 || !ruc.All(char.IsDigit) ? "El RUC debe tener exactamente 11 dígitos." :
        !ruc.StartsWith("20") ? "Solo se aceptan RUC de persona jurídica que comiencen con 20." : null;

    public async Task<(RegistroPadronSunat? Data, string? Error)> ValidateAsync(string ruc, CancellationToken ct)
    {
        string? error = ValidateRucFormat(ruc);
        RegistroPadronSunat? row = null;
        var source = "Padrón Reducido SUNAT";
        if (error is null)
        {
            var apiKey = config["JSON_PE_API_KEY"] ?? config["JsonPe:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                (row, error) = await QueryJsonPeAsync(ruc, apiKey, ct);
                source = "JSON.pe / SUNAT";
            }
            else row = await db.PadronSunat.AsNoTracking().SingleOrDefaultAsync(x => x.Ruc == ruc, ct);
        }
        if (error is null && row is null)
        {
            var sync = syncState.Get();
            error = sync.Estado is "DESCARGANDO" or "PROCESANDO"
                ? "El padrón SUNAT se está actualizando automáticamente. Intenta nuevamente en unos minutos."
                : "RUC no encontrado en el padrón SUNAT actualizado.";
        }
        if (row is not null && !row.Estado.Equals("ACTIVO", StringComparison.OrdinalIgnoreCase)) error = "El RUC no se encuentra ACTIVO.";
        if (row is not null && !row.Condicion.Equals("HABIDO", StringComparison.OrdinalIgnoreCase)) error = "El domicilio fiscal no tiene condición HABIDO.";
        if (row is not null && !(row.Ubigeo.StartsWith("1301") || row.Direccion.Contains("TRUJILLO", StringComparison.OrdinalIgnoreCase))) error = "El domicilio fiscal debe ubicarse en la provincia de Trujillo, La Libertad.";
        db.ValidacionesSunat.Add(new ValidacionSunat { Ruc = ruc, EsValida = error is null, Motivo = error ?? "VALIDADO", Fuente = source }); await db.SaveChangesAsync(ct);
        return (error is null ? row : null, error);
    }

    private async Task<(RegistroPadronSunat? Data, string? Error)> QueryJsonPeAsync(string ruc, string apiKey, CancellationToken ct)
    {
        try
        {
            var client=clients.CreateClient("json-pe");using var request=new HttpRequestMessage(HttpMethod.Post,"api/ruc");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",apiKey);request.Content=JsonContent.Create(new{ruc});
            using var response=await client.SendAsync(request,ct);if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return(null,"No se encontró el RUC ingresado.");
            if(response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)return(null,"El servicio de validación RUC no está configurado correctamente.");
            response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var root=json.RootElement;
            if(root.TryGetProperty("success",out var success)&&!success.GetBoolean())return(null,"No se pudo consultar el RUC en SUNAT.");
            var data=root.TryGetProperty("data",out var nested)?nested:root;
            string Get(params string[] names){foreach(var name in names)if(data.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String)return value.GetString()?.Trim()??"";return "";}
            var estado=Get("estado");var condicion=Get("condicion");var provincia=Get("provincia");var distrito=Get("distrito");var ubigeo=Get("ubigeo","ubigeo_sunat");
            if(!estado.Equals("ACTIVO",StringComparison.OrdinalIgnoreCase))return(null,$"El RUC no se encuentra ACTIVO. Estado actual: {estado}.");
            if(!condicion.Equals("HABIDO",StringComparison.OrdinalIgnoreCase))return(null,$"El RUC no está HABIDO. Condición actual: {condicion}.");
            if(!provincia.Equals("TRUJILLO",StringComparison.OrdinalIgnoreCase)&&!ubigeo.StartsWith("1301"))return(null,"El domicilio fiscal debe ubicarse en la provincia de Trujillo, La Libertad.");
            return(new RegistroPadronSunat{Ruc=ruc,RazonSocial=Get("nombre_o_razon_social","nombre","razon_social"),Estado=estado,Condicion=condicion,Ubigeo=ubigeo,Direccion=Get("direccion_completa","direccion")},null);
        }
        catch(Exception ex){logger.LogWarning(ex,"Falló la consulta de RUC {Ruc} en JSON.pe",ruc);return(null,"No se pudo validar el RUC en este momento. Intenta nuevamente.");}
    }

    public async Task<int> ImportAsync(Stream input, CancellationToken ct)
    {
        using var reader = new StreamReader(input, Encoding.Latin1); var count = 0; string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            var p = line.Split('|'); if (p.Length < 6 || p[0].Length != 11 || !p[0].All(char.IsDigit)) continue;
            var row = await db.PadronSunat.SingleOrDefaultAsync(x => x.Ruc == p[0], ct);
            if (row is null) db.PadronSunat.Add(row = new RegistroPadronSunat { Ruc = p[0] });
            row.RazonSocial = p[1].Trim(); row.Estado = p[2].Trim(); row.Condicion = p[3].Trim(); row.Ubigeo = p[4].Trim();
            row.Direccion = string.Join(" ", p.Skip(5).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x) && x != "-"));
            row.ImportadoEn = DateTimeOffset.UtcNow; count++;
            if (count % 2000 == 0) await db.SaveChangesAsync(ct);
        }
        await db.SaveChangesAsync(ct); return count;
    }
}
