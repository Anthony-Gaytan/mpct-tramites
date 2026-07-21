using System.IO.Compression;
using System.Text;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using MpctTramites.Api.Data;
using Npgsql;
using NpgsqlTypes;

namespace MpctTramites.Api.Services;

public sealed record SunatSyncStatus(string Estado, long LineasLeidas, long RegistrosTrujillo, string? Mensaje, DateTimeOffset? IniciadoEn, DateTimeOffset? FinalizadoEn);

public sealed class SunatSyncState
{
    private readonly object gate = new();
    private SunatSyncStatus value = new("SIN_EJECUTAR", 0, 0, null, null, null);
    public SunatSyncStatus Get() { lock (gate) return value; }
    public void Set(SunatSyncStatus status) { lock (gate) value = status; }
    public void Progress(long lines, long records) { lock (gate) value = value with { LineasLeidas = lines, RegistrosTrujillo = records }; }
}

public sealed class SunatSyncQueue
{
    private readonly Channel<bool> channel = Channel.CreateBounded<bool>(1);
    public bool TryQueue() => channel.Writer.TryWrite(true);
    public IAsyncEnumerable<bool> ReadAllAsync(CancellationToken ct) => channel.Reader.ReadAllAsync(ct);
}

public sealed class SunatSyncWorker(IServiceScopeFactory scopes, IHttpClientFactory clients, SunatSyncQueue queue, SunatSyncState state, ILogger<SunatSyncWorker> logger) : BackgroundService
{
    private const string SourceUrl = "https://www2.sunat.gob.pe/padron_reducido_ruc.zip";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var _ in queue.ReadAllAsync(stoppingToken))
        {
            try { await SynchronizeAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falló la sincronización del padrón SUNAT");
                var current = state.Get(); state.Set(current with { Estado = "ERROR", Mensaje = ex.Message, FinalizadoEn = DateTimeOffset.UtcNow });
            }
        }
    }

    private async Task SynchronizeAsync(CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow; state.Set(new("DESCARGANDO", 0, 0, "Descargando padrón oficial SUNAT", started, null));
        var tempFile = Path.Combine(Path.GetTempPath(), $"sunat-{Guid.NewGuid():N}.zip");
        try
        {
            var client = clients.CreateClient("sunat-download");
            using (var response = await client.GetAsync(SourceUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var target = File.Create(tempFile); await source.CopyToAsync(target, ct);
            }

            state.Set(state.Get() with { Estado = "PROCESANDO", Mensaje = "Filtrando personas jurídicas de la provincia de Trujillo" });
            using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var connection = (NpgsqlConnection)db.Database.GetDbConnection(); await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);
            await using (var create = new NpgsqlCommand("CREATE TEMP TABLE sunat_stage (ruc text PRIMARY KEY, razon_social text, estado text, condicion text, ubigeo text, direccion text, importado_en timestamptz) ON COMMIT DROP", connection, tx)) await create.ExecuteNonQueryAsync(ct);

            long lines = 0, records = 0;
            await using (var importer = await connection.BeginBinaryImportAsync("COPY sunat_stage (ruc, razon_social, estado, condicion, ubigeo, direccion, importado_en) FROM STDIN (FORMAT BINARY)", ct))
            {
                using var zip = ZipFile.OpenRead(tempFile); var entry = zip.Entries.FirstOrDefault(x => x.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidDataException("El ZIP oficial no contiene el TXT esperado.");
                using var reader = new StreamReader(entry.Open(), Encoding.Latin1); string? line;
                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    lines++; var p = line.Split('|');
                    if (p.Length < 6 || p[0].Length != 11 || !p[0].StartsWith("20") || !p[4].StartsWith("1301")) { if (lines % 250000 == 0) state.Progress(lines, records); continue; }
                    var address = string.Join(" ", p.Skip(5).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x) && x != "-"));
                    await importer.StartRowAsync(ct); await importer.WriteAsync(p[0], NpgsqlDbType.Text, ct); await importer.WriteAsync(p[1].Trim(), NpgsqlDbType.Text, ct); await importer.WriteAsync(p[2].Trim(), NpgsqlDbType.Text, ct); await importer.WriteAsync(p[3].Trim(), NpgsqlDbType.Text, ct); await importer.WriteAsync(p[4].Trim(), NpgsqlDbType.Text, ct); await importer.WriteAsync(address, NpgsqlDbType.Text, ct); await importer.WriteAsync(started, NpgsqlDbType.TimestampTz, ct); records++;
                    if (records % 1000 == 0) state.Progress(lines, records);
                }
                await importer.CompleteAsync(ct);
            }

            const string mergeSql = """
                INSERT INTO "PadronSunat" ("Ruc", "RazonSocial", "Estado", "Condicion", "Ubigeo", "Direccion", "ImportadoEn")
                SELECT ruc, razon_social, estado, condicion, ubigeo, direccion, importado_en FROM sunat_stage
                ON CONFLICT ("Ruc") DO UPDATE SET "RazonSocial"=EXCLUDED."RazonSocial", "Estado"=EXCLUDED."Estado", "Condicion"=EXCLUDED."Condicion", "Ubigeo"=EXCLUDED."Ubigeo", "Direccion"=EXCLUDED."Direccion", "ImportadoEn"=EXCLUDED."ImportadoEn";
                DELETE FROM "PadronSunat" p WHERE p."Ruc" LIKE '20%' AND p."Ubigeo" LIKE '1301%' AND NOT EXISTS (SELECT 1 FROM sunat_stage s WHERE s.ruc=p."Ruc");
                """;
            await using (var merge = new NpgsqlCommand(mergeSql, connection, tx) { CommandTimeout = 600 }) await merge.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct); state.Set(new("COMPLETADO", lines, records, "Padrón oficial de Trujillo actualizado", started, DateTimeOffset.UtcNow));
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }
}
