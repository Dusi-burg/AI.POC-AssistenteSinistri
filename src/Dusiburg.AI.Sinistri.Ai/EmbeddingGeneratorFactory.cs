using Dusiburg.AI.Sinistri.Ai.Ollama;
using Dusiburg.AI.Sinistri.Ai.OpenAiCompatible;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Dusiburg.AI.Sinistri.Ai;

/// <summary>
/// Unico punto che conosce i runtime dell'embedding (<c>EMBEDDING_PROVIDER</c>): il resto del codice vede solo
/// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.
/// </summary>
public sealed class EmbeddingGeneratorFactory(SinistriOptions options, ILoggerFactory loggerFactory)
{
    public IEmbeddingGenerator<string, Embedding<float>> Create()
    {
        IEmbeddingGenerator<string, Embedding<float>> inner = options.EmbeddingProvider switch
        {
            EmbeddingProviders.Ollama => new OllamaEmbeddingGenerator(
                OllamaClients.Create(options.EmbeddingEndpoint, OllamaClients.GenerationTimeout),
                options.EmbeddingModel,
                options.EmbeddingNumGpu,
                options.EmbeddingDimensions),

            // FastFlowLM o Lemonade sulla NPU (fase-1b.md, P2).
            EmbeddingProviders.OpenAiCompatible => new OpenAiCompatibleEmbeddingGenerator(
                new HttpClient { BaseAddress = WithTrailingSlash(options.EmbeddingEndpoint), Timeout = OllamaClients.GenerationTimeout },
                options.EmbeddingModel,
                options.EmbeddingDimensions),

            // Windows ML / ONNX Runtime nel processo: per ora solo nel banco di prova (tools/Dusiburg.AI.Sinistri.EmbeddingBench).
            _ => throw new NotSupportedException(
                $"{SinistriOptions.Keys.EmbeddingProvider}={options.EmbeddingProvider} non è ancora disponibile nell'applicazione: " +
                "si integra dopo il CHECKPOINT 1b, se il banco di prova lo sceglie.")
        };

        return inner
            .AsBuilder()
            .UseLogging(loggerFactory)
            .UseOpenTelemetry(loggerFactory, SinistriTelemetry.Sources.Embedding)
            .Build();
    }

    /// <summary>Senza la barra finale <c>new Uri(base, "embeddings")</c> sostituirebbe l'ultimo segmento (<c>/v1</c>).</summary>
    private static Uri WithTrailingSlash(Uri endpoint) =>
        endpoint.AbsoluteUri.EndsWith('/') ? endpoint : new Uri(endpoint.AbsoluteUri + "/");
}
