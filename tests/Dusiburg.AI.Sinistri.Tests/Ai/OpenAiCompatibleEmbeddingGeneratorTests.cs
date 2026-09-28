using System.Net;
using System.Text;
using System.Text.Json;
using Dusiburg.AI.Sinistri.Ai.OpenAiCompatible;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.Tests.Ai;

/// <summary>Provider <c>openai-compatible</c> (FastFlowLM, Lemonade) con un server HTTP finto.</summary>
public class OpenAiCompatibleEmbeddingGeneratorTests
{
    [Test]
    public async Task GenerateAsync_InviaModelloTestiEFormatoFloat_ERispettaLIndice()
    {
        //SETUP
        var handler = new RecordingHandler("""
            { "model": "embed-gemma:300m", "data": [
                { "index": 1, "embedding": [0.0, 1.0] },
                { "index": 0, "embedding": [1.0, 0.0] } ] }
            """);
        using var generator = new OpenAiCompatibleEmbeddingGenerator(
            new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:52625/v1/") }, "embed-gemma:300m", 2);

        //SUT
        GeneratedEmbeddings<Embedding<float>> embeddings = await generator.GenerateAsync(["primo", "secondo"]);

        Assert.That(handler.RequestUri, Is.EqualTo(new Uri("http://127.0.0.1:52625/v1/embeddings")));
        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        Assert.That(body.RootElement.GetProperty("model").GetString(), Is.EqualTo("embed-gemma:300m"));
        Assert.That(body.RootElement.GetProperty("encoding_format").GetString(), Is.EqualTo("float"));
        Assert.That(body.RootElement.GetProperty("input").EnumerateArray().Select(e => e.GetString()), Is.EqualTo(new[] { "primo", "secondo" }));
        Assert.That(embeddings.Select(e => e.Vector.ToArray()), Is.EqualTo(new[] { new[] { 1f, 0f }, new[] { 0f, 1f } }));
    }

    [Test]
    public void GenerateAsync_MenoVettoriDeiTesti_Errore()
    {
        //SETUP
        var handler = new RecordingHandler("""{ "data": [ { "index": 0, "embedding": [1.0, 0.0] } ] }""");
        using var generator = new OpenAiCompatibleEmbeddingGenerator(
            new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:52625/v1/") }, "embed-gemma:300m", 2);

        //SUT
        Assert.That(async () => await generator.GenerateAsync(["primo", "secondo"]),
            Throws.InvalidOperationException.With.Message.Contains("1 vettori per 2 testi"));
    }

    private sealed class RecordingHandler(string json) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
