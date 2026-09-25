using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Candidates;

/// <summary>
/// Una richiesta per testo. FastFlowLM 1.0.6 restituisce vettori diversi per lo stesso testo se arriva in un batch
/// (coseno ~0,01 contro la richiesta singola): con questo adattatore si misura il modello senza quel difetto.
/// </summary>
internal sealed class OneByOneEmbeddingGenerator(IEmbeddingGenerator<string, Embedding<float>> inner)
    : DelegatingEmbeddingGenerator<string, Embedding<float>>(inner)
{
    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        GeneratedEmbeddings<Embedding<float>> result = [];

        foreach (string value in values)
        {
            GeneratedEmbeddings<Embedding<float>> single = await base.GenerateAsync([value], options, cancellationToken);
            result.Add(single[0]);
        }

        return result;
    }
}
