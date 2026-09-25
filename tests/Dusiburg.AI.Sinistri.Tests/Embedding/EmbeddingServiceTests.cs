using System.Net;
using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Core.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace Dusiburg.AI.Sinistri.Tests.Embedding;

/// <summary>EmbeddingService con un generatore finto: prefissi, batch, retry, riduzione MRL e controllo di dimensione.</summary>
public class EmbeddingServiceTests
{
    [Test]
    public void EmbedBatch_DimensioneErrata_Throws()
    {
        //SETUP
        var generatore = new GeneratoreFinto(_ => new float[5]);
        EmbeddingService service = Service(generatore, dimensioni: 4);

        //SUT
        Assert.That(() => service.EmbedDocumentsAsync(["testo"], CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("5 elementi").And.Message.Contains("attesi 4").And.Message.Contains("embeddinggemma"));
    }

    [Test]
    public async Task EmbedBatch_ErroreTransitorio_Riprova()
    {
        //SETUP
        var generatore = new GeneratoreFinto(_ => [1f, 0f, 0f, 0f])
        {
            Errori = new Queue<Exception>([new HttpRequestException("rete"), new HttpRequestException("500", null, HttpStatusCode.InternalServerError)])
        };
        List<TimeSpan> attese = [];
        EmbeddingService service = Service(generatore, dimensioni: 4, attese: attese);

        //SUT
        IReadOnlyList<float[]> vettori = await service.EmbedDocumentsAsync(["testo"], CancellationToken.None);

        Assert.That(vettori.Single(), Is.EqualTo(new[] { 1f, 0f, 0f, 0f }));
        Assert.That(generatore.Chiamate, Has.Count.EqualTo(3));
        Assert.That(attese, Is.EqualTo(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3) }));
    }

    [Test]
    public void EmbedBatch_Errore4xxOErroriFiniti_NonRiprovaOltre()
    {
        //SETUP
        var modelloInesistente = new GeneratoreFinto(_ => new float[4])
        {
            Errori = new Queue<Exception>([new HttpRequestException("404", null, HttpStatusCode.NotFound)])
        };
        var sempreGiu = new GeneratoreFinto(_ => new float[4])
        {
            Errori = new Queue<Exception>(Enumerable.Range(0, 10).Select(_ => new HttpRequestException("giù")))
        };

        //SUT
        Assert.That(() => Service(modelloInesistente, 4).EmbedDocumentsAsync(["testo"], CancellationToken.None), Throws.TypeOf<HttpRequestException>());
        Assert.That(() => Service(sempreGiu, 4).EmbedDocumentsAsync(["testo"], CancellationToken.None), Throws.TypeOf<HttpRequestException>());

        Assert.That(modelloInesistente.Chiamate, Has.Count.EqualTo(1));
        Assert.That(sempreGiu.Chiamate, Has.Count.EqualTo(1 + EmbeddingService.AtteseRetry.Length));
    }

    [Test]
    public async Task EmbedQueryEDocumenti_PrefissiEBatch()
    {
        //SETUP
        var generatore = new GeneratoreFinto(_ => [0f, 1f, 0f, 0f]);
        EmbeddingService service = Service(generatore, dimensioni: 4, batch: 16);
        string[] testi = [.. Enumerable.Range(1, 40).Select(i => $"clausola {i}")];

        //SUT
        await service.EmbedQueryAsync("denuncia", CancellationToken.None);
        IReadOnlyList<float[]> documenti = await service.EmbedDocumentsAsync(testi, CancellationToken.None);

        Assert.That(generatore.Chiamate[0], Is.EqualTo(new[] { "task: search result | query: denuncia" }));
        Assert.That(generatore.Chiamate.Skip(1).Select(c => c.Length), Is.EqualTo(new[] { 16, 16, 8 }));
        Assert.That(generatore.Chiamate.Skip(1).SelectMany(c => c), Is.All.StartsWith("title: none | text: clausola "));
        Assert.That(documenti, Has.Count.EqualTo(40));
    }

    [Test]
    public async Task VettoreNativoPiuLungo_TroncatoERinormalizzato()
    {
        //SETUP
        var generatore = new GeneratoreFinto(_ => [.. new[] { 3f, 4f, 0f, 0f }, .. Enumerable.Repeat(5f, 764)]);
        EmbeddingService service = Service(generatore, dimensioni: 4);

        //SUT
        float[] vettore = await service.EmbedQueryAsync("denuncia", CancellationToken.None);

        Assert.That(vettore, Is.EqualTo(new[] { 0.6f, 0.8f, 0f, 0f }).Within(1e-6f));
    }

    private static EmbeddingService Service(GeneratoreFinto generatore, int dimensioni, int batch = 16, List<TimeSpan>? attese = null)
    {
        SinistriOptions options = SinistriOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EMBEDDING_DIMENSIONS"] = $"{dimensioni}" })
            .Build());

        return new EmbeddingService(generatore, options, batch, (attesa, _) =>
        {
            attese?.Add(attesa);

            return Task.CompletedTask;
        });
    }

    /// <summary>Generatore finto: registra i testi di ogni chiamata e lancia prima gli errori in coda.</summary>
    private sealed class GeneratoreFinto(Func<string, float[]> vettore) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Queue<Exception> Errori { get; init; } = new();

        public List<string[]> Chiamate { get; } = [];

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            string[] testi = [.. values];
            Chiamate.Add(testi);

            return Errori.TryDequeue(out Exception? errore)
                ? Task.FromException<GeneratedEmbeddings<Embedding<float>>>(errore)
                : Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(testi.Select(t => new Embedding<float>(vettore(t)))));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
