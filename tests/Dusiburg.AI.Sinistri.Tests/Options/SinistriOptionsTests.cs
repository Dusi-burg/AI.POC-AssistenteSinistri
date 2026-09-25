using Dusiburg.AI.Sinistri.Core.Options;
using Microsoft.Extensions.Configuration;

namespace Dusiburg.AI.Sinistri.Tests.Options;

/// <summary>Manopole della demo (D12): default nel codice e validazione con messaggi espliciti.</summary>
public class SinistriOptionsTests
{
    [Test]
    public void FromConfiguration_Default()
    {
        //SUT
        SinistriOptions options = SinistriOptions.FromConfiguration(Configuration([]));

        Assert.That(options.OllamaEndpoint, Is.EqualTo(new Uri("http://127.0.0.1:11434")));
        Assert.That(options.OllamaChatModel, Is.EqualTo("qwen3.5:9b"));
        Assert.That(options.OllamaContextLength, Is.EqualTo(8_192));
        Assert.That(options.EmbeddingProvider, Is.EqualTo(EmbeddingProviders.Ollama));
        Assert.That(options.EmbeddingEndpoint, Is.EqualTo(options.OllamaEndpoint));
        Assert.That(options.EmbeddingModel, Is.EqualTo("bge-m3"));
        Assert.That(options.EmbeddingNumGpu, Is.EqualTo(0), "D18: l'embedding non va mai in VRAM");
        Assert.That(options.EmbeddingDimensions, Is.EqualTo(1024));
        Assert.That(options.SogliaDuplicatoCosine, Is.EqualTo(0.08));
        Assert.That(options.PromptCaptureDirectory, Is.Null);
        Assert.That(options.Warmup, Is.True);
    }

    [Test]
    public void FromConfiguration_ValoriImpostati_SovrascrivonoIDefault()
    {
        //SETUP
        IConfiguration configuration = Configuration(new()
        {
            ["OLLAMA_ENDPOINT"] = "http://gpu-box:11434",
            ["EMBEDDING_MODEL"] = "qwen3-embedding:0.6b",
            ["EMBEDDING_DIMENSIONS"] = "768",
            ["EMBEDDING_NUM_GPU"] = "auto",
            ["SOGLIA_DUPLICATO_COSINE"] = "0.12",
            ["SINISTRI_WARMUP"] = "false"
        });

        //SUT
        SinistriOptions options = SinistriOptions.FromConfiguration(configuration);

        Assert.That(options.EmbeddingEndpoint, Is.EqualTo(new Uri("http://gpu-box:11434")), "per ollama l'embedding usa l'endpoint di Ollama");
        Assert.That(options.EmbeddingModel, Is.EqualTo("qwen3-embedding:0.6b"));
        Assert.That(options.EmbeddingDimensions, Is.EqualTo(768));
        Assert.That(options.EmbeddingNumGpu, Is.Null, "auto lascia la scelta a Ollama");
        Assert.That(options.SogliaDuplicatoCosine, Is.EqualTo(0.12));
        Assert.That(options.Warmup, Is.False);
    }

    [TestCase("0")]
    [TestCase("abc")]
    [TestCase("1999")]
    [TestCase("-5")]
    public void FromConfiguration_DimensioneNonValida_Errore(string value)
    {
        //SETUP
        IConfiguration configuration = Configuration(new() { ["EMBEDDING_DIMENSIONS"] = value });

        //SUT
        Assert.That(() => SinistriOptions.FromConfiguration(configuration),
            Throws.InvalidOperationException.With.Message.Contains("EMBEDDING_DIMENSIONS").And.Message.Contains(value));
    }

    [Test]
    public void FromConfiguration_ProviderSconosciuto_Errore()
    {
        //SETUP
        IConfiguration configuration = Configuration(new() { ["EMBEDDING_PROVIDER"] = "cuda" });

        //SUT
        Assert.That(() => SinistriOptions.FromConfiguration(configuration),
            Throws.InvalidOperationException.With.Message.Contains("EMBEDDING_PROVIDER").And.Message.Contains("openai-compatible"));
    }

    [Test]
    public void FromConfiguration_OpenAiCompatibleSenzaEndpoint_Errore()
    {
        //SETUP
        IConfiguration configuration = Configuration(new() { ["EMBEDDING_PROVIDER"] = "openai-compatible" });

        //SUT
        Assert.That(() => SinistriOptions.FromConfiguration(configuration),
            Throws.InvalidOperationException.With.Message.Contains("EMBEDDING_ENDPOINT"));
    }

    [Test]
    public void FromConfiguration_OpenAiCompatibleConEndpoint_UsaQuellEndpoint()
    {
        //SETUP
        IConfiguration configuration = Configuration(new()
        {
            ["EMBEDDING_PROVIDER"] = "OpenAI-Compatible",
            ["EMBEDDING_ENDPOINT"] = "http://127.0.0.1:52625/v1"
        });

        //SUT
        SinistriOptions options = SinistriOptions.FromConfiguration(configuration);

        Assert.That(options.EmbeddingProvider, Is.EqualTo(EmbeddingProviders.OpenAiCompatible));
        Assert.That(options.EmbeddingEndpoint, Is.EqualTo(new Uri("http://127.0.0.1:52625/v1")));
    }

    [Test]
    public void FromConfiguration_OnnxSenzaPercorso_Errore()
    {
        //SETUP
        IConfiguration configuration = Configuration(new() { ["EMBEDDING_PROVIDER"] = "onnx" });

        //SUT
        Assert.That(() => SinistriOptions.FromConfiguration(configuration),
            Throws.InvalidOperationException.With.Message.Contains("EMBEDDING_ONNX_PATH"));
    }

    [TestCase("OLLAMA_ENDPOINT", "localhost:11434")]
    [TestCase("OLLAMA_NUM_CTX", "512")]
    [TestCase("SOGLIA_DUPLICATO_COSINE", "0,08")]
    [TestCase("SINISTRI_WARMUP", "si")]
    public void FromConfiguration_ValoreNonValido_ErroreConLaChiave(string key, string value)
    {
        //SETUP
        IConfiguration configuration = Configuration(new() { [key] = value });

        //SUT
        Assert.That(() => SinistriOptions.FromConfiguration(configuration),
            Throws.InvalidOperationException.With.Message.StartsWith("Configurazione non valida").And.Message.Contains(key));
    }

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
