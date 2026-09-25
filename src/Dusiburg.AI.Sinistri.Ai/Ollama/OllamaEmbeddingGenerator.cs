using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;

namespace Dusiburg.AI.Sinistri.Ai.Ollama;

/// <summary>
/// Embedding via <c>/api/embed</c> con <c>num_gpu</c> esplicito (D18): con 0 il modello resta sulla CPU e non toglie VRAM alla chat.
/// Si usa <see cref="IOllamaApiClient.EmbedAsync"/> direttamente perché l'adapter di OllamaSharp per
/// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> non documenta come passare le opzioni del runtime.
/// </summary>
internal sealed class OllamaEmbeddingGenerator(IOllamaApiClient client, string model, int? numGpu, int dimensions)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly EmbeddingGeneratorMetadata _metadata = new("ollama", client.Uri, model, dimensions);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = new EmbedRequest
        {
            Model = options?.ModelId ?? model,
            Input = [.. values],
            Options = numGpu is null ? null : new RequestOptions { NumGpu = numGpu }
        };

        EmbedResponse response = await client.EmbedAsync(request, cancellationToken);
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        return [.. response.Embeddings.Select(vector => new Embedding<float>(vector) { ModelId = request.Model, CreatedAt = createdAt })];
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is not null
        ? null
        : serviceType == typeof(EmbeddingGeneratorMetadata) ? _metadata
        : serviceType.IsInstanceOfType(this) ? this
        : serviceType.IsInstanceOfType(client) ? client
        : null;

    public void Dispose() => (client as IDisposable)?.Dispose();
}
