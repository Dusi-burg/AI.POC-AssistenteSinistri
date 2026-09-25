using Dusiburg.AI.Sinistri.Ai.Ollama;
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

            // FastFlowLM/Lemonade e Windows ML arrivano con il banco di prova (fase-1b.md).
            _ => throw new NotSupportedException(
                $"{SinistriOptions.Keys.EmbeddingProvider}={options.EmbeddingProvider} non è ancora implementato: arriva con la Fase 1b.")
        };

        return inner
            .AsBuilder()
            .UseLogging(loggerFactory)
            .UseOpenTelemetry(loggerFactory, SinistriTelemetry.Sources.Embedding)
            .Build();
    }
}
