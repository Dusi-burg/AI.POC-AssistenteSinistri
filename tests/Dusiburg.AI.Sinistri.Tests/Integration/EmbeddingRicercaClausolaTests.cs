using Dapper;
using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Embedding;
using Dusiburg.AI.Sinistri.Data.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>
/// Percorso completo di una clausola con il modello di embedding vero (Ollama, <c>embeddinggemma</c>): testo → vettore → DB → ricerca
/// per somiglianza con <c>VECTOR_DISTANCE</c>. A differenza degli altri test di integrazione (vettori a 4 dimensioni scritti a mano)
/// usa un database dedicato con <c>VECTOR(768)</c>, così <c>Sinistri_Test</c> non si tocca. Senza Ollama raggiungibile il test viene
/// ignorato.
/// </summary>
[Category("Integration")]
[Category("Ollama")]
[NonParallelizable]
public class EmbeddingRicercaClausolaTests
{
    /// <summary>
    /// Distanza coseno massima per considerare "trovata" una clausola: tra 0 (identica) e 2 (opposta). È la soglia tarata in Fase 5
    /// (<see cref="RetrievalOptions.DistanzaMaxClausolaIntegrativa"/>, 0,70): con <c>embeddinggemma</c> la denuncia affine sta a ~0,59,
    /// quella fuori tema a ~0,80.
    /// </summary>
    private static readonly double SogliaSomiglianza = new RetrievalOptions().DistanzaMaxClausolaIntegrativa;

    private static readonly string ConnectionString =
        new SqlConnectionStringBuilder(TestDatabase.ConnectionString) { InitialCatalog = "Sinistri_Test_Embedding" }.ConnectionString;

    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task ClausolaVettorizzata_RicercaPerSomiglianza_TrovataSoloConTestoAffine()
    {
        //SETUP
        // Configurazione con i soli default del codice (SinistriOptions): Ollama su 127.0.0.1:11434, modello embeddinggemma, 768
        // dimensioni, embedding su CPU. È la stessa configurazione dell'applicazione quando non si imposta nessuna manopola.
        SinistriOptions opzioni = SinistriOptions.FromConfiguration(new ConfigurationBuilder().Build());
        await IgnoraSeOllamaNonRaggiungibileAsync(opzioni);

        // Servizio di embedding come in produzione: la factory crea il generatore Ollama, EmbeddingService aggiunge i prefissi del
        // profilo del modello ("title: none | text: " per i documenti, "task: search result | query: " per le query).
        using IEmbeddingGenerator<string, Embedding<float>> generatore = new EmbeddingGeneratorFactory(opzioni, NullLoggerFactory.Instance).Create();
        var embedding = new EmbeddingService(generatore, opzioni, Microsoft.Extensions.Options.Options.Create(new RetrievalOptions()));

        // Database vuoto con schema e lookup, colonne VECTOR della dimensione del modello (768).
        await DatabaseInitializer.RecreateAsync(ConnectionString, opzioni.EmbeddingDimensions, CancellationToken);
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken);

