using System.Text.Json;
using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Data.Embedding;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data.Retrieval;

/// <summary>
/// Ricerca B e statistiche (fase-5.md §3–4). I filtri stato + data usano <c>IX_Sinistro_Filtri</c> (StatoSinistroId, DataEvento),
/// il join verso <c>Polizza</c> la sua PK; la distanza è una scansione esatta sulle righe filtrate. Le statistiche leggono per PK
/// gli Id dei simili. I filtri opzionali usano <c>(@p IS NULL OR col = @p)</c>: con ~400 righe il piano non conta (Fase 10).
/// </summary>
public sealed class SinistroRepository(SqlConnectionFactory connectionFactory) : ISinistroRepository
{
    public async Task<IReadOnlyList<SinistroSimile>> CercaSimiliAsync(
        float[] vettoreDenuncia, FiltriStorico filtri, int top, CancellationToken cancellationToken)
    {
        bool escludi = filtri.EscludiSinistriIds is { Count: > 0 };
        await using SqlConnection connection = connectionFactory.CreateConnection();

        IEnumerable<Riga> righe = await connection.QueryAsync<Riga>(new CommandDefinition(
            $"""
            SELECT TOP (@top)
                   s.Id, s.Numero, s.DataEvento, s.Provincia, s.CausaSinistroId AS Causa,
                   s.Descrizione, s.EsitoPerizia, s.StatoSinistroId AS Stato, s.ImportoLiquidato,
                   CAST(VECTOR_DISTANCE('cosine', s.Embedding, @q) AS float) AS Distanza
            FROM dbo.Sinistro AS s
            JOIN dbo.Polizza AS p ON p.Id = s.PolizzaId
            WHERE p.ProdottoId = @prodottoId
              AND s.StatoSinistroId IN (@chiusoId, @respintoId)
              AND s.DataEvento >= DATEADD(YEAR, -@anni, CAST(GETDATE() AS date))
              AND (@provincia IS NULL OR s.Provincia = @provincia)
              AND (@importoMin IS NULL OR s.ImportoLiquidato >= @importoMin)
              AND (@causaId IS NULL OR s.CausaSinistroId = @causaId)
              AND s.Embedding IS NOT NULL
              {(escludi ? "AND s.Id NOT IN (SELECT CAST(value AS int) FROM OPENJSON(@escludi))" : "")}
            ORDER BY Distanza, s.Id;
            """,
            new
            {
                q = new VectorParameter(vettoreDenuncia),
                top,
                prodottoId = (byte)filtri.Prodotto,
                chiusoId = (byte)StatoSinistro.Chiuso,
                respintoId = (byte)StatoSinistro.Respinto,
                anni = filtri.AnniStorico,
                // CHAR(2): parametro ANSI a lunghezza fissa (fase-2.md); Value null diventa DBNull e disattiva il filtro.
                provincia = new DbString { Value = filtri.Provincia, IsAnsi = true, IsFixedLength = true, Length = 2 },
                importoMin = filtri.ImportoMin,
                causaId = (byte?)filtri.Causa,
                escludi = escludi ? JsonSerializer.Serialize(filtri.EscludiSinistriIds) : null
            },
            cancellationToken: cancellationToken));

        return [.. righe.Select(r => new SinistroSimile(r.Id, r.Numero, DateOnly.FromDateTime(r.DataEvento), r.Provincia, r.Causa,
            r.Descrizione, r.EsitoPerizia, r.Stato, r.ImportoLiquidato, r.Distanza))];
    }

    public async Task<StatisticheSimili> CalcolaStatisticheAsync(IReadOnlyCollection<int> sinistriIds, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleAsync<StatisticheSimili>(new CommandDefinition(
            """
            WITH Simili AS (
                SELECT s.StatoSinistroId, s.ImportoLiquidato
                FROM dbo.Sinistro AS s
                WHERE s.Id IN (SELECT CAST(value AS int) FROM OPENJSON(@ids))
            ),
            Mediana AS (
                SELECT DISTINCT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY ImportoLiquidato) OVER () AS Valore
                FROM Simili
                WHERE StatoSinistroId = @chiusoId AND ImportoLiquidato IS NOT NULL
            )
            SELECT COUNT(*)                                                                     AS NumeroCasi,
                   ISNULL(SUM(CASE WHEN StatoSinistroId = @respintoId THEN 1 ELSE 0 END), 0)   AS Respinti,
                   ISNULL(CAST(100.0 * SUM(CASE WHEN StatoSinistroId = @respintoId THEN 1 ELSE 0 END)
                        / NULLIF(COUNT(*), 0) AS decimal(5, 1)), 0)                             AS PercentualeRespinti,
                   MIN(CASE WHEN StatoSinistroId = @chiusoId THEN ImportoLiquidato END)         AS LiquidatoMin,
                   (SELECT CAST(Valore AS decimal(12, 2)) FROM Mediana)                         AS LiquidatoMediana,
                   MAX(CASE WHEN StatoSinistroId = @chiusoId THEN ImportoLiquidato END)         AS LiquidatoMax
            FROM Simili;
            """,
            new
            {
                ids = JsonSerializer.Serialize(sinistriIds),
                chiusoId = (byte)StatoSinistro.Chiuso,
                respintoId = (byte)StatoSinistro.Respinto
            },
            cancellationToken: cancellationToken));
    }

    /// <summary>Riga letta da SQL: <c>DataEvento</c> come <see cref="DateTime"/>, convertita in <see cref="DateOnly"/> nel mapping.</summary>
    private sealed record Riga(
        int Id, string Numero, DateTime DataEvento, string Provincia, CausaSinistro Causa,
        string Descrizione, string? EsitoPerizia, StatoSinistro Stato, decimal? ImportoLiquidato, double Distanza);
}
