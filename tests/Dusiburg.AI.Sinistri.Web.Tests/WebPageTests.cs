using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Web.Api;
using Dusiburg.AI.Sinistri.Web.PreIstruttoria;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Web.Tests;

/// <summary>UI Razor Pages in-process (fase-8.md §5), con l'API sostituita da un handler HTTP con risposte preparate.</summary>
public class WebPageTests
{
    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task Stato_MostraIControlliDellApi()
    {
        //SETUP
        var report = new ProbeReport(
        [
            new ProbeResult(1, "Connessione SQL", ProbeStatus.Ok, "(localdb)\\localdev", TimeSpan.FromMilliseconds(12)),
            new ProbeResult(8, "Dimensione embedding", ProbeStatus.Error, "768 ma EMBEDDING_DIMENSIONS=1024", TimeSpan.Zero)
        ]);
        await using WebApplicationFactory<WebEntryPoint> factory = Factory(new ApiPreparata().Json("/api/health", report));
        using HttpClient client = factory.CreateClient();

        //SUT
        string page = await client.GetStringAsync("/Stato", CancellationToken);

        Assert.That(page, Does.Contain("Connessione SQL").And.Contain("badge text-bg-success"));
        Assert.That(page, Does.Contain("Dimensione embedding").And.Contain("badge text-bg-danger").And.Contain("EMBEDDING_DIMENSIONS=1024"));
        Assert.That(page, Does.Contain("sistema non pronto"), "indicatore del layout rosso");
    }

    [Test]
    public async Task Index_Scenario1_PrecompilaIlForm()
    {
        //SETUP
        await using WebApplicationFactory<WebEntryPoint> factory = Factory(new ApiPreparata()
            .Json("/api/polizze", new[] { new PolizzaVoce("CF-DEMO-000001", Core.Dominio.Prodotto.CasaFabbricati, "Mario Bianchi", "MI", default, default, true) })
            .Json("/api/riparatori", new[] { new RiparatoreVoce(6, "Idraulica Rossi") }));
        using HttpClient client = factory.CreateClient();

        //SUT
        string page = await client.GetStringAsync("/?scenario=1", CancellationToken);

        Assert.That(page, Does.Contain("value=\"CF-DEMO-000001\"").And.Contain("Rottura di un tubo nel bagno del piano superiore"));
        Assert.That(page, Does.Contain("demo · Mario Bianchi").And.Contain("Idraulica Rossi"));
        Assert.That(page, Does.Contain("Scenario 4").And.Not.Contain("Scenario 5"), "solo gli scenari di pre-istruttoria");
    }

    [Test]
    public async Task Index_EsitoConCitazioni_MostraLinkAlleClausole()
    {
        //SETUP
        await using WebApplicationFactory<WebEntryPoint> factory = Factory(new ApiPreparata(), dashboard: "https://localhost:17379");
        EsitoSalvato salvato = factory.Services.GetRequiredService<EsitiRecenti>().Salva(DatiDemo.Esito(), "4bf92f3577b34da6a3ce929d0e0e4736");
        using HttpClient client = factory.CreateClient();

        //SUT
        string page = await client.GetStringAsync($"/?esito={salvato.Id:N}", CancellationToken);

        foreach (var clausola in DatiDemo.Clausole)
        {
            Assert.That(page, Does.Contain($"data-dialog=\"clausola-{clausola.Id}\">{clausola.Articolo} — {clausola.Titolo}</button>"));
            Assert.That(page, Does.Contain($"<dialog id=\"clausola-{clausola.Id}\"").And.Contain(clausola.Testo));
        }

        Assert.That(page, Does.Contain("calcolate dal database, non dal modello"));
        Assert.That(page, Does.Contain("SIN-2026-000024</a>").And.Contain("stesso contraente"));
        Assert.That(page, Does.Contain("Art. 7.7 citato dal modello ma non tra le clausole recuperate"));
        Assert.That(page, Does.Contain("https://localhost:17379/traces/detail/4bf92f3577b34da6a3ce929d0e0e4736"));
        Assert.That(page, Does.Contain($"href=\"/?esito={salvato.Id:N}&amp;handler=Scarica\""));
    }

