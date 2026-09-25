using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Dusiburg.AI.Sinistri.Core.Options;

/// <summary>Runtime che calcola gli embedding (D18): la scelta definitiva arriva dal CHECKPOINT 1b.</summary>
public static class EmbeddingProviders
{
    public const string Ollama = "ollama";

    /// <summary>Server con API OpenAI (<c>/v1/embeddings</c>): FastFlowLM, Lemonade.</summary>
    public const string OpenAiCompatible = "openai-compatible";

    /// <summary>Windows ML / ONNX Runtime dentro il processo .NET.</summary>
    public const string Onnx = "onnx";

    public static readonly IReadOnlyList<string> All = [Ollama, OpenAiCompatible, Onnx];
}

/// <summary>
/// Manopole della demo (D12), come <c>ModelOptions</c> di O2C: si impostano sull'AppHost (user-secrets, riga di comando,
/// variabili d'ambiente) e arrivano ai servizi; senza valore vale il default del codice. Validate a ogni lettura.
/// </summary>
public sealed record SinistriOptions(
    Uri OllamaEndpoint,
    string OllamaChatModel,
    int OllamaContextLength,
    string EmbeddingProvider,
    Uri EmbeddingEndpoint,
    string EmbeddingModel,
    int? EmbeddingNumGpu,
    string? EmbeddingOnnxPath,
    int EmbeddingDimensions,
    double SogliaDuplicatoCosine,
    string? PromptCaptureDirectory,
    bool Warmup)
{
    /// <summary>127.0.0.1 e non localhost: su Windows ogni nuova connessione a localhost prova prima IPv6 e perde ~2 s (Fase 0).</summary>
    public const string DefaultOllamaEndpoint = "http://127.0.0.1:11434";

    public const string DefaultChatModel = "qwen3.5:9b";

    /// <summary>Con 8 GB di VRAM Ollama sceglierebbe 4096 token, pochi per il prompt della scheda (~3–4k token) più la risposta.</summary>
    public const int DefaultContextLength = 8_192;

    /// <summary>Default provvisorio fino al CHECKPOINT 1b.</summary>
    public const string DefaultEmbeddingModel = "bge-m3";

    public const int DefaultEmbeddingDimensions = 1024;

    /// <summary>D18: l'embedding non va mai in VRAM, per non contendere la GPU alla chat.</summary>
    public const int DefaultEmbeddingNumGpu = 0;

    /// <summary>Limite del tipo <c>VECTOR</c> di SQL Server 2025 in <c>float32</c>.</summary>
    public const int MaxEmbeddingDimensions = 1998;

    public const double DefaultSogliaDuplicatoCosine = 0.08;

    /// <summary>Valore di <c>EMBEDDING_NUM_GPU</c> che lascia la scelta a Ollama.</summary>
    public const string AutomaticNumGpu = "auto";

    public static class Keys
    {
        public const string OllamaEndpoint = "OLLAMA_ENDPOINT";
        public const string OllamaChatModel = "OLLAMA_CHAT_MODEL";
        public const string OllamaNumCtx = "OLLAMA_NUM_CTX";
        public const string EmbeddingProvider = "EMBEDDING_PROVIDER";
        public const string EmbeddingEndpoint = "EMBEDDING_ENDPOINT";
        public const string EmbeddingModel = "EMBEDDING_MODEL";
        public const string EmbeddingNumGpu = "EMBEDDING_NUM_GPU";
        public const string EmbeddingOnnxPath = "EMBEDDING_ONNX_PATH";
        public const string EmbeddingDimensions = "EMBEDDING_DIMENSIONS";
        public const string SogliaDuplicatoCosine = "SOGLIA_DUPLICATO_COSINE";
        public const string PromptCaptureDirectory = "SINISTRI_PROMPT_CAPTURE_DIR";
        public const string Warmup = "SINISTRI_WARMUP";

        /// <summary>Tutte le manopole, nell'ordine in cui l'AppHost le inoltra all'API.</summary>
        public static readonly IReadOnlyList<string> All =
        [
            OllamaEndpoint, OllamaChatModel, OllamaNumCtx,
            EmbeddingProvider, EmbeddingEndpoint, EmbeddingModel, EmbeddingNumGpu, EmbeddingOnnxPath, EmbeddingDimensions,
            SogliaDuplicatoCosine, PromptCaptureDirectory, Warmup
        ];
    }

    public static SinistriOptions FromConfiguration(IConfiguration configuration)
    {
        Uri ollamaEndpoint = ReadUri(configuration, Keys.OllamaEndpoint) ?? new Uri(DefaultOllamaEndpoint);
        string provider = ReadProvider(configuration);
        Uri? embeddingEndpoint = ReadUri(configuration, Keys.EmbeddingEndpoint);
        string? onnxPath = NonEmpty(configuration[Keys.EmbeddingOnnxPath]);

        if (provider == EmbeddingProviders.OpenAiCompatible && embeddingEndpoint is null)
        {
            throw Invalid($"{Keys.EmbeddingEndpoint} è obbligatorio con {Keys.EmbeddingProvider}={provider} (es. http://127.0.0.1:52625/v1).");
        }

        if (provider == EmbeddingProviders.Onnx && onnxPath is null)
        {
            throw Invalid($"{Keys.EmbeddingOnnxPath} è obbligatorio con {Keys.EmbeddingProvider}={provider}: cartella con modello e tokenizer.");
        }

        return new SinistriOptions(
            ollamaEndpoint,
            NonEmpty(configuration[Keys.OllamaChatModel]) ?? DefaultChatModel,
            ReadInt(configuration, Keys.OllamaNumCtx, DefaultContextLength, minimum: 1_024, maximum: 262_144),
            provider,
            embeddingEndpoint ?? ollamaEndpoint,
            NonEmpty(configuration[Keys.EmbeddingModel]) ?? DefaultEmbeddingModel,
            ReadNumGpu(configuration),
            onnxPath,
            ReadInt(configuration, Keys.EmbeddingDimensions, DefaultEmbeddingDimensions, minimum: 1, maximum: MaxEmbeddingDimensions),
            ReadDouble(configuration, Keys.SogliaDuplicatoCosine, DefaultSogliaDuplicatoCosine, minimum: 0, maximum: 2),
            NonEmpty(configuration[Keys.PromptCaptureDirectory]),
            ReadBool(configuration, Keys.Warmup, defaultValue: true));
    }

    private static string ReadProvider(IConfiguration configuration)
    {
        string provider = (NonEmpty(configuration[Keys.EmbeddingProvider]) ?? EmbeddingProviders.Ollama).ToLowerInvariant();

        return EmbeddingProviders.All.Contains(provider)
            ? provider
            : throw Invalid($"{Keys.EmbeddingProvider} '{provider}' non supportato: usare {string.Join(", ", EmbeddingProviders.All)}.");
    }

    private static int? ReadNumGpu(IConfiguration configuration)
    {
        string? value = NonEmpty(configuration[Keys.EmbeddingNumGpu]);

        if (value is null)
        {
            return DefaultEmbeddingNumGpu;
        }

        return value.Equals(AutomaticNumGpu, StringComparison.OrdinalIgnoreCase)
            ? null
            : ReadInt(configuration, Keys.EmbeddingNumGpu, DefaultEmbeddingNumGpu, minimum: 0, maximum: 999);
    }

    private static Uri? ReadUri(IConfiguration configuration, string key)
    {
        string? value = NonEmpty(configuration[key]);

        if (value is null)
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https"
            ? uri
            : throw Invalid($"{key} '{value}' non è un indirizzo http(s) valido.");
    }

    private static int ReadInt(IConfiguration configuration, string key, int defaultValue, int minimum, int maximum)
    {
        string? value = NonEmpty(configuration[key]);

        if (value is null)
        {
            return defaultValue;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) && number >= minimum && number <= maximum
            ? number
            : throw Invalid($"{key} '{value}' non valido: serve un intero tra {minimum} e {maximum}.");
    }

    private static double ReadDouble(IConfiguration configuration, string key, double defaultValue, double minimum, double maximum)
    {
        string? value = NonEmpty(configuration[key]);

        if (value is null)
        {
            return defaultValue;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && number >= minimum && number <= maximum
            ? number
            : throw Invalid($"{key} '{value}' non valido: serve un numero tra {minimum} e {maximum} (separatore decimale: punto).");
    }

    private static bool ReadBool(IConfiguration configuration, string key, bool defaultValue)
    {
        string? value = NonEmpty(configuration[key]);

        if (value is null)
        {
            return defaultValue;
        }

        return bool.TryParse(value, out bool flag) ? flag : throw Invalid($"{key} '{value}' non valido: usare true o false.");
    }

    private static InvalidOperationException Invalid(string message) => new($"Configurazione non valida: {message}");

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
