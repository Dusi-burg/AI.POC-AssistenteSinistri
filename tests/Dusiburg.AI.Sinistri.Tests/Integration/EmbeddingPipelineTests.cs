using Dapper;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Data;
using Dusiburg.AI.Sinistri.Data.Embedding;
using Dusiburg.AI.Sinistri.Data.Schema;
using Dusiburg.AI.Sinistri.Data.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Embedding;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Dusiburg.AI.Sinistri.Tests.Integration;

/// <summary>Repository e pipeline di embedding su LocalDB (<c>VECTOR(4)</c>), con un servizio di embedding finto.</summary>
[Category("Integration")]
[NonParallelizable]
public class EmbeddingPipelineTests
{
    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    private readonly EmbeddingRepository _repository = new(new SqlConnectionFactory(TestDatabase.ConnectionString));

    /// <summary>Gli altri test di integrazione si aspettano un DB senza embedding calcolati.</summary>
    [OneTimeTearDown]
    public async Task RipristinaAsync() => await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken.None);

    [Test]
    public async Task UpdateVector_RoundTrip()
    {
        //SETUP
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken);
        await using SqlConnection connection = await TestDatabase.OpenAsync();
        await SeedRepository.SeedClausoleAsync(connection, CancellationToken);
        int id = await connection.ExecuteScalarAsync<int>("SELECT MIN(Id) FROM dbo.Clausola");
        float[] vettore = [0.5f, -0.25f, 0.125f, 1f];

        //SUT
        await _repository.UpdateAsync(TabellaEmbedding.Clausole, [(id, vettore)], CancellationToken);

        // Coseno 0 con sé stesso; euclidea 0 perché il coseno non vede un vettore scalato.
        (double coseno, double euclidea) = await connection.QuerySingleAsync<(double, double)>(
            """
            SELECT VECTOR_DISTANCE('cosine', Embedding, CAST('[0.5,-0.25,0.125,1]' AS VECTOR(4))),
                   VECTOR_DISTANCE('euclidean', Embedding, CAST('[0.5,-0.25,0.125,1]' AS VECTOR(4)))
            FROM dbo.Clausola WHERE Id = @id
            """,
            new { id });
        Assert.That(coseno, Is.EqualTo(0).Within(1e-6));
        Assert.That(euclidea, Is.EqualTo(0).Within(1e-6));
        Assert.That(await _repository.CountAsync(TabellaEmbedding.Clausole, soloMancanti: true, CancellationToken), Is.EqualTo(59));
    }

    [Test]
    public async Task Embed_TuttoPoiSoloMancanti_NienteDaElaborare()
    {
        //SETUP
        await PopolaAsync();
        var servizio = new EmbeddingFinto();
        EmbeddingPipeline pipeline = Pipeline(servizio, new() { ["EMBEDDING_DIMENSIONS"] = "4" });
        TabellaEmbedding[] tutte = Enum.GetValues<TabellaEmbedding>();

        //SUT
        EmbedEsito completo = await pipeline.RunAsync(new EmbedRichiesta(SoloMancanti: false, tutte), null, CancellationToken);
        EmbedEsito mancanti = await pipeline.RunAsync(new EmbedRichiesta(SoloMancanti: true, tutte), null, CancellationToken);

        Assert.That(completo.Tabelle.Select(t => (t.Tabella, t.Vettori)), Is.EqualTo(new[]
        {
            (TabellaEmbedding.Clausole, 60), (TabellaEmbedding.Sinistri, 410), (TabellaEmbedding.SinistriAntifrode, 410)
        }));
        Assert.That(completo.RigheSenzaEmbedding, Is.Zero);
        Assert.That(servizio.Testi, Has.Count.EqualTo(880));
        Assert.That(servizio.Testi, Has.Some.StartsWith("Franchigia / scoperto / limite - ").And.Some.StartsWith("Causa: Acqua condotta. "));
        // Il vettore antifrode è la sola descrizione: niente causa, niente esito di perizia.
        Assert.That(servizio.Testi.Skip(470), Has.None.StartsWith("Causa: ").And.None.Contains("Esito perizia: "));
        Assert.That(mancanti.Tabelle.Sum(t => t.Vettori), Is.Zero);
        Assert.That(await _repository.ReadEmbeddingInfoAsync(CancellationToken), Has.Property("Modello").EqualTo("embeddinggemma").And.Property("Dimensioni").EqualTo(4));
    }

    [Test]
    public async Task Embed_ModelloCambiato_RifiutaIlLavoroParziale()
    {
        //SETUP
        await PopolaAsync();
        await Pipeline(new EmbeddingFinto(), new() { ["EMBEDDING_DIMENSIONS"] = "4" })
            .RunAsync(new EmbedRichiesta(false, Enum.GetValues<TabellaEmbedding>()), null, CancellationToken);
        EmbeddingPipeline altroModello = Pipeline(new EmbeddingFinto(), new() { ["EMBEDDING_DIMENSIONS"] = "4", ["EMBEDDING_MODEL"] = "bge-m3" });

        //SUT
        Assert.That(() => altroModello.RunAsync(new EmbedRichiesta(true, Enum.GetValues<TabellaEmbedding>()), null, CancellationToken),
            Throws.InvalidOperationException.With.Message.Contains("embeddinggemma").And.Message.Contains("bge-m3"));
        Assert.That(() => altroModello.RunAsync(new EmbedRichiesta(false, [TabellaEmbedding.Clausole]), null, CancellationToken),
            Throws.InvalidOperationException);
        EmbedEsito completo = await altroModello.RunAsync(new EmbedRichiesta(false, Enum.GetValues<TabellaEmbedding>()), null, CancellationToken);

        Assert.That(completo.Tabelle.Sum(t => t.Vettori), Is.EqualTo(880));
        Assert.That((await _repository.ReadEmbeddingInfoAsync(CancellationToken))?.Modello, Is.EqualTo("bge-m3"));
    }

    [Test]
    public async Task Embed_DimensioneDiversaDalleColonne_Rifiuta()
    {
        //SETUP
        await PopolaAsync();
        EmbeddingPipeline pipeline = Pipeline(new EmbeddingFinto(), new() { ["EMBEDDING_DIMENSIONS"] = "768" });

        //SUT
        Assert.That(() => pipeline.RunAsync(new EmbedRichiesta(false, Enum.GetValues<TabellaEmbedding>()), null, CancellationToken),
            Throws.InvalidOperationException.With.Message.Contains("VECTOR(4)").And.Message.Contains("rieseguire DbInit"));
    }

    [Test]
    public async Task HealthControllo9_RigheSenzaEmbedding_Avviso()
    {
        //SETUP
        await PopolaAsync();
        Dictionary<string, string?> configurazione = new() { ["EMBEDDING_DIMENSIONS"] = "4" };
        await Pipeline(new EmbeddingFinto(), configurazione).RunAsync(new EmbedRichiesta(false, Enum.GetValues<TabellaEmbedding>()), null, CancellationToken);
        var probe = new SqlHealthProbe(new SqlConnectionFactory(TestDatabase.ConnectionString), Opzioni(configurazione));

        //SUT
        ProbeResult completo = (await probe.RunAsync(CancellationToken)).Single(r => r.Numero == 9);
        await using (SqlConnection connection = await TestDatabase.OpenAsync())
        {
            // Due righe senza il vettore dello storico, una senza quello antifrode.
            await connection.ExecuteAsync(
                """
                UPDATE dbo.Sinistro SET Embedding = NULL WHERE Id IN (SELECT TOP (2) Id FROM dbo.Sinistro ORDER BY Id);
                UPDATE dbo.Sinistro SET EmbeddingAntifrode = NULL WHERE Id = (SELECT MAX(Id) FROM dbo.Sinistro);
                """);
        }
        ProbeResult incompleto = (await probe.RunAsync(CancellationToken)).Single(r => r.Numero == 9);

        Assert.That(completo.Stato, Is.EqualTo(ProbeStatus.Ok), completo.Dettaglio);
        Assert.That(incompleto.Stato, Is.EqualTo(ProbeStatus.Warning));
        Assert.That(incompleto.Dettaglio, Does.Contain("3 righe senza embedding").And.Contain("--solo-mancanti"));
    }

    private static async Task PopolaAsync()
    {
        await DatabaseInitializer.RecreateAsync(TestDatabase.ConnectionString, TestDatabase.Dimensions, CancellationToken);
        await using SqlConnection connection = await TestDatabase.OpenAsync();
        await SeedRepository.SeedClausoleAsync(connection, CancellationToken);
        await SeedRepository.InsertAsync(connection, SyntheticDataGenerator.Genera(SyntheticDataGenerator.DefaultRandomSeed, DateOnly.FromDateTime(DateTime.Today)), CancellationToken);
    }

    private EmbeddingPipeline Pipeline(IEmbeddingService servizio, Dictionary<string, string?> configurazione) =>
        new(servizio, _repository, Opzioni(configurazione), Microsoft.Extensions.Options.Options.Create(new RetrievalOptions()), TimeProvider.System);

    private static SinistriOptions Opzioni(Dictionary<string, string?> configurazione) =>
        SinistriOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(configurazione).Build());

    /// <summary>Vettori a 4 dimensioni derivati dalla lunghezza del testo: basta che siano validi e diversi tra loro.</summary>
    private sealed class EmbeddingFinto : IEmbeddingService
    {
        public List<string> Testi { get; } = [];

        public EmbeddingProfile Profile { get; } = EmbeddingProfile.ForModel("embeddinggemma");

        public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken) => Task.FromResult(Vettore(text));

        public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
        {
            Testi.AddRange(texts);

            return Task.FromResult<IReadOnlyList<float[]>>([.. texts.Select(Vettore)]);
        }

        private static float[] Vettore(string testo) => [1f, testo.Length % 7, testo.Length % 11, 0.5f];
    }
}
