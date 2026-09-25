using System.Net;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Ai;

/// <summary>
/// <see cref="IEmbeddingService"/> sul generatore creato da <see cref="EmbeddingGeneratorFactory"/> (fase-4.md §1): non sa su
/// quale chip gira. Applica il prefisso del profilo, spezza in batch, riprova sugli errori transitori, riduce i vettori con
/// dimensione nativa maggiore (troncamento MRL con rinormalizzazione) e controlla la dimensione di ogni vettore.
/// </summary>
public sealed class EmbeddingService : IEmbeddingService
{
    /// <summary>Attese tra i tentativi: 3 tentativi in tutto dopo il primo errore, ciclo esplicito senza librerie di resilienza.</summary>
    internal static readonly TimeSpan[] AtteseRetry = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(9)];

    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly SinistriOptions _options;
    private readonly int _batchSize;
    private readonly Func<TimeSpan, CancellationToken, Task> _attesa;

    public EmbeddingService(IEmbeddingGenerator<string, Embedding<float>> generator, SinistriOptions options, IOptions<RetrievalOptions> retrieval)
        : this(generator, options, retrieval.Value.EmbeddingBatchSize, Task.Delay)
    {
    }

    internal EmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> generator, SinistriOptions options, int batchSize, Func<TimeSpan, CancellationToken, Task> attesa)
    {
        _generator = generator;
        _options = options;
        _batchSize = batchSize;
        _attesa = attesa;
        Profile = EmbeddingProfile.ForModel(options.EmbeddingModel);

        if (options.EmbeddingDimensions > Profile.DimensioneNativa)
        {
            throw new InvalidOperationException(
                $"{SinistriOptions.Keys.EmbeddingDimensions}={options.EmbeddingDimensions} supera la dimensione nativa di {Profile.Modello} ({Profile.DimensioneNativa}).");
        }
    }

    public EmbeddingProfile Profile { get; }

    public async Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken) =>
        (await GenerateAsync([Profile.Query(text)], cancellationToken))[0];

    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        List<float[]> vettori = new(texts.Count);

        foreach (string[] batch in texts.Select(Profile.Document).Chunk(_batchSize))
        {
            vettori.AddRange(await GenerateAsync(batch, cancellationToken));
        }

        return vettori;
    }

    private async Task<IReadOnlyList<float[]>> GenerateAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        GeneratedEmbeddings<Embedding<float>> embeddings = await WithRetryAsync(
            () => _generator.GenerateAsync(texts, cancellationToken: cancellationToken), cancellationToken);

        if (embeddings.Count != texts.Count)
        {
            throw new InvalidOperationException($"{Descrizione()}: {embeddings.Count} vettori per {texts.Count} testi.");
        }

        return [.. embeddings.Select(e => Riduci(e.Vector.ToArray()))];
    }

    /// <summary>Troncamento MRL alla dimensione usata e rinormalizzazione; poi il controllo di dimensione.</summary>
    private float[] Riduci(float[] vettore)
    {
        if (vettore.Length > _options.EmbeddingDimensions && vettore.Length == Profile.DimensioneNativa)
        {
            vettore = vettore[.._options.EmbeddingDimensions];
            float norma = MathF.Sqrt(vettore.Sum(v => v * v));

            if (norma > 0)
            {
                vettore = [.. vettore.Select(v => v / norma)];
            }
        }

        return vettore.Length == _options.EmbeddingDimensions
            ? vettore
            : throw new InvalidOperationException(
                $"{Descrizione()}: vettore di {vettore.Length} elementi, attesi {_options.EmbeddingDimensions} ({SinistriOptions.Keys.EmbeddingDimensions}).");
    }

    private async Task<T> WithRetryAsync<T>(Func<Task<T>> operazione, CancellationToken cancellationToken)
    {
        for (int tentativo = 0; ; tentativo++)
        {
            try
            {
                return await operazione();
            }
            catch (Exception exception) when (tentativo < AtteseRetry.Length && Transitorio(exception, cancellationToken))
            {
                await _attesa(AtteseRetry[tentativo], cancellationToken);
            }
        }
    }

    /// <summary>Errori di rete, timeout e risposte 5xx; mai i 4xx (es. modello inesistente) né l'annullamento chiesto dal chiamante.</summary>
    internal static bool Transitorio(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        HttpRequestException { StatusCode: { } status } => (int)status >= 500 || status == HttpStatusCode.RequestTimeout,
        HttpRequestException => true,
        TimeoutException => true,
        TaskCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false
    };

    private string Descrizione() => $"embedding {_options.EmbeddingModel} ({_options.EmbeddingProvider})";
}
