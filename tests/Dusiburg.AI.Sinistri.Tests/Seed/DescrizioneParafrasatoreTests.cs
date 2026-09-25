using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.Tests.Seed;

/// <summary>Opzione <c>--parafrasa-con-llm</c> con un modello finto: nessuna chiamata a Ollama.</summary>
public class DescrizioneParafrasatoreTests
{
    [Test]
    public async Task Parafrasa_GemelliRigeneratiDagliOriginaliParafrasati()
    {
        //SETUP
        DatiSintetici dati = SyntheticDataGenerator.Genera(SyntheticDataGenerator.DefaultRandomSeed, SinistriGeneratorTests.Oggi);
        var chat = new ChatFinta(domanda => $"PARAFRASI {domanda.Length}.");
        var parafrasatore = new DescrizioneParafrasatore(chat, new ChatOptions { Temperature = 0.1f });

        //SUT
        DatiSintetici parafrasati = await parafrasatore.ParafrasaAsync(dati, avanzamento: null, CancellationToken.None);

        Dictionary<string, string> descrizioni = parafrasati.Sinistri.ToDictionary(s => s.Numero, s => s.Descrizione);
        HashSet<string> gemelli = [.. dati.Coppie.Select(c => c.Duplicato)];

        Assert.That(chat.Richieste, Is.EqualTo(DescrizioneParafrasatore.DaParafrasare(dati)));
        Assert.That(chat.Temperature, Is.All.EqualTo(0.7f));
        Assert.That(parafrasati.Sinistri.Where(s => !gemelli.Contains(s.Numero)).Select(s => s.Descrizione), Is.All.StartsWith("PARAFRASI "));
        Assert.That(dati.Coppie.Select(c => descrizioni[c.Duplicato]), Is.All.Not.StartsWith("PARAFRASI "));
        Assert.That(dati.Coppie.Select(c => descrizioni[c.Duplicato]), Is.All.Contains("PARAFRASI "));
        Assert.That(parafrasati.Sinistri.Select(s => s with { Descrizione = "" }), Is.EqualTo(dati.Sinistri.Select(s => s with { Descrizione = "" })));
    }

    [Test]
    public async Task Parafrasa_ErroreDelModello_TieneIlTestoOriginale()
    {
        //SETUP
        var parafrasatore = new DescrizioneParafrasatore(new ChatFinta(_ => throw new HttpRequestException("Ollama non raggiungibile")), new ChatOptions());

        //SUT
        string risultato = await parafrasatore.ParafrasaAsync("Si è rotto il tubo in bagno.", CancellationToken.None);

        Assert.That(risultato, Is.EqualTo("Si è rotto il tubo in bagno."));
    }

    private sealed class ChatFinta(Func<string, string> risposta) : IChatClient
    {
        public int Richieste { get; private set; }

        public List<float?> Temperature { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Richieste++;
            Temperature.Add(options?.Temperature);

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, risposta(messages.Last().Text))));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
