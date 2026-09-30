using System.Diagnostics;
using Dapper;
using Dusiburg.AI.Sinistri.Core.Benchmark;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Data.Embedding;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data.Benchmark;

/// <summary>
/// Indice DiskANN e <c>VECTOR_SEARCH</c> (fase-10.md §10.1), verificati su SQL Server 2025 RTM-CU3 Express (LocalDB):
/// <list type="bullet">
/// <item>sono in anteprima: servono <c>PREVIEW_FEATURES = ON</c> sul database e <c>QUOTED_IDENTIFIER ON</c> (default di SqlClient);</item>
/// <item><c>VECTOR_SEARCH</c> si riconosce solo in un batch compilato nel database con l'anteprima attiva;</item>
/// <item>con l'indice la tabella diventa <b>di sola lettura</b> (errore 42231): per questo si usa solo nel DB del banco di prova;</item>
/// <item>i filtri SQL si applicano dopo la ricerca approssimata, quindi i risultati possono essere meno di <c>TOP</c>.</item>
/// </list>
/// </summary>
public sealed class RicercaApprossimataRepository(SqlConnectionFactory connectionFactory) : IRicercaApprossimataRepository
{
    public const string NomeIndice = "IX_Sinistro_Embedding_DiskAnn";

    /// <summary>Crea l'indice se manca; restituisce la durata della creazione, null se c'era già.</summary>
    public async Task<TimeSpan?> CreaIndiceAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "ALTER DATABASE SCOPED CONFIGURATION SET PREVIEW_FEATURES = ON;", cancellationToken: cancellationToken));

        bool esiste = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CAST(COUNT(*) AS bit) FROM sys.indexes WHERE name = @nome AND object_id = OBJECT_ID(N'dbo.Sinistro')",
            new { nome = NomeIndice }, cancellationToken: cancellationToken));

        if (esiste)
        {
            return null;
        }

        var cronometro = Stopwatch.StartNew();
        await connection.ExecuteAsync(new CommandDefinition(
            $"CREATE VECTOR INDEX {NomeIndice} ON dbo.Sinistro (Embedding) WITH (METRIC = 'cosine', TYPE = 'diskann');",
            commandTimeout: 3600, cancellationToken: cancellationToken));

        return cronometro.Elapsed;
    }

    public async Task<IReadOnlyList<SinistroSimile>> CercaSimiliAsync(
        float[] vettore, FiltriStorico filtri, int top, int topN, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        // Stessi filtri e stesso ordinamento di SinistroRepository.CercaSimiliAsync, applicati ai topN vicini dell'indice.
        IEnumerable<Riga> righe = await connection.QueryAsync<Riga>(new CommandDefinition(
            """
            SELECT TOP (@top)
                   s.Id, s.Numero, s.DataEvento, s.Provincia, s.CausaSinistroId AS Causa,
                   s.Descrizione, s.EsitoPerizia, s.StatoSinistroId AS Stato, s.ImportoLiquidato,
                   CAST(r.distance AS float) AS Distanza
            FROM VECTOR_SEARCH(TABLE = dbo.Sinistro AS s, COLUMN = Embedding, SIMILAR_TO = @q, METRIC = 'cosine', TOP_N = @topN) AS r
            JOIN dbo.Polizza AS p ON p.Id = s.PolizzaId
            WHERE p.ProdottoId = @prodottoId
              AND s.StatoSinistroId IN (@chiusoId, @respintoId)
              AND s.DataEvento >= DATEADD(YEAR, -@anni, CAST(GETDATE() AS date))
              AND (@provincia IS NULL OR s.Provincia = @provincia)
              AND (@importoMin IS NULL OR s.ImportoLiquidato >= @importoMin)
              AND (@causaId IS NULL OR s.CausaSinistroId = @causaId)
            ORDER BY r.distance, s.Id;
            """,
            new
            {
                q = new VectorParameter(vettore),
                top,
                topN,
                prodottoId = (byte)filtri.Prodotto,
                chiusoId = (byte)StatoSinistro.Chiuso,
                respintoId = (byte)StatoSinistro.Respinto,
                anni = filtri.AnniStorico,
                provincia = new DbString { Value = filtri.Provincia, IsAnsi = true, IsFixedLength = true, Length = 2 },
                importoMin = filtri.ImportoMin,
                causaId = (byte?)filtri.Causa
            },
            cancellationToken: cancellationToken));

        return [.. righe.Select(r => new SinistroSimile(r.Id, r.Numero, DateOnly.FromDateTime(r.DataEvento), r.Provincia, r.Causa,
            r.Descrizione, r.EsitoPerizia, r.Stato, r.ImportoLiquidato, r.Distanza))];
    }

    private sealed record Riga(
        int Id, string Numero, DateTime DataEvento, string Provincia, CausaSinistro Causa,
        string Descrizione, string? EsitoPerizia, StatoSinistro Stato, decimal? ImportoLiquidato, double Distanza);
}
