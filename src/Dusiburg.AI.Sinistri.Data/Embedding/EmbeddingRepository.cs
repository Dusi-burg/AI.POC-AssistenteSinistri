using Dapper;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Data.Schema;
using Microsoft.Data.SqlClient;

namespace Dusiburg.AI.Sinistri.Data.Embedding;

/// <summary>Riga da vettorizzare: Id e contenuto già composto da <see cref="EmbeddingTextBuilder"/>.</summary>
public sealed record TestoDaVettorizzare(int Id, string Testo);

/// <summary>
/// Lettura dei testi, scrittura dei vettori ed <c>EmbeddingInfo</c> (fase-4.md §3). Le pagine si leggono per chiave primaria
/// (<c>Id &gt; @dopoId ORDER BY Id</c>, seek sull'indice cluster); il filtro sul vettore <c>NULL</c> non ha un indice e resta un
/// predicato residuo: con ~60 clausole e ~410 sinistri va bene.
/// </summary>
public sealed class EmbeddingRepository(SqlConnectionFactory connectionFactory)
{
    public async Task<int> CountAsync(TabellaEmbedding tabella, bool soloMancanti, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM dbo.{Nome(tabella)} {(soloMancanti ? $"WHERE {Colonna(tabella)} IS NULL" : "")}",
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TestoDaVettorizzare>> ReadPageAsync(
        TabellaEmbedding tabella, bool soloMancanti, int dopoId, int dimensionePagina, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();
        string mancanti = soloMancanti ? $"AND {Colonna(tabella)} IS NULL" : "";
        var parametri = new { dopoId, dimensionePagina };

        return tabella switch
        {
            TabellaEmbedding.Clausole =>
            [
                .. (await connection.QueryAsync<(int Id, TipoClausola Tipo, string Titolo, string Testo)>(new CommandDefinition(
                    $"SELECT TOP (@dimensionePagina) Id, TipoClausolaId, Titolo, Testo FROM dbo.Clausola WHERE Id > @dopoId {mancanti} ORDER BY Id",
                    parametri, cancellationToken: cancellationToken)))
                    .Select(r => new TestoDaVettorizzare(r.Id, EmbeddingTextBuilder.Clausola(r.Tipo, r.Titolo, r.Testo)))
            ],
            TabellaEmbedding.Sinistri =>
            [
                .. (await connection.QueryAsync<(int Id, CausaSinistro Causa, string Descrizione, string? Esito)>(new CommandDefinition(
                    $"SELECT TOP (@dimensionePagina) Id, CausaSinistroId, Descrizione, EsitoPerizia FROM dbo.Sinistro WHERE Id > @dopoId {mancanti} ORDER BY Id",
                    parametri, cancellationToken: cancellationToken)))
                    .Select(r => new TestoDaVettorizzare(r.Id, EmbeddingTextBuilder.Sinistro(r.Causa, r.Descrizione, r.Esito)))
            ],
            TabellaEmbedding.SinistriAntifrode =>
            [
                .. (await connection.QueryAsync<(int Id, string Descrizione)>(new CommandDefinition(
                    $"SELECT TOP (@dimensionePagina) Id, Descrizione FROM dbo.Sinistro WHERE Id > @dopoId {mancanti} ORDER BY Id",
                    parametri, cancellationToken: cancellationToken)))
                    .Select(r => new TestoDaVettorizzare(r.Id, EmbeddingTextBuilder.Antifrode(r.Descrizione)))
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(tabella), tabella, null)
        };
    }

    /// <summary>Un batch in una transazione: un <c>UPDATE</c> per riga sulla stessa connessione (con questi volumi niente TVP).</summary>
    public async Task UpdateAsync(TabellaEmbedding tabella, IReadOnlyList<(int Id, float[] Vettore)> righe, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction = connection.BeginTransaction();

        foreach ((int id, float[] vettore) in righe)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"UPDATE dbo.{Nome(tabella)} SET {Colonna(tabella)} = @embedding WHERE Id = @id",
                new { id, embedding = new VectorParameter(vettore) }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Riga unica (Id = 1): modello e runtime dei vettori salvati, letti dal controllo 9 di health.</summary>
    public async Task WriteEmbeddingInfoAsync(string modello, EmbeddingProvider provider, int dimensioni, DateTime aggiornatoIl, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE dbo.EmbeddingInfo AS t
            USING (SELECT CAST(1 AS tinyint) AS Id) AS s ON t.Id = s.Id
            WHEN MATCHED THEN UPDATE SET Modello = @modello, EmbeddingProviderId = @provider, Dimensioni = @dimensioni, AggiornatoIl = @aggiornatoIl
            WHEN NOT MATCHED THEN INSERT (Id, Modello, EmbeddingProviderId, Dimensioni, AggiornatoIl)
                VALUES (1, @modello, @provider, @dimensioni, @aggiornatoIl);
            """,
            new { modello = new DbString { Value = modello, IsAnsi = true, Length = 100 }, provider = (byte)provider, dimensioni, aggiornatoIl },
            cancellationToken: cancellationToken));
    }

    public async Task<EmbeddingInfo?> ReadEmbeddingInfoAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await DatabaseInitializer.ReadEmbeddingInfoAsync(connection, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> ReadVectorDimensionsAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await DatabaseInitializer.ReadVectorDimensionsAsync(connection, cancellationToken);
    }

    /// <summary>Nome della tabella da un enum, mai da input esterno.</summary>
    private static string Nome(TabellaEmbedding tabella) => tabella switch
    {
        TabellaEmbedding.Clausole => "Clausola",
        TabellaEmbedding.Sinistri or TabellaEmbedding.SinistriAntifrode => "Sinistro",
        _ => throw new ArgumentOutOfRangeException(nameof(tabella), tabella, null)
    };

    /// <summary>Colonna vettoriale della destinazione, mai da input esterno.</summary>
    private static string Colonna(TabellaEmbedding tabella) =>
        tabella == TabellaEmbedding.SinistriAntifrode ? "EmbeddingAntifrode" : "Embedding";
}
