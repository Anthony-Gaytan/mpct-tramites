using System.Text;
using Microsoft.EntityFrameworkCore;
using MpctTramites.Api.Data;
using MpctTramites.Api.Domain;

namespace MpctTramites.Api.Services;
public sealed class SunatService(AppDbContext db, SunatSyncState syncState)
{
    public static string? ValidateRucFormat(string ruc) =>
        ruc.Length != 11 || !ruc.All(char.IsDigit) ? "El RUC debe tener exactamente 11 dígitos." :
        !ruc.StartsWith("20") ? "Solo se aceptan RUC de persona jurídica que comiencen con 20." : null;

    public async Task<(RegistroPadronSunat? Data, string? Error)> ValidateAsync(string ruc, CancellationToken ct)
    {
        string? error = ValidateRucFormat(ruc);
        var row = error is null ? await db.PadronSunat.AsNoTracking().SingleOrDefaultAsync(x => x.Ruc == ruc, ct) : null;
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
        db.ValidacionesSunat.Add(new ValidacionSunat { Ruc = ruc, EsValida = error is null, Motivo = error ?? "VALIDADO" }); await db.SaveChangesAsync(ct);
        return (error is null ? row : null, error);
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
