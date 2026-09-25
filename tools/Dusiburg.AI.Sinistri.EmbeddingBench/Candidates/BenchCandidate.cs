using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.EmbeddingBench.Onnx;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Candidates;

/// <summary>Combinazione modello × percorso misurata dal banco (fase-1b.md §2).</summary>
/// <param name="Id">Identificativo da riga di comando, es. <c>p1-bge-m3</c>.</param>
/// <param name="Percorso">Id del percorso in fase-1b.md: P0, P1, P2, P3a.</param>
/// <param name="Riferimento">Id del candidato con lo stesso modello su CPU, per la correttezza dei vettori NPU.</param>
internal sealed record BenchCandidate(
    string Id,
    string Percorso,
    string Chip,
    string Runtime,
    string Modello,
    int Dimensioni,
    EmbeddingProfile Profilo,
    Func<IEmbeddingGenerator<string, Embedding<float>>> CreateGenerator,
    string? Riferimento = null,
    string? OllamaModel = null);

internal static class BenchCandidates
{
    public const string OllamaEndpoint = "http://127.0.0.1:11434";
    public const string FastFlowLmEndpoint = "http://127.0.0.1:52625/v1";

    private static string BgeM3OnnxDirectory => Path.Combine(WindowsMlRuntime.AppDataRoot, "models", "bge-m3-onnx");

    public static IReadOnlyList<BenchCandidate> All() =>
    [
        Ollama("p1-bge-m3", "P1", "CPU", "bge-m3", 1024, numGpu: "0"),
        Ollama("p1-embeddinggemma", "P1", "CPU", "embeddinggemma", 768, numGpu: "0"),
        Ollama("p0-bge-m3-gpu", "P0", "GPU", "bge-m3", 1024, numGpu: "auto"),
        new BenchCandidate(
            "p2-embed-gemma-npu", "P2", "NPU", "FastFlowLM (batch 16)", "embed-gemma:300m", 768, EmbeddingProfile.EmbeddingGemma,
            FastFlowLm,
            Riferimento: "p1-embeddinggemma"),
        new BenchCandidate(
            "p2-embed-gemma-npu-single", "P2", "NPU", "FastFlowLM (1 testo per richiesta)", "embed-gemma:300m", 768, EmbeddingProfile.EmbeddingGemma,
            () => new OneByOneEmbeddingGenerator(FastFlowLm()),
            Riferimento: "p1-embeddinggemma"),
        new BenchCandidate(
            "p3a-bge-m3-npu", "P3a", "NPU", "Windows ML + VitisAI", "amd/bge-m3-onnx", 1024, EmbeddingProfile.None,
            () => new BgeM3OnnxEmbeddingGenerator(BgeM3OnnxDirectory, OnnxDevice.Npu),
            Riferimento: "p1-bge-m3"),
        new BenchCandidate(
            "p3a-bge-m3-cpu", "P3a", "CPU", "Windows ML (CPU)", "amd/bge-m3-onnx", 1024, EmbeddingProfile.None,
            () => new BgeM3OnnxEmbeddingGenerator(BgeM3OnnxDirectory, OnnxDevice.Cpu),
            Riferimento: "p1-bge-m3")
    ];

    private static BenchCandidate Ollama(string id, string percorso, string chip, string model, int dimensions, string numGpu) =>
        new(id, percorso, chip, $"Ollama (num_gpu={numGpu})", model, dimensions, EmbeddingProfile.ForModel(model),
            () => Factory(new()
            {
                [SinistriOptions.Keys.OllamaEndpoint] = OllamaEndpoint,
                [SinistriOptions.Keys.EmbeddingModel] = model,
                [SinistriOptions.Keys.EmbeddingDimensions] = dimensions.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [SinistriOptions.Keys.EmbeddingNumGpu] = numGpu
            }),
            OllamaModel: model);

    private static IEmbeddingGenerator<string, Embedding<float>> FastFlowLm() => Factory(new()
    {
        [SinistriOptions.Keys.EmbeddingProvider] = EmbeddingProviders.OpenAiCompatible,
        [SinistriOptions.Keys.EmbeddingEndpoint] = FastFlowLmEndpoint,
        [SinistriOptions.Keys.EmbeddingModel] = "embed-gemma:300m",
        [SinistriOptions.Keys.EmbeddingDimensions] = "768"
    });

    /// <summary>Stessa factory dell'applicazione: il banco misura il codice che userà l'API.</summary>
    private static IEmbeddingGenerator<string, Embedding<float>> Factory(Dictionary<string, string?> settings)
    {
        SinistriOptions options = SinistriOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        return new EmbeddingGeneratorFactory(options, NullLoggerFactory.Instance).Create();
    }
}
