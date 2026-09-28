using System.Diagnostics;
using Dusiburg.AI.Sinistri.EmbeddingBench.Candidates;
using Dusiburg.AI.Sinistri.EmbeddingBench.Dataset;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Measurement;

/// <summary>
/// Convivenza con la chat (fase-1b.md §4): embedding a ciclo continuo mentre <c>qwen3.5:9b</c> genera sulla GPU.
/// Si misura la latenza dell'embedding sotto carico, la velocità della chat e se il modello di chat è stato ricaricato.
/// </summary>
internal sealed class CoexistenceProbe(BenchDataset dataset, OllamaAdmin ollama, TextWriter log)
{
    /// <summary>Oltre questo tempo di caricamento la chat è stata scaricata dalla VRAM e ricaricata.</summary>
    private const double ReloadThresholdSeconds = 1.0;

    public async Task<double> ChatBaselineAsync(CancellationToken cancellationToken)
    {
        await ollama.GenerateAsync(cancellationToken);
        (double tokensPerSecond, _) = await ollama.GenerateAsync(cancellationToken);
        log.WriteLine($"chat da sola: {tokensPerSecond:0.0} token/s");

        return tokensPerSecond;
    }

    public async Task<Coexistence> RunAsync(BenchCandidate candidate, double chatBaseline, CancellationToken cancellationToken)
    {
        // Ollama non ricarica un modello già in memoria se cambia solo num_gpu: si riparte da zero, come nella misura principale.
        if (candidate.OllamaModel is { } model)
        {
            await OllamaAdmin.StopAsync(model, cancellationToken);
        }

        using IEmbeddingGenerator<string, Embedding<float>> generator = candidate.CreateGenerator();
        await generator.GenerateVectorAsync(candidate.Profilo.Query("riscaldamento"), cancellationToken: cancellationToken);

        Task<(double TokensPerSecond, double LoadSeconds)> chat = ollama.GenerateAsync(cancellationToken);
        List<double> latencies = [];

        while (!chat.IsCompleted)
        {
            string query = candidate.Profilo.Query(dataset.Casi[latencies.Count % dataset.Casi.Count].Query);
            long start = Stopwatch.GetTimestamp();
            await generator.GenerateVectorAsync(query, cancellationToken: cancellationToken);
            latencies.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        (double tokensPerSecond, double loadSeconds) = await chat;
        string placement = await ollama.PlacementAsync(cancellationToken);

        var result = new Coexistence(
            latencies.Count > 0 ? VectorMath.Percentile(latencies, 50) : double.NaN,
            chatBaseline,
            tokensPerSecond,
            loadSeconds > ReloadThresholdSeconds,
            placement);

        log.WriteLine($"  convivenza: embedding p50 {result.EmbeddingP50SottoCaricoMs:0} ms su {latencies.Count} richieste, chat {tokensPerSecond:0.0} token/s, ricaricata: {result.ChatRicaricata}");

        return result;
    }
}

internal static class CorrectnessCheck
{
    /// <summary>Similarità coseno tra i vettori degli stessi testi calcolati da due percorsi con lo stesso modello.</summary>
    public static Correctness Compare(BenchResult candidate, BenchResult reference)
    {
        double[] similarities =
        [
            .. candidate.Vettori
                .Where(v => reference.Vettori.ContainsKey(v.Key))
                .Select(v => VectorMath.CosineSimilarity(v.Value, reference.Vettori[v.Key]))
        ];

        return new Correctness(reference.Candidate.Id, similarities.Average(), similarities.Min(), similarities.Length);
    }
}
