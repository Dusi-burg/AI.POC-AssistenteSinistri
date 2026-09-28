using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Ai.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.Tests.PreIstruttoria;

/// <summary>PreIstruttoriaService con repository, embedding e chat finti: retry, fallback, validità della polizza, validazione.</summary>
public class PreIstruttoriaServiceTests
{
    private const string SchedaValida =
        """
        {"garanzieOperanti":[{"articolo":"art 2.4","motivazione":"Rottura accidentale."},{"articolo":"Art. 7.7","motivazione":"Inventata."}],
         "esclusioniDaVerificare":[{"articolo":"Art. 3.4","cosaVerificare":"Usura."}],
         "franchigiaApplicabile":{"articolo":"Art. 4.3","descrizione":"Franchigia di polizza."},
         "puntiDaChiarireConCliente":["Data della rottura"],"valutazioneSintetica":"Indennizzabile salvo usura."}
        """;

    private static readonly RichiestaPreIstruttoria Richiesta = new("Rottura di un tubo in bagno.", "CF-DEMO-000001");

    [Test]
    public async Task Genera_JsonNonValido_RiprovaPoiFallback()
    {
        //SETUP
        var dueNonValide = new ChatFinta("non è json", "{\"garanzieOperanti\": [", "GARANZIE OPERANTI: Art. 2.4 ...");
        var unaNonValida = new ChatFinta("non è json", SchedaValida);

        //SUT
        EsitoPreIstruttoria fallback = await Service(dueNonValide).GeneraAsync(Richiesta, null, null, CancellationToken.None);
        EsitoPreIstruttoria riprova = await Service(unaNonValida).GeneraAsync(Richiesta, null, null, CancellationToken.None);

        Assert.That(fallback.Scheda, Is.Null);
        Assert.That(fallback.TestoLibero, Is.EqualTo("GARANZIE OPERANTI: Art. 2.4 ..."));
        Assert.That(fallback.Avvisi.Select(a => a.Messaggio), Has.Some.StartsWith("risposta del modello non valida due volte")
            .And.Some.EqualTo("scheda non strutturata: citazioni non validate."));
        Assert.That(dueNonValide.Richieste.Select(r => r.Opzioni?.ResponseFormat is ChatResponseFormatJson), Is.EqualTo(new[] { true, true, false }));
        Assert.That(dueNonValide.Richieste[1].Messaggi.Last().Text, Does.StartWith("La risposta non è un JSON valido secondo lo schema"));
        Assert.That(dueNonValide.Richieste[2].Messaggi[0].Text, Does.Contain("Rispondi in testo semplice"));

        Assert.That(riprova.Scheda, Is.Not.Null);
        Assert.That(riprova.Scheda!.GaranzieOperanti.Select(g => g.Articolo), Is.EqualTo(new[] { "Art. 2.4" }));
        Assert.That(riprova.Avvisi.Select(a => a.Tipo), Is.EquivalentTo(new[] { TipoAvviso.Parsing, TipoAvviso.Citazione }));
        Assert.That(unaNonValida.Richieste, Has.Count.EqualTo(2));
    }

    [Test]
    public void Genera_PolizzaNonInVigore_Errore()
    {
        //SETUP
        PreIstruttoriaService service = Service(new ChatFinta(SchedaValida));
        RichiestaPreIstruttoria primaDellaDecorrenza = Richiesta with { DataEvento = DatiScheda.Polizza.Decorrenza.AddDays(-1) };
        RichiestaPreIstruttoria polizzaInesistente = Richiesta with { NumeroPolizza = "CF-XXXX-000000" };

        //SUT
        Assert.That(() => service.GeneraAsync(primaDellaDecorrenza, null, null, CancellationToken.None),
            Throws.TypeOf<PolizzaNonInVigoreException>().With.Message.Contains("non è in vigore alla data evento 2025-09-24"));
        Assert.That(() => service.GeneraAsync(polizzaInesistente, null, null, CancellationToken.None),
            Throws.TypeOf<PolizzaNonTrovataException>().With.Message.Contains("CF-XXXX-000000"));
    }

