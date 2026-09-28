using System.Net;
using System.Net.Http.Json;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dusiburg.AI.Sinistri.Web.Tests;

/// <summary>API in-process (fase-8.md §5) con probe, repository, embedding e modello finti: nessun database né Ollama.</summary>
public class ApiEndpointTests
{
    private static readonly ProbeResult SqlOk = new(1, "Connessione SQL", ProbeStatus.Ok, "(localdb)\\localdev", TimeSpan.FromMilliseconds(12));

    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task Root_RestituisceIlNomeDelServizio()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = ApiFinta.Crea(controlli: [SqlOk]);
        using HttpClient client = factory.CreateClient();

        //SUT
        string body = await client.GetStringAsync("/", CancellationToken);

        Assert.That(body, Does.Contain("Dusiburg.AI.Sinistri.Api"));
    }

    [Test]
    public async Task Health_RestituisceControlli()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = ApiFinta.Crea(controlli:
        [
            SqlOk,
            new ProbeResult(4, "Database applicativo", ProbeStatus.Warning, "non esiste ancora", TimeSpan.Zero)
        ]);
        using HttpClient client = factory.CreateClient();

        //SUT
        ProbeReport? report = await client.GetFromJsonAsync<ProbeReport>("/api/health", SinistriJson.Opzioni, CancellationToken);

        Assert.That(report?.Controlli.Select(c => (c.Numero, c.Stato)), Is.EqualTo(new[] { (1, ProbeStatus.Ok), (4, ProbeStatus.Warning) }));
    }

    [Test]
    public async Task Health_ConUnControlloInErrore_Unhealthy()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = ApiFinta.Crea(controlli:
            [new ProbeResult(8, "Dimensione embedding", ProbeStatus.Error, "768 ma EMBEDDING_DIMENSIONS=1024", TimeSpan.Zero)]);
        await using WebApplicationFactory<ApiEntryPoint> soloAvvisi = ApiFinta.Crea(controlli:
            [new ProbeResult(4, "Database applicativo", ProbeStatus.Warning, "non esiste ancora", TimeSpan.Zero)]);

        //SUT
        HttpResponseMessage errore = await factory.CreateClient().GetAsync("/health", CancellationToken);
        HttpResponseMessage avvisi = await soloAvvisi.CreateClient().GetAsync("/health", CancellationToken);

        Assert.That(errore.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(avvisi.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task PreIstruttoria_PolizzaInesistente_404()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = ApiFinta.Crea(ApiFinta.PreIstruttoriaFinta, SqlOk);
        using HttpClient client = factory.CreateClient();

        //SUT
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/pre-istruttoria",
            new RichiestaPreIstruttoria("Tubo rotto.", "CF-XXXX-000000"), SinistriJson.Opzioni, CancellationToken);
        ProblemDetails? problema = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(problema, Has.Property(nameof(ProblemDetails.Title)).EqualTo("Polizza inesistente")
            .And.Property(nameof(ProblemDetails.Detail)).Contains("CF-XXXX-000000"));
    }

    [Test]
    public async Task PreIstruttoria_PolizzaNonInVigore_422()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = ApiFinta.Crea(ApiFinta.PreIstruttoriaFinta, SqlOk);
        using HttpClient client = factory.CreateClient();
        var primaDellaDecorrenza = new RichiestaPreIstruttoria("Tubo rotto.", DatiDemo.Polizza.Numero, DatiDemo.Polizza.Decorrenza.AddDays(-1));

        //SUT
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/pre-istruttoria", primaDellaDecorrenza, SinistriJson.Opzioni, CancellationToken);
        HttpResponseMessage vuota = await client.PostAsJsonAsync("/api/pre-istruttoria", new RichiestaPreIstruttoria(" ", ""), SinistriJson.Opzioni, CancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That((await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken))?.Title, Is.EqualTo("Polizza non in vigore"));
        Assert.That(vuota.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PreIstruttoriaStream_PassiPoiEsito_LettiDalClientDellaWeb()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = ApiFinta.Crea(ApiFinta.PreIstruttoriaFinta, SqlOk);
        var client = new PreIstruttoriaApiClient(factory.CreateClient());
        List<object> eventi = [];

        //SUT
        await foreach (object evento in client.StreamAsync(new RichiestaPreIstruttoria("Tubo rotto in bagno.", DatiDemo.Polizza.Numero), CancellationToken))
        {
            eventi.Add(evento);
        }

        string[] completati = [.. eventi.OfType<AvanzamentoPreIstruttoria>().Where(p => p.Durata is not null).Select(p => p.Passo)];
        Assert.That(completati, Is.EqualTo(new[] { "polizza", "embedding", "clausole", "storico", "statistiche", "generazione scheda", "antifrode" }));
        Assert.That(eventi[^1], Is.InstanceOf<EsitoPreIstruttoria>());

        var esito = (EsitoPreIstruttoria)eventi[^1];
        Assert.That(esito.Scheda?.GaranzieOperanti.Select(g => g.Articolo), Is.EqualTo(new[] { "Art. 2.4" }));
        Assert.That(esito.PossibiliDuplicati.Select(d => (d.NumeroSinistro, d.Motivo)),
            Is.EqualTo(new[] { ("SIN-2026-000024", MotivoSegnalazione.StessoContraente) }));
    }

    [Test]
    public async Task PreIstruttoriaStream_PolizzaInesistente_EventoErrore()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = ApiFinta.Crea(ApiFinta.PreIstruttoriaFinta, SqlOk);
        using HttpClient http = factory.CreateClient();

        //SUT
        HttpResponseMessage response = await http.PostAsJsonAsync("/api/pre-istruttoria/stream",
            new RichiestaPreIstruttoria("Tubo rotto.", "CF-XXXX-000000"), SinistriJson.Opzioni, CancellationToken);
        string corpo = await response.Content.ReadAsStringAsync(CancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "lo stream è già partito: l'errore viaggia come evento");
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
        Assert.That(corpo, Does.Contain("event: passo").And.Contain("event: errore").And.Contain("Polizza inesistente").And.Contain("404"));
        Assert.That(corpo, Does.Not.Contain("event: esito"));
    }

    [Test]
    public async Task SinistriRicerca_PassaIFiltri()
    {
        //SETUP
        var sinistri = new SinistriFinti();
        await using WebApplicationFactory<ApiEntryPoint> factory = ApiFinta.Crea(services =>
        {
            ApiFinta.PreIstruttoriaFinta(services);
            ApiFinta.Sostituisci<ISinistroRepository>(services, sinistri);
        }, SqlOk);
        using HttpClient client = factory.CreateClient();

        //SUT
        HttpResponseMessage conFiltri = await client.PostAsJsonAsync("/api/sinistri/ricerca",
            new RichiestaRicercaSinistri("sovratensione", Prodotto.CasaFabbricati, " mi ", 5000m, CausaSinistro.FenomenoElettrico, 3, 7),
            SinistriJson.Opzioni, CancellationToken);
        HttpResponseMessage senzaFiltri = await client.PostAsJsonAsync("/api/sinistri/ricerca",
            new RichiestaRicercaSinistri("errore di calcolo", Prodotto.RcProfTecnici), SinistriJson.Opzioni, CancellationToken);
        HttpResponseMessage senzaTesto = await client.PostAsJsonAsync("/api/sinistri/ricerca",
            new RichiestaRicercaSinistri("", Prodotto.RcProfTecnici), SinistriJson.Opzioni, CancellationToken);

        Assert.That(conFiltri.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(senzaFiltri.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(senzaTesto.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(sinistri.Ricerche, Is.EqualTo(new[]
        {
            (new FiltriStorico(Prodotto.CasaFabbricati, 3, "MI", 5000m, CausaSinistro.FenomenoElettrico), 7),
            (new FiltriStorico(Prodotto.RcProfTecnici, 5), 10)
        }));
    }

    [Test]
    public async Task Markdown_StessoRendererDellaCli()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = ApiFinta.Crea(controlli: [SqlOk]);
        var client = new SinistriApiClient(factory.CreateClient());

        //SUT
        string markdown = await client.GetMarkdownAsync(DatiDemo.Esito(), CancellationToken);

        Assert.That(markdown, Is.EqualTo(SchedaMarkdownRenderer.Render(DatiDemo.Esito())));
    }
}