        // Passo 1 — la clausola entra nel DB senza vettore, come fa il seed di DbInit: l'embedding si calcola in un secondo momento.
        const string titolo = "Acqua condotta";
        const string testo = "La Società indennizza i danni materiali e diretti causati all'abitazione da fuoriuscita di acqua " +
                             "a seguito di rottura accidentale degli impianti idrici, igienici e di riscaldamento.";
        int id = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO dbo.Clausola (ProdottoId, Articolo, TipoClausolaId, Titolo, Testo)
            OUTPUT INSERTED.Id
            VALUES (@prodotto, N'Art. 2.1', @tipo, @titolo, @testo)
            """,
            new { prodotto = (byte)Prodotto.CasaFabbricati, tipo = (byte)TipoClausola.Garanzia, titolo, testo });

        // Passo 2 — il testo da vettorizzare si costruisce come nella pipeline: etichetta del tipo, titolo e testo
        // ("Garanzia - Acqua condotta. La Società indennizza...").
        string testoClausola = EmbeddingTextBuilder.Clausola(TipoClausola.Garanzia, titolo, testo);

        // Passo 3 — il modello trasforma il testo in un vettore di 768 numeri (embedding "documento", con il prefisso del documento).
        float[] vettoreClausola = (await embedding.EmbedDocumentsAsync([testoClausola], CancellationToken)).Single();
        Assert.That(vettoreClausola, Has.Length.EqualTo(opzioni.EmbeddingDimensions));

        // Passo 4 — il vettore si salva nella colonna Embedding (VECTOR(768)) con lo stesso repository della pipeline di embedding.
        await new EmbeddingRepository(new SqlConnectionFactory(ConnectionString))
            .UpdateAsync(TabellaEmbedding.Clausole, [(id, vettoreClausola)], CancellationToken);

        // Due denunce: la prima descrive un danno coperto dalla clausola con parole diverse, la seconda parla d'altro.
        const string denunciaAffine = "Si è rotto un tubo dell'acqua sotto il lavandino del bagno e ha allagato il pavimento del soggiorno.";
        const string denunciaDiversa = "Il progetto strutturale del capannone conteneva un errore di calcolo dei carichi sul solaio.";

        //SUT
        // Passo 5 — ogni denuncia diventa un vettore "query" (prefisso della query) e si cerca la clausola più vicina con
        // VECTOR_DISTANCE: si restituisce solo se la distanza coseno è entro la soglia.
        (int Id, double Distanza)? trovataAffine = await CercaClausolaAsync(connection, embedding, denunciaAffine);
        (int Id, double Distanza)? trovataDiversa = await CercaClausolaAsync(connection, embedding, denunciaDiversa);

        // Passo 6 — il testo affine ritrova la clausola, quello fuori tema no. Le distanze nel log aiutano a tarare la soglia.
        double distanzaAffine = await DistanzaAsync(connection, id, await embedding.EmbedQueryAsync(EmbeddingTextBuilder.Denuncia(denunciaAffine, null), CancellationToken));
        double distanzaDiversa = await DistanzaAsync(connection, id, await embedding.EmbedQueryAsync(EmbeddingTextBuilder.Denuncia(denunciaDiversa, null), CancellationToken));
        await TestContext.Out.WriteLineAsync($"Distanza denuncia affine: {distanzaAffine:F3}; denuncia diversa: {distanzaDiversa:F3}; soglia: {SogliaSomiglianza}");

        Assert.That(trovataAffine, Is.Not.Null, "La denuncia affine deve ritrovare la clausola.");
        Assert.That(trovataAffine!.Value.Id, Is.EqualTo(id));
        Assert.That(trovataAffine.Value.Distanza, Is.LessThan(SogliaSomiglianza));
        Assert.That(trovataDiversa, Is.Null, "La denuncia fuori tema non deve ritrovare la clausola.");
        Assert.That(distanzaDiversa, Is.GreaterThan(distanzaAffine));
    }

    /// <summary>
    /// Vettorizza la denuncia come query e interroga il DB: la clausola più vicina, se entro <see cref="SogliaSomiglianza"/>.
    /// <c>VECTOR_DISTANCE('cosine', ...)</c> è la stessa funzione usata da <c>ClausolaRepository</c>.
    /// </summary>
    private static async Task<(int Id, double Distanza)?> CercaClausolaAsync(SqlConnection connection, EmbeddingService embedding, string denuncia)
    {
        float[] vettoreDenuncia = await embedding.EmbedQueryAsync(EmbeddingTextBuilder.Denuncia(denuncia, null), CancellationToken);

        IEnumerable<(int Id, double Distanza)> righe = await connection.QueryAsync<(int, double)>(
            """
            SELECT TOP (1) Id, Distanza
            FROM (
                SELECT Id, CAST(VECTOR_DISTANCE('cosine', Embedding, @q) AS float) AS Distanza
                FROM dbo.Clausola
                WHERE Embedding IS NOT NULL
            ) AS d
            WHERE Distanza <= @soglia
            ORDER BY Distanza
            """,
            new { q = new VectorParameter(vettoreDenuncia), soglia = SogliaSomiglianza });

        return righe.Select(r => ((int, double)?)r).SingleOrDefault();
    }

    /// <summary>Distanza coseno tra la clausola e un vettore, senza soglia: solo per il log.</summary>
    private static Task<double> DistanzaAsync(SqlConnection connection, int id, float[] vettore) =>
        connection.ExecuteScalarAsync<double>(
            "SELECT CAST(VECTOR_DISTANCE('cosine', Embedding, @q) AS float) FROM dbo.Clausola WHERE Id = @id",
            new { q = new VectorParameter(vettore), id });

    private static async Task IgnoraSeOllamaNonRaggiungibileAsync(SinistriOptions opzioni)
    {
        using var http = new HttpClient { BaseAddress = opzioni.EmbeddingEndpoint, Timeout = TimeSpan.FromSeconds(3) };

        try
        {
            (await http.GetAsync("api/tags", CancellationToken)).EnsureSuccessStatusCode();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Assert.Ignore($"Ollama non raggiungibile su {opzioni.EmbeddingEndpoint}: test che richiede il modello di embedding vero.");
        }
    }
}
