using System.Text.Json;
using Dapper;
using Dusiburg.AI.Sinistri.Core.Antifrode;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Data.Embedding;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data.Antifrode;

/// <summary>
/// Query dell'antifrode (fase-7.md §2–3): distanza esatta su tutti i sinistri del periodo, di qualsiasi prodotto e stato. La nuova
/// denuncia si confronta con <c>EmbeddingAntifrode</c> (sola descrizione), le coppie di sinistri con <c>Embedding</c> (§6 bis). Con
/// ~350 sinistri in 24 mesi il self-join fa ~60.000 confronti: nessun indice vettoriale necessario (Fase 10).
/// </summary>
public sealed class AntifrodeRepository(SqlConnectionFactory connectionFactory) : IAntifrodeRepository
{
    public async Task<IReadOnlyList<SegnalazioneDuplicato>> CercaDuplicatiDenunciaAsync(
        float[] vettoreDenuncia, string numeroPolizza, int? riparatoreId, int mesi, double soglia, int top, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        // Il contraente della denuncia è quello della polizza indicata: lo si ricava qui, senza portarlo nei contratti della scheda.
        IEnumerable<RigaDenuncia> righe = await connection.QueryAsync<RigaDenuncia>(new CommandDefinition(
            """
            DECLARE @da date = DATEADD(MONTH, -@mesi, CAST(GETDATE() AS date));
            DECLARE @contraenteId int = (SELECT ContraenteId FROM dbo.Polizza WHERE Numero = @numeroPolizza);

            WITH Vicini AS (
                SELECT s.Numero, s.DataDenuncia, s.CausaSinistroId, s.StatoSinistroId, s.Descrizione, s.RiparatoreId, p.ContraenteId,
                       CAST(VECTOR_DISTANCE('cosine', s.EmbeddingAntifrode, @q) AS float) AS Distanza
                FROM dbo.Sinistro AS s
                JOIN dbo.Polizza AS p ON p.Id = s.PolizzaId
                WHERE s.DataDenuncia >= @da
                  AND s.EmbeddingAntifrode IS NOT NULL
            )
            SELECT TOP (@top)
                   v.Numero, v.DataDenuncia, v.CausaSinistroId AS Causa, v.StatoSinistroId AS Stato,
                   c.Nominativo AS Contraente, r.RagioneSociale AS Riparatore, v.Descrizione, v.Distanza,
                   CAST(CASE WHEN v.ContraenteId = @contraenteId THEN 1 ELSE 0 END AS bit) AS StessoContraente,
                   CAST(CASE WHEN @riparatoreId IS NOT NULL AND v.RiparatoreId = @riparatoreId THEN 1 ELSE 0 END AS bit) AS StessoRiparatore
            FROM Vicini AS v
            JOIN dbo.Contraente AS c ON c.Id = v.ContraenteId
            LEFT JOIN dbo.Riparatore AS r ON r.Id = v.RiparatoreId
            WHERE v.Distanza < @soglia
            ORDER BY StessoContraente DESC, StessoRiparatore DESC, v.Distanza, v.Numero;
            """,
            new
            {
                q = new VectorParameter(vettoreDenuncia),
                numeroPolizza = new DbString { Value = numeroPolizza.Trim(), IsAnsi = true, Length = 30 },
                riparatoreId,
                mesi,
                soglia,
                top
            },
            cancellationToken: cancellationToken));

        return [.. righe.Select(r => new SegnalazioneDuplicato(
            r.Numero, DateOnly.FromDateTime(r.DataDenuncia), r.Causa, r.Stato, r.Contraente, r.Riparatore, r.Distanza,
            Motivi.Da(r.StessoContraente, r.StessoRiparatore), r.Descrizione))];
    }

