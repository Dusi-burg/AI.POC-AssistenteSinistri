using Dusiburg.AI.Sinistri.Ai;
using Dusiburg.AI.Sinistri.Core.Antifrode;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Core.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dusiburg.AI.Sinistri.Web.Tests;

/// <summary>Dati fissi della demo per i test di API e Web: polizza, clausole, simili, scheda ed esito completo.</summary>
internal static class DatiDemo
{
    public static readonly DatiPolizza Polizza = new("CF-DEMO-000001", Prodotto.CasaFabbricati, "Mario Bianchi", "MI",
        new DateOnly(2025, 9, 25), new DateOnly(2028, 9, 25), 300_000m, 250m);

    public static readonly IReadOnlyList<ClausolaTrovata> Clausole =
    [
        new(21, "Art. 2.4", TipoClausola.Garanzia, "Acqua condotta", "La Società indennizza i danni da fuoriuscita di acqua condotta.", 0.52, 1, false),
        new(34, "Art. 3.4", TipoClausola.Esclusione, "Usura", "Sono esclusi i danni dovuti a usura o corrosione.", 0.54, 2, false),
        new(43, "Art. 4.3", TipoClausola.Franchigia, "Franchigia acqua condotta", "Franchigia di 150 euro per sinistro.", 0.65, 3, true),
    ];

    public static readonly IReadOnlyList<SinistroSimile> Simili =
    [
        new(1, "SIN-2025-000001", new DateOnly(2025, 3, 1), "MI", CausaSinistro.AcquaCondotta, "Tubo rotto in bagno.", "Rottura accidentale.",
            StatoSinistro.Chiuso, 3200m, 0.21)
    ];

    public static readonly StatisticheSimili Statistiche = new(1, 0, 0m, 3200m, 3200m, 3200m);

    /// <summary>Risposta del modello finto: valida secondo lo schema, cita solo articoli recuperati.</summary>
    public const string SchedaJson =
        """
        {"garanzieOperanti":[{"articolo":"Art. 2.4","motivazione":"Rottura accidentale del tubo."}],
         "esclusioniDaVerificare":[{"articolo":"Art. 3.4","cosaVerificare":"Se la rottura è dovuta a usura."}],
         "franchigiaApplicabile":{"articolo":"Art. 4.3","descrizione":"Franchigia di 150 euro."},
         "puntiDaChiarireConCliente":["Data della rottura"],"valutazioneSintetica":"Indennizzabile salvo usura."}
        """;

    public static readonly SegnalazioneDuplicato Duplicato = new("SIN-2026-000024", new DateOnly(2026, 2, 21), CausaSinistro.AcquaCondotta,
        StatoSinistro.Chiuso, "Mario Bianchi", null, 0.041, MotivoSegnalazione.StessoContraente, "Tubo rotto nel bagno.");

    public static EsitoPreIstruttoria Esito() => new(
        new RichiestaPreIstruttoria("Tubo rotto in bagno, parquet rovinato.", Polizza.Numero),
        Polizza,
        new SchedaPreIstruttoria(
            [new GaranziaOperante("Art. 2.4", "Rottura accidentale del tubo.")],
            [new EsclusioneDaVerificare("Art. 3.4", "Se la rottura è dovuta a usura.")],
            new FranchigiaApplicabile("Art. 4.3", "Franchigia di 150 euro."),
            ["Data della rottura"],
            "Indennizzabile salvo usura."),
        null,
        Clausole,
        Simili,
        Statistiche,
        [new Avviso(TipoAvviso.Citazione, "Art. 7.7 citato dal modello ma non tra le clausole recuperate: rimosso")],
        [Duplicato],
        new TempiEsecuzione(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(14.2), TimeSpan.FromMilliseconds(60), TimeSpan.FromSeconds(14.5)),
        "qwen3.5:9b",
        new DateTimeOffset(2026, 9, 28, 10, 32, 0, TimeSpan.Zero));
}

