using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Data.Embedding;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data.Retrieval;

/// <summary>
/// Ricerca A (fase-5.md §2, D10): query unica con le prime <c>@top</c> per distanza più la migliore esclusione e la migliore
/// franchigia entro la soglia. Il filtro per prodotto usa la colonna iniziale di <c>UQ_Clausola_Prodotto_Articolo</c>; la distanza
/// è una scansione esatta sulle ~35 clausole del prodotto (nessun indice vettoriale fino alla Fase 10).
/// </summary>
public sealed class ClausolaRepository(SqlConnectionFactory connectionFactory) : IClausolaRepository
{
    public async Task<IReadOnlyList<ClausolaTrovata>> CercaPertinentiAsync(
        float[] vettoreDenuncia, Prodotto prodotto, int top, double distanzaMaxIntegrativa, string? articoloFranchigiaBase,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        // Selezionate: le prime @top più la migliore esclusione e la migliore franchigia entro la soglia (D10). Se nessuna franchigia
        // è stata scelta si aggiunge la franchigia di base del prodotto (fase-6.md, CHECKPOINT 6), a qualunque distanza.
        IEnumerable<ClausolaTrovata> righe = await connection.QueryAsync<ClausolaTrovata>(new CommandDefinition(
            """
            WITH Distanze AS (
                SELECT c.Id, c.Articolo, c.TipoClausolaId, c.Titolo, c.Testo,
                       VECTOR_DISTANCE('cosine', c.Embedding, @q) AS Distanza
                FROM dbo.Clausola AS c
                WHERE c.ProdottoId = @prodottoId
                  AND c.Embedding IS NOT NULL
            ),
            Classificate AS (
                SELECT *,
                       ROW_NUMBER() OVER (ORDER BY Distanza, Id)                             AS RankGlobale,
                       ROW_NUMBER() OVER (PARTITION BY TipoClausolaId ORDER BY Distanza, Id) AS RankPerTipo
                FROM Distanze
            ),
            Selezionate AS (
                SELECT *,
                       CASE WHEN RankGlobale <= @top
                                 OR (RankPerTipo = 1 AND TipoClausolaId IN (@esclusioneId, @franchigiaId) AND Distanza <= @distanzaMax)
                            THEN 1 ELSE 0 END AS Scelta
                FROM Classificate
            )
            SELECT Id, Articolo, TipoClausolaId AS Tipo, Titolo, Testo, CAST(Distanza AS float) AS Distanza,
                   CAST(RankGlobale AS int) AS Rank,
                   CAST(CASE WHEN RankGlobale > @top THEN 1 ELSE 0 END AS bit) AS Integrativa
            FROM Selezionate
            WHERE Scelta = 1
               OR (@franchigiaBase IS NOT NULL
                   AND Articolo = @franchigiaBase
                   AND TipoClausolaId = @franchigiaId
                   AND NOT EXISTS (SELECT 1 FROM Selezionate AS s WHERE s.Scelta = 1 AND s.TipoClausolaId = @franchigiaId))
            ORDER BY Distanza, Id;
            """,
            new
            {
                q = new VectorParameter(vettoreDenuncia),
                prodottoId = (byte)prodotto,
                top,
                esclusioneId = (byte)TipoClausola.Esclusione,
                franchigiaId = (byte)TipoClausola.Franchigia,
                distanzaMax = distanzaMaxIntegrativa,
                // Articolo è NVARCHAR(20): parametro Unicode della stessa lunghezza; null disattiva la franchigia di base.
                franchigiaBase = new DbString { Value = string.IsNullOrWhiteSpace(articoloFranchigiaBase) ? null : articoloFranchigiaBase, Length = 20 }
            },
            cancellationToken: cancellationToken));

        return [.. righe];
    }
}
