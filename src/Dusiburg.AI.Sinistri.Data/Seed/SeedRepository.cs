using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Data.Schema;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data.Seed;

public sealed record ConteggioSeed(int Contraenti, int Riparatori, int Polizze, int Clausole, int Sinistri);

public sealed record DistribuzioneCausaStato(CausaSinistro Causa, StatoSinistro Stato, int Sinistri);

public sealed record DistribuzioneProdottoProvincia(Prodotto Prodotto, string Provincia, int Sinistri);

/// <summary>
/// Inserimento dei dati sintetici nel DB appena ricreato da DbInit (fase-3.md, passo 6). Gli Id sono quelli del generatore
/// (<c>IDENTITY_INSERT</c>): il DB è vuoto, quindi non servono controlli su righe già presenti. Con ~400 righe basta Dapper.
/// </summary>
public static class SeedRepository
{
    public const string ClausoleScript = "003_seed_clausole.sql";

    public static Task SeedClausoleAsync(SqlConnection connection, CancellationToken cancellationToken) =>
        SqlScriptRunner.RunAsync(connection, ClausoleScript, new Dictionary<string, string>(), cancellationToken);

    public static async Task InsertAsync(SqlConnection connection, DatiSintetici dati, CancellationToken cancellationToken)
    {
        await using SqlTransaction transaction = connection.BeginTransaction();

        await InsertWithIdsAsync(connection, transaction, "Contraente",
            "INSERT INTO dbo.Contraente (Id, Nominativo, Provincia) VALUES (@Id, @Nominativo, @Provincia)",
            dati.Contraenti.Select(c => new { c.Id, c.Nominativo, c.Provincia }), cancellationToken);

        await InsertWithIdsAsync(connection, transaction, "Riparatore",
            "INSERT INTO dbo.Riparatore (Id, RagioneSociale) VALUES (@Id, @RagioneSociale)",
            dati.Riparatori, cancellationToken);

        await InsertWithIdsAsync(connection, transaction, "Polizza",
            """
            INSERT INTO dbo.Polizza (Id, Numero, ProdottoId, ContraenteId, Decorrenza, Scadenza, Massimale, Franchigia)
            VALUES (@Id, @Numero, @ProdottoId, @ContraenteId, @Decorrenza, @Scadenza, @Massimale, @Franchigia)
            """,
            dati.Polizze.Select(p => new
            {
                p.Id, p.Numero, ProdottoId = (byte)p.Prodotto, p.ContraenteId,
                Decorrenza = p.Decorrenza.ToDateTime(TimeOnly.MinValue), Scadenza = p.Scadenza.ToDateTime(TimeOnly.MinValue),
                p.Massimale, p.Franchigia
            }),
            cancellationToken);

        await InsertWithIdsAsync(connection, transaction, "Sinistro",
            """
            INSERT INTO dbo.Sinistro (Id, Numero, PolizzaId, RiparatoreId, DataEvento, DataDenuncia, Provincia, CausaSinistroId,
                Descrizione, EsitoPerizia, StatoSinistroId, ImportoRiservato, ImportoLiquidato)
            VALUES (@Id, @Numero, @PolizzaId, @RiparatoreId, @DataEvento, @DataDenuncia, @Provincia, @CausaSinistroId,
                @Descrizione, @EsitoPerizia, @StatoSinistroId, @ImportoRiservato, @ImportoLiquidato)
            """,
            dati.Sinistri.Select(s => new
            {
                s.Id, s.Numero, s.PolizzaId, s.RiparatoreId,
                DataEvento = s.DataEvento.ToDateTime(TimeOnly.MinValue), DataDenuncia = s.DataDenuncia.ToDateTime(TimeOnly.MinValue),
                s.Provincia, CausaSinistroId = (byte)s.Causa, s.Descrizione, s.EsitoPerizia, StatoSinistroId = (byte)s.Stato,
                s.ImportoRiservato, s.ImportoLiquidato
            }),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Conteggi per tabella: scansioni complete su tabelle da poche centinaia di righe, solo per il riepilogo di DbInit.</summary>
    public static async Task<ConteggioSeed> ReadConteggioAsync(SqlConnection connection, CancellationToken cancellationToken) =>
        await connection.QuerySingleAsync<ConteggioSeed>(new CommandDefinition(
            """
            SELECT (SELECT COUNT(*) FROM dbo.Contraente) AS Contraenti, (SELECT COUNT(*) FROM dbo.Riparatore) AS Riparatori,
                   (SELECT COUNT(*) FROM dbo.Polizza) AS Polizze, (SELECT COUNT(*) FROM dbo.Clausola) AS Clausole,
                   (SELECT COUNT(*) FROM dbo.Sinistro) AS Sinistri
            """,
            cancellationToken: cancellationToken));

    public static async Task<IReadOnlyList<DistribuzioneCausaStato>> ReadCausaStatoAsync(SqlConnection connection, CancellationToken cancellationToken) =>
        [.. await connection.QueryAsync<DistribuzioneCausaStato>(new CommandDefinition(
            """
            SELECT CausaSinistroId AS Causa, StatoSinistroId AS Stato, COUNT(*) AS Sinistri
            FROM dbo.Sinistro
            GROUP BY CausaSinistroId, StatoSinistroId
            ORDER BY CausaSinistroId, StatoSinistroId
            """,
            cancellationToken: cancellationToken))];

    public static async Task<IReadOnlyList<DistribuzioneProdottoProvincia>> ReadProdottoProvinciaAsync(
        SqlConnection connection, int top, CancellationToken cancellationToken) =>
        [.. await connection.QueryAsync<DistribuzioneProdottoProvincia>(new CommandDefinition(
            """
            SELECT TOP (@top) p.ProdottoId AS Prodotto, s.Provincia, COUNT(*) AS Sinistri
            FROM dbo.Sinistro AS s
            JOIN dbo.Polizza AS p ON p.Id = s.PolizzaId
            GROUP BY p.ProdottoId, s.Provincia
            ORDER BY COUNT(*) DESC, p.ProdottoId, s.Provincia
            """,
            new { top },
            cancellationToken: cancellationToken))];

    private static async Task InsertWithIdsAsync<T>(
        SqlConnection connection, SqlTransaction transaction, string table, string sql, IEnumerable<T> rows, CancellationToken cancellationToken)
    {
        // Nome della tabella da un elenco fisso nel codice, mai da input esterno. IDENTITY_INSERT vale per la sessione.
        await connection.ExecuteAsync(new CommandDefinition($"SET IDENTITY_INSERT dbo.{table} ON", transaction: transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(sql, rows, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition($"SET IDENTITY_INSERT dbo.{table} OFF", transaction: transaction, cancellationToken: cancellationToken));
    }
}
