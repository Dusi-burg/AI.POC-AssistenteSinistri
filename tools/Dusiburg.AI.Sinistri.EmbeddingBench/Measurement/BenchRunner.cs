using System.Diagnostics;
using Dusiburg.AI.Sinistri.EmbeddingBench.Candidates;
using Dusiburg.AI.Sinistri.EmbeddingBench.Dataset;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Measurement;

/// <summary>Misure di un candidato (fase-1b.md §4): qualità, duplicati, latenza, throughput, determinismo, memoria.</summary>
internal sealed class BenchRunner(BenchDataset dataset, OllamaAdmin ollama, int latencyRepetitions, TextWriter log)
{
    private const int BatchSize = 16;

    public async Task<BenchResult> RunAsync(BenchCandidate candidate, CancellationToken cancellationToken)
    {
        try
        {
            return await MeasureAsync(candidate, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log.WriteLine($"  ERRORE: {exception.Message}");

            return new BenchResult(candidate) { Errore = exception.Message };
        }
    }

    private async Task<BenchResult> MeasureAsync(BenchCandidate candidate, CancellationToken cancellationToken)
    {
        if (candidate.OllamaModel is { } model)
        {
            await OllamaAdmin.StopAsync(model, cancellationToken);
        }

        long workingSetBefore = Process.GetCurrentProcess().WorkingSet64;
        long start = Stopwatch.GetTimestamp();

        using IEmbeddingGenerator<string, Embedding<float>> generator = candidate.CreateGenerator();
        await generator.GenerateVectorAsync(candidate.Profilo.Query(dataset.Casi[0].Query), cancellationToken: cancellationToken);
        double coldMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        log.WriteLine($"  prima richiesta: {coldMs:0} ms");

        (Dictionary<string, float[]> vectors, double textsPerSecond) = await EmbedAllAsync(generator, candidate, cancellationToken);
        log.WriteLine($"  throughput: {textsPerSecond:0.0} testi/s");

        CheckDimensions(candidate, vectors.Values.First());

        float[][] queries = [.. (await generator.GenerateAsync(dataset.Casi.Select(c => candidate.Profilo.Query(c.Query)), cancellationToken: cancellationToken)).Select(e => e.Vector.ToArray())];
        Dictionary<string, float[]> documents = dataset.Documenti.ToDictionary(d => d.Id, d => vectors[d.Testo]);
        RetrievalQuality quality = QualityMetrics.Retrieval(dataset, documents, queries);
        log.WriteLine($"  qualità: hit@1 {quality.HitAt1:0.00}, MRR {quality.Mrr:0.000}, margine medio {quality.MargineMedio:0.000}");

        DuplicateSeparation duplicates = await DuplicatesAsync(generator, candidate.Profilo, cancellationToken);
        (double p50, double p95) = await LatencyAsync(generator, candidate.Profilo, cancellationToken);
        log.WriteLine($"  latenza: p50 {p50:0} ms, p95 {p95:0} ms");

        double determinism = await DeterminismAsync(generator, candidate.Profilo, cancellationToken);
        double? processMb = candidate.Runtime.StartsWith("Windows ML", StringComparison.Ordinal)
            ? (Process.GetCurrentProcess().WorkingSet64 - workingSetBefore) / 1_048_576.0
            : null;
        string? placement = candidate.OllamaModel is null ? null : await ollama.PlacementAsync(cancellationToken);

        return new BenchResult(candidate)
        {
            Qualita = quality,
            Duplicati = duplicates,
            Prestazioni = new Performance(coldMs, p50, p95, textsPerSecond, determinism, processMb, placement),
            Vettori = vectors,
            Note = generator.GetService<Onnx.BgeM3OnnxEmbeddingGenerator>() is { TruncatedTexts: > 0 } onnx
                ? $"{onnx.TruncatedTexts} testi troncati a 512 token"
                : null
        };
    }

    /// <summary>Tutti i testi con il prefisso "documento", a batch di 16: vettori per il confronto tra percorsi e throughput.</summary>
    private async Task<(Dictionary<string, float[]> Vectors, double TextsPerSecond)> EmbedAllAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator, BenchCandidate candidate, CancellationToken cancellationToken)
    {
        string[] texts = [.. dataset.AllTexts()];
        Dictionary<string, float[]> vectors = [];
        long start = Stopwatch.GetTimestamp();

        foreach (string[] batch in texts.Chunk(BatchSize))
        {
            GeneratedEmbeddings<Embedding<float>> embeddings =
                await generator.GenerateAsync(batch.Select(candidate.Profilo.Document), cancellationToken: cancellationToken);

            for (int i = 0; i < batch.Length; i++)
            {
                vectors[batch[i]] = embeddings[i].Vector.ToArray();
            }
        }

        return (vectors, texts.Length / Stopwatch.GetElapsedTime(start).TotalSeconds);
    }

    private async Task<DuplicateSeparation> DuplicatesAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator, EmbeddingProfile profile, CancellationToken cancellationToken)
    {
        async Task<List<double>> DistancesAsync(IReadOnlyList<BenchPair> pairs)
        {
            GeneratedEmbeddings<Embedding<float>> a = await generator.GenerateAsync(pairs.Select(p => profile.Similarity(p.A)), cancellationToken: cancellationToken);
            GeneratedEmbeddings<Embedding<float>> b = await generator.GenerateAsync(pairs.Select(p => profile.Similarity(p.B)), cancellationToken: cancellationToken);

            return [.. a.Zip(b, (x, y) => VectorMath.CosineDistance(x.Vector.Span, y.Vector.Span))];
        }

        return QualityMetrics.Duplicates(await DistancesAsync(dataset.CoppieDuplicate), await DistancesAsync(dataset.CoppieStessoTema));
    }

    /// <summary>Latenza di una singola query a caldo, come nella pre-istruttoria: una richiesta per volta.</summary>
    private async Task<(double P50, double P95)> LatencyAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator, EmbeddingProfile profile, CancellationToken cancellationToken)
    {
        List<double> latencies = [];

        for (int i = 0; i < latencyRepetitions; i++)
        {
            string query = profile.Query(dataset.Casi[i % dataset.Casi.Count].Query);
            long start = Stopwatch.GetTimestamp();
            await generator.GenerateVectorAsync(query, cancellationToken: cancellationToken);
            latencies.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        return (VectorMath.Percentile(latencies, 50), VectorMath.Percentile(latencies, 95));
    }

    private async Task<double> DeterminismAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator, EmbeddingProfile profile, CancellationToken cancellationToken)
    {
        double worst = 0;

        foreach (string text in dataset.Documenti.Take(5).Select(d => profile.Document(d.Testo)))
        {
            ReadOnlyMemory<float> first = await generator.GenerateVectorAsync(text, cancellationToken: cancellationToken);
            ReadOnlyMemory<float> second = await generator.GenerateVectorAsync(text, cancellationToken: cancellationToken);
            worst = Math.Max(worst, VectorMath.CosineDistance(first.Span, second.Span));
        }

        return worst;
    }

    private static void CheckDimensions(BenchCandidate candidate, float[] vector)
    {
        if (vector.Length != candidate.Dimensioni)
        {
            throw new InvalidOperationException($"{candidate.Id}: vettori da {vector.Length}, attesi {candidate.Dimensioni}.");
        }
    }
}
