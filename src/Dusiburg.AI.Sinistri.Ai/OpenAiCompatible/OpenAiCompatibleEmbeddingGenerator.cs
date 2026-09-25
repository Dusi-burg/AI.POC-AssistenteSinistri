using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.Ai.OpenAiCompatible;

/// <summary>
/// Embedding via <c>POST {endpoint}/embeddings</c> con il formato OpenAI, per i runtime NPU con server proprio
/// (FastFlowLM, Lemonade). Client minimo scritto a mano: si chiedono vettori <c>float</c> in chiaro, che tutti questi
/// server supportano, e non il formato base64 che il client OpenAI usa di default.
/// </summary>
internal sealed class OpenAiCompatibleEmbeddingGenerator(HttpClient http, string model, int dimensions)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly EmbeddingGeneratorMetadata _metadata = new("openai-compatible", http.BaseAddress, model, dimensions);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = new EmbeddingsRequest(options?.ModelId ?? model, [.. values]);

        using HttpResponseMessage response = await http.PostAsJsonAsync("embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        EmbeddingsResponse body = await response.Content.ReadFromJsonAsync<EmbeddingsResponse>(cancellationToken)
            ?? throw new InvalidOperationException($"Risposta vuota da {http.BaseAddress}embeddings.");

        if (body.Data.Count != request.Input.Count)
        {
            throw new InvalidOperationException($"Il server ha restituito {body.Data.Count} vettori per {request.Input.Count} testi.");
        }

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        return [.. body.Data.OrderBy(d => d.Index).Select(d => new Embedding<float>(d.Embedding) { ModelId = body.Model ?? request.Model, CreatedAt = createdAt })];
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is not null
        ? null
        : serviceType == typeof(EmbeddingGeneratorMetadata) ? _metadata
        : serviceType.IsInstanceOfType(this) ? this
        : null;

    public void Dispose() => http.Dispose();

    private sealed record EmbeddingsRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input)
    {
        [JsonPropertyName("encoding_format")]
        public string EncodingFormat => "float";
    }

    private sealed record EmbeddingsResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("data")] IReadOnlyList<EmbeddingData> Data);

    private sealed record EmbeddingData(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