/// <summary>API in-process con i servizi esterni sostituiti: nessun database, nessun modello, nessun warm-up.</summary>
internal static class ApiFinta
{
    public static WebApplicationFactory<ApiEntryPoint> Crea(Action<IServiceCollection>? sostituzioni = null, params ProbeResult[] controlli) =>
        new WebApplicationFactory<ApiEntryPoint>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SINISTRI_WARMUP", "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHealthProbe>();
                services.AddSingleton<IHealthProbe>(new ProbeFisso(controlli));
                sostituzioni?.Invoke(services);
            });
        });

    /// <summary>Tutta la pipeline della pre-istruttoria su servizi finti: polizza, embedding, clausole, storico, antifrode, modello.</summary>
    public static void PreIstruttoriaFinta(IServiceCollection services)
    {
        Sostituisci<IPolizzaRepository>(services, new PolizzeFinte());
        Sostituisci<IEmbeddingService>(services, new EmbeddingFinto());
        Sostituisci<IClausolaRepository>(services, new ClausoleFinte());
        Sostituisci<ISinistroRepository>(services, new SinistriFinti());
        Sostituisci<IAntifrodeRepository>(services, new AntifrodeFinto());
        Sostituisci(services, new ChatModel(new ChatFinta(DatiDemo.SchedaJson), "modello-finto", new ChatOptions { Temperature = 0.1f }));
    }

    public static void Sostituisci<T>(IServiceCollection services, T istanza) where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton(istanza);
    }

    private sealed class ProbeFisso(IReadOnlyList<ProbeResult> results) : IHealthProbe
    {
        public Task<IReadOnlyList<ProbeResult>> RunAsync(CancellationToken cancellationToken) => Task.FromResult(results);
    }
}

internal sealed class PolizzeFinte : IPolizzaRepository
{
    public Task<DatiPolizza?> GetByNumeroAsync(string numero, CancellationToken cancellationToken) =>
        Task.FromResult(numero == DatiDemo.Polizza.Numero ? DatiDemo.Polizza : null);
}

internal sealed class EmbeddingFinto : IEmbeddingService
{
    public EmbeddingProfile Profile { get; } = EmbeddingProfile.ForModel("embeddinggemma");

    public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken) => Task.FromResult(new[] { 1f, 0f, 0f, 0f });

    public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<float[]>>([.. texts.Select(_ => new[] { 1f, 0f, 0f, 0f })]);
}

internal sealed class ClausoleFinte : IClausolaRepository
{
    public Task<IReadOnlyList<ClausolaTrovata>> CercaPertinentiAsync(
        float[] vettoreDenuncia, Prodotto prodotto, int top, double distanzaMaxIntegrativa, string? articoloFranchigiaBase, CancellationToken cancellationToken) =>
        Task.FromResult(DatiDemo.Clausole);
}

/// <summary>Registra filtri e top ricevuti: la ricerca dello storico deve passarli così come arrivano dalla richiesta.</summary>
internal sealed class SinistriFinti : ISinistroRepository
{
    public List<(FiltriStorico Filtri, int Top)> Ricerche { get; } = [];

    public Task<IReadOnlyList<SinistroSimile>> CercaSimiliAsync(float[] vettoreDenuncia, FiltriStorico filtri, int top, CancellationToken cancellationToken)
    {
        Ricerche.Add((filtri, top));

        return Task.FromResult(DatiDemo.Simili);
    }

    public Task<StatisticheSimili> CalcolaStatisticheAsync(IReadOnlyCollection<int> sinistriIds, CancellationToken cancellationToken) =>
        Task.FromResult(DatiDemo.Statistiche);
}

internal sealed class AntifrodeFinto : IAntifrodeRepository
{
    public Task<IReadOnlyList<SegnalazioneDuplicato>> CercaDuplicatiDenunciaAsync(
        float[] vettoreDenuncia, string numeroPolizza, int? riparatoreId, int mesi, double soglia, int top, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SegnalazioneDuplicato>>([DatiDemo.Duplicato]);

    public Task<IReadOnlyList<CoppiaSospetta>> CercaCoppieAsync(int mesi, double soglia, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CoppiaSospetta>>([]);

    public Task<IReadOnlyList<DistanzaCoppiaAttesa>> DistanzeCoppieAsync(IReadOnlyList<CoppiaDuplicati> coppie, int mesi, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DistanzaCoppiaAttesa>>([]);
}

/// <summary>Modello che risponde sempre con lo stesso testo.</summary>
internal sealed class ChatFinta(string risposta) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, risposta)));

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