    [Test]
    public async Task Genera_PolizzaScadutaSenzaDataEvento_AvvisoEScheda()
    {
        //SETUP
        var dopoLaScadenza = new TempoFisso(new DateTimeOffset(2029, 1, 10, 9, 0, 0, TimeSpan.Zero));
        List<AvanzamentoPreIstruttoria> passi = [];

        //SUT
        EsitoPreIstruttoria esito = await Service(new ChatFinta(SchedaValida), dopoLaScadenza)
            .GeneraAsync(Richiesta, new Avanzamento(passi.Add), null, CancellationToken.None);

        Assert.That(esito.Scheda, Is.Not.Null);
        Assert.That(esito.Avvisi.Select(a => a.Tipo), Has.Some.EqualTo(TipoAvviso.Polizza));
        Assert.That(passi.Where(p => p.Durata is not null).Select(p => p.Passo),
            Is.EqualTo(new[] { "polizza", "embedding", "clausole", "storico", "statistiche", "generazione scheda" }));
        Assert.That(esito.Modello, Is.EqualTo("modello-finto"));
    }

    private static PreIstruttoriaService Service(ChatFinta chat, TimeProvider? tempo = null) => new(
        new PolizzeFinte(),
        new EmbeddingFinto(),
        new ClausoleFinte(),
        new SinistriFinti(),
        new ChatModel(chat, "modello-finto", new ChatOptions { Temperature = 0.1f }),
        Microsoft.Extensions.Options.Options.Create(new RetrievalOptions()),
        tempo ?? new TempoFisso(new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.Zero)));

    private sealed class ChatFinta(params string[] risposte) : IChatClient
    {
        private readonly Queue<string> _risposte = new(risposte);

        public List<(List<ChatMessage> Messaggi, ChatOptions? Opzioni)> Richieste { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Richieste.Add(([.. messages], options));

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _risposte.Dequeue())));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class PolizzeFinte : IPolizzaRepository
    {
        public Task<DatiPolizza?> GetByNumeroAsync(string numero, CancellationToken cancellationToken) =>
            Task.FromResult(numero == DatiScheda.Polizza.Numero ? DatiScheda.Polizza : null);
    }

    private sealed class EmbeddingFinto : IEmbeddingService
    {
        public EmbeddingProfile Profile { get; } = EmbeddingProfile.ForModel("embeddinggemma");

        public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new[] { 1f, 0f, 0f, 0f });

        public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ClausoleFinte : IClausolaRepository
    {
        public Task<IReadOnlyList<ClausolaTrovata>> CercaPertinentiAsync(
            float[] vettoreDenuncia, Prodotto prodotto, int top, double distanzaMaxIntegrativa, string? articoloFranchigiaBase, CancellationToken cancellationToken) =>
            Task.FromResult(DatiScheda.Clausole);
    }

    private sealed class SinistriFinti : ISinistroRepository
    {
        public Task<IReadOnlyList<SinistroSimile>> CercaSimiliAsync(float[] vettoreDenuncia, FiltriStorico filtri, int top, CancellationToken cancellationToken) =>
            Task.FromResult(DatiScheda.Simili(3));

        public Task<StatisticheSimili> CalcolaStatisticheAsync(IReadOnlyCollection<int> sinistriIds, CancellationToken cancellationToken) =>
            Task.FromResult(DatiScheda.Statistiche);
    }

    private sealed class TempoFisso(DateTimeOffset adesso) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => adesso;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class Avanzamento(Action<AvanzamentoPreIstruttoria> registra) : IProgress<AvanzamentoPreIstruttoria>
    {
        public void Report(AvanzamentoPreIstruttoria value) => registra(value);
    }
}