    [Test]
    public async Task Index_ApiNonRaggiungibile_MessaggioChiaro()
    {
        //SETUP
        await using WebApplicationFactory<WebEntryPoint> factory = Factory(new ApiPreparata());
        using HttpClient client = factory.CreateClient();

        //SUT
        HttpResponseMessage response = await client.GetAsync("/", CancellationToken);
        string page = await response.Content.ReadAsStringAsync(CancellationToken);
        string esitoScaduto = await client.GetStringAsync($"/?esito={Guid.NewGuid():N}", CancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(page, Does.Contain("Elenco di polizze e riparatori non disponibile").And.Contain("API non raggiungibile"));
        Assert.That(esitoScaduto, Does.Contain("La scheda richiesta non è più disponibile"));
    }

    [Test]
    public async Task Stream_InoltraIPassiESalvaLaScheda()
    {
        //SETUP
        string sse = Sse(EventiPreIstruttoria.Passo, new AvanzamentoPreIstruttoria("polizza", null))
            + Sse(EventiPreIstruttoria.Passo, new AvanzamentoPreIstruttoria("polizza", TimeSpan.FromMilliseconds(250)))
            + Sse(EventiPreIstruttoria.Esito, DatiDemo.Esito());
        await using WebApplicationFactory<WebEntryPoint> factory = Factory(new ApiPreparata().Stream("/api/pre-istruttoria/stream", sse));
        using HttpClient client = factory.CreateClient();

        //SUT
        HttpResponseMessage response = await client.PostAsJsonAsync(PreIstruttoriaStreamEndpoint.Percorso,
            new RichiestaPreIstruttoria("Tubo rotto.", "CF-DEMO-000001"), SinistriJson.Opzioni, CancellationToken);
        string corpo = await response.Content.ReadAsStringAsync(CancellationToken);

        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
        Assert.That(corpo, Does.Contain("{\"passo\":\"polizza\",\"secondi\":null}").And.Contain("{\"passo\":\"polizza\",\"secondi\":0.25}"));

        string id = corpo.Split("/?esito=")[1][..32];
        Assert.That(corpo, Does.Contain("event: fine"));
        Assert.That(factory.Services.GetRequiredService<EsitiRecenti>().Trova(Guid.Parse(id))?.Esito.Polizza, Is.EqualTo(DatiDemo.Polizza));
    }

    [Test]
    public async Task Stream_ErroreDellApi_EventoErroreLeggibile()
    {
        //SETUP
        string sse = Sse(EventiPreIstruttoria.Passo, new AvanzamentoPreIstruttoria("polizza", null))
            + Sse(EventiPreIstruttoria.Errore, new { title = "Polizza inesistente", status = 404, detail = "La polizza CF-XXXX-000000 non esiste." });
        await using WebApplicationFactory<WebEntryPoint> factory = Factory(new ApiPreparata().Stream("/api/pre-istruttoria/stream", sse));
        await using WebApplicationFactory<WebEntryPoint> apiGiu = Factory(new ApiPreparata());

        //SUT
        string errore = await (await factory.CreateClient().PostAsJsonAsync(PreIstruttoriaStreamEndpoint.Percorso,
            new RichiestaPreIstruttoria("Tubo rotto.", "CF-XXXX-000000"), SinistriJson.Opzioni, CancellationToken)).Content.ReadAsStringAsync(CancellationToken);
        string nonRaggiungibile = await (await apiGiu.CreateClient().PostAsJsonAsync(PreIstruttoriaStreamEndpoint.Percorso,
            new RichiestaPreIstruttoria("Tubo rotto.", "CF-DEMO-000001"), SinistriJson.Opzioni, CancellationToken)).Content.ReadAsStringAsync(CancellationToken);

        Assert.That(errore, Does.Contain("event: errore").And.Contain("\"titolo\":\"Polizza inesistente\"").And.Contain("CF-XXXX-000000 non esiste"));
        Assert.That(errore, Does.Not.Contain("event: fine"));
        Assert.That(nonRaggiungibile, Does.Contain("event: errore").And.Contain("API non raggiungibile"));
    }

    [Test]
    public async Task Health_Raggiungibile()
    {
        //SETUP
        await using WebApplicationFactory<WebEntryPoint> factory = Factory(new ApiPreparata());
        using HttpClient client = factory.CreateClient();

        //SUT
        HttpResponseMessage response = await client.GetAsync("/health", CancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static string Sse(string evento, object dato) => $"event: {evento}\ndata: {JsonSerializer.Serialize(dato, SinistriJson.Opzioni)}\n\n";

    private static WebApplicationFactory<WebEntryPoint> Factory(ApiPreparata api, string? dashboard = null) =>
        new WebApplicationFactory<WebEntryPoint>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(SinistriApiClient.BaseAddressConfigurationKey, "http://api.test");

            if (dashboard is not null)
            {
                builder.UseSetting(DashboardOptions.Key, dashboard);
            }

            builder.ConfigureServices(services =>
            {
                services.AddHttpClient(nameof(SinistriApiClient)).ConfigurePrimaryHttpMessageHandler(() => api);
                services.AddHttpClient(nameof(PreIstruttoriaApiClient)).ConfigurePrimaryHttpMessageHandler(() => api);
            });
        });

    /// <summary>
    /// Risposte preparate per percorso. Un percorso sconosciuto simula un'API spenta: per la pre-istruttoria e il fraud-scan (client
    /// senza resilience handler) una <see cref="HttpRequestException"/>; per le letture un 404, perché il resilience handler standard
    /// ritenterebbe l'eccezione con attese esponenziali e rallenterebbe i test.
    /// </summary>
    private sealed class ApiPreparata : HttpMessageHandler
    {
        private readonly Dictionary<string, (string Contenuto, string MediaType)> _risposte = [];

        public ApiPreparata Json(string percorso, object dato)
        {
            _risposte[percorso] = (JsonSerializer.Serialize(dato, SinistriJson.Opzioni), "application/json");

            return this;
        }

        public ApiPreparata Stream(string percorso, string sse)
        {
            _risposte[percorso] = (sse, "text/event-stream");

            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath is { } percorso && _risposte.TryGetValue(percorso, out var risposta))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(risposta.Contenuto, Encoding.UTF8, risposta.MediaType)
                });
            }

            if (request.RequestUri?.AbsolutePath is { } lungo && (lungo.StartsWith("/api/pre-istruttoria") || lungo.StartsWith("/api/antifrode")))
            {
                throw new HttpRequestException("Connessione rifiutata (api.test)");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
