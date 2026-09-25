using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Ai.Ollama;
using Dusiburg.AI.Sinistri.Core.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OllamaSharp.Models;

namespace Dusiburg.AI.Sinistri.Tests.Ai;

/// <summary>Client dei modelli creati solo da configurazione; nessuna chiamata a Ollama.</summary>
public class ChatClientFactoryTests
{
    [Test]
    public void Create_Default_ThinkingSpentoTemperaturaBassaContestoDaConfigurazione()
    {
        //SETUP
        var factory = new ChatClientFactory(Options(new() { ["OLLAMA_NUM_CTX"] = "12288" }), NullLoggerFactory.Instance);

        //SUT
        using ChatModel model = factory.Create();

        Assert.That(model.ModelId, Is.EqualTo(SinistriOptions.DefaultChatModel));
        Assert.That(model.DefaultOptions.Temperature, Is.EqualTo(0.1f));
        Assert.That(model.DefaultOptions.AdditionalProperties?[OllamaOption.Think.Name], Is.EqualTo(false));
        Assert.That(model.DefaultOptions.AdditionalProperties?[OllamaOption.NumCtx.Name], Is.EqualTo(12_288));
    }

    [Test]
    public void Create_ConCartellaDiCattura_AggiungePromptCapture()
    {
        //SETUP
        var factory = new ChatClientFactory(Options(new() { ["SINISTRI_PROMPT_CAPTURE_DIR"] = Path.GetTempPath() }), NullLoggerFactory.Instance);

        //SUT
        using ChatModel model = factory.Create();

        Assert.That(model.ChatClient.GetService<PromptCaptureChatClient>(), Is.Not.Null);
    }

    [Test]
    public void EmbeddingFactory_Ollama_MetadatiDalModelloConfigurato()
    {
        //SETUP
        var factory = new EmbeddingGeneratorFactory(Options(new() { ["EMBEDDING_DIMENSIONS"] = "1024" }), NullLoggerFactory.Instance);

        //SUT
        using IEmbeddingGenerator<string, Embedding<float>> generator = factory.Create();

        EmbeddingGeneratorMetadata? metadata = generator.GetService<EmbeddingGeneratorMetadata>();
        Assert.That(metadata?.DefaultModelId, Is.EqualTo("bge-m3"));
        Assert.That(metadata?.DefaultModelDimensions, Is.EqualTo(1024));
    }

    [Test]
    public void EmbeddingFactory_ProviderNonAncoraImplementato_ErroreEsplicito()
    {
        //SETUP
        var factory = new EmbeddingGeneratorFactory(
            Options(new() { ["EMBEDDING_PROVIDER"] = "openai-compatible", ["EMBEDDING_ENDPOINT"] = "http://127.0.0.1:52625/v1" }),
            NullLoggerFactory.Instance);

        //SUT
        Assert.That(() => factory.Create(), Throws.TypeOf<NotSupportedException>().With.Message.Contains("Fase 1b"));
    }

    [TestCase("bge-m3:latest", "bge-m3", true)]
    [TestCase("bge-m3:latest", "BGE-M3:latest", true)]
    [TestCase("qwen3.5:9b", "qwen3.5", false)]
    [TestCase("qwen3.5:9b", "qwen3.5:9b", true)]
    public void SameModel_TagImplicitoLatest(string installed, string configured, bool expected)
    {
        //SUT
        Assert.That(OllamaClients.SameModel(installed, configured), Is.EqualTo(expected));
    }

    private static SinistriOptions Options(Dictionary<string, string?> settings) =>
        SinistriOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
}
