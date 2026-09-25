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
        float[] vettoreDenuncia, Prodotto prodotto, int top, double distanzaMaxIntegrativa, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

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
            )
            SELECT Id, Articolo, TipoClausolaId AS Tipo, Titolo, Testo, CAST(Distanza AS float) AS Distanza,
                   CAST(RankGlobale AS int) AS Rank,
                   CAST(CASE WHEN RankGlobale > @top THEN 1 ELSE 0 END AS bit) AS Integrativa
            FROM Classificate
            WHERE RankGlobale <= @top
               OR (RankPerTipo = 1
                   AND TipoClausolaId IN (@esclusioneId, @franchigiaId)
                   AND Distanza <= @distanzaMax)
            ORDER BY Distanza, Id;
            """,
            new
            {
                q = new VectorParameter(vettoreDenuncia),
                prodottoId = (byte)prodotto,
                top,
                esclusioneId = (byte)TipoClausola.Esclusione,
                franchigiaId = (byte)TipoClausola.Franchigia,
                distanzaMax = distanzaMaxIntegrativa
            },
            cancellationToken: cancellationToken));

        return [.. righe];
    }
}