    public async Task<IReadOnlyList<CoppiaSospetta>> CercaCoppieAsync(int mesi, double soglia, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        // a.Id < b.Id: niente autoconfronti né coppie doppie. RiparatoreId null non è mai "stesso riparatore".
        IEnumerable<RigaCoppia> righe = await connection.QueryAsync<RigaCoppia>(new CommandDefinition(
            """
            DECLARE @da date = DATEADD(MONTH, -@mesi, CAST(GETDATE() AS date));

            WITH Periodo AS (
                SELECT s.Id, s.Numero, s.DataDenuncia, s.Descrizione, s.RiparatoreId, p.ContraenteId, s.Embedding
                FROM dbo.Sinistro AS s
                JOIN dbo.Polizza AS p ON p.Id = s.PolizzaId
                WHERE s.DataDenuncia >= @da
                  AND s.Embedding IS NOT NULL
            ),
            Coppie AS (
                SELECT a.Numero AS NumeroA, b.Numero AS NumeroB,
                       CAST(VECTOR_DISTANCE('cosine', a.Embedding, b.Embedding) AS float) AS Distanza,
                       CAST(CASE WHEN a.ContraenteId = b.ContraenteId THEN 1 ELSE 0 END AS bit) AS StessoContraente,
                       CAST(CASE WHEN a.RiparatoreId = b.RiparatoreId THEN 1 ELSE 0 END AS bit) AS StessoRiparatore,
                       ABS(DATEDIFF(DAY, a.DataDenuncia, b.DataDenuncia)) AS GiorniTraDenunce,
                       a.Descrizione AS DescrizioneA, b.Descrizione AS DescrizioneB
                FROM Periodo AS a
                JOIN Periodo AS b ON a.Id < b.Id
            )
            SELECT NumeroA, NumeroB, Distanza, StessoContraente, StessoRiparatore, GiorniTraDenunce, DescrizioneA, DescrizioneB
            FROM Coppie
            WHERE Distanza < @soglia
            ORDER BY Distanza, NumeroA, NumeroB;
            """,
            new { mesi, soglia },
            cancellationToken: cancellationToken));

        return [.. righe.Select(r => new CoppiaSospetta(
            r.NumeroA, r.NumeroB, r.Distanza, Motivi.Da(r.StessoContraente, r.StessoRiparatore), r.GiorniTraDenunce,
            r.DescrizioneA, r.DescrizioneB))];
    }

    public async Task<IReadOnlyList<DistanzaCoppiaAttesa>> DistanzeCoppieAsync(
        IReadOnlyList<CoppiaDuplicati> coppie, int mesi, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        IEnumerable<RigaDistanza> righe = await connection.QueryAsync<RigaDistanza>(new CommandDefinition(
            """
            DECLARE @da date = DATEADD(MONTH, -@mesi, CAST(GETDATE() AS date));

            SELECT j.A, j.B,
                   CAST(VECTOR_DISTANCE('cosine', a.Embedding, b.Embedding) AS float) AS Distanza,
                   CAST(CASE WHEN a.DataDenuncia >= @da AND b.DataDenuncia >= @da THEN 1 ELSE 0 END AS bit) AS InPeriodo
            FROM OPENJSON(@coppie) WITH (A varchar(30) '$.a', B varchar(30) '$.b') AS j
            LEFT JOIN dbo.Sinistro AS a ON a.Numero = j.A
            LEFT JOIN dbo.Sinistro AS b ON b.Numero = j.B;
            """,
            new { coppie = JsonSerializer.Serialize(coppie.Select(c => new { a = c.Originale, b = c.Duplicato })), mesi },
            cancellationToken: cancellationToken));

        Dictionary<(string, string), RigaDistanza> perCoppia = righe.DistinctBy(r => (r.A, r.B)).ToDictionary(r => (r.A, r.B));

        return [.. coppie.Select(c => perCoppia.TryGetValue((c.Originale, c.Duplicato), out RigaDistanza? riga)
            ? new DistanzaCoppiaAttesa(c, riga.Distanza, riga.InPeriodo)
            : new DistanzaCoppiaAttesa(c, null, false))];
    }

    private sealed record RigaDenuncia(
        string Numero, DateTime DataDenuncia, CausaSinistro Causa, StatoSinistro Stato, string Contraente, string? Riparatore,
        string Descrizione, double Distanza, bool StessoContraente, bool StessoRiparatore);

    private sealed record RigaCoppia(
        string NumeroA, string NumeroB, double Distanza, bool StessoContraente, bool StessoRiparatore, int GiorniTraDenunce,
        string DescrizioneA, string DescrizioneB);

    private sealed record RigaDistanza(string A, string B, double? Distanza, bool InPeriodo);
}
