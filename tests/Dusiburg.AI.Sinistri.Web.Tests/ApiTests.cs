using System.Net;
using System.Net.Http.Json;
using Dusiburg.AI.Sinistri.Core.Health;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dusiburg.AI.Sinistri.Web.Tests;

/// <summary>API in-process con i probe di SQL e Ollama sostituiti da uno finto: nessun database né modello.</summary>
public class ApiTests
{
    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task Root_RestituisceIlNomeDelServizio()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = Factory(new ProbeResult(1, "Connessione SQL", ProbeStatus.Ok, "ok", TimeSpan.Zero));
        using HttpClient client = factory.CreateClient();

        //SUT
        string body = await client.GetStringAsync("/", CancellationToken);

        Assert.That(body, Does.Contain("Dusiburg.AI.Sinistri.Api"));
    }

    [Test]
    public async Task ApiHealth_RestituisceIControlliDeiProbe()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = Factory(
            new ProbeResult(1, "Connessione SQL", ProbeStatus.Ok, "(localdb)\\localdev", TimeSpan.FromMilliseconds(12)),
            new ProbeResult(4, "Database applicativo", ProbeStatus.Warning, "non esiste ancora", TimeSpan.Zero));
        using HttpClient client = factory.CreateClient();

        //SUT
        ProbeReport? report = await client.GetFromJsonAsync<ProbeReport>("/api/health", CancellationToken);

        Assert.That(report?.Controlli.Select(c => (c.Numero, c.Stato)), Is.EqualTo(new[] { (1, ProbeStatus.Ok), (4, ProbeStatus.Warning) }));
    }

    [Test]
    public async Task Health_ConUnControlloInErrore_Unhealthy()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = Factory(
            new ProbeResult(8, "Dimensione embedding", ProbeStatus.Error, "1024 ma EMBEDDING_DIMENSIONS=768", TimeSpan.Zero));
        using HttpClient client = factory.CreateClient();

        //SUT
        HttpResponseMessage response = await client.GetAsync("/health", CancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Health_ConSoloAvvisi_Raggiungibile()
    {
        //SETUP
        await using WebApplicationFactory<ApiEntryPoint> factory = Factory(
            new ProbeResult(4, "Database applicativo", ProbeStatus.Warning, "non esiste ancora", TimeSpan.Zero));
        using HttpClient client = factory.CreateClient();

        //SUT
        HttpResponseMessage response = await client.GetAsync("/health", CancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static WebApplicationFactory<ApiEntryPoint> Factory(params ProbeResult[] results) =>
        new WebApplicationFactory<ApiEntryPoint>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHealthProbe>();
                services.AddSingleton<IHealthProbe>(new FixedProbe(results));
            }));

    private sealed class FixedProbe(IReadOnlyList<ProbeResult> results) : IHealthProbe
    {
        public Task<IReadOnlyList<ProbeResult>> RunAsync(CancellationToken cancellationToken) => Task.FromResult(results);
    }
}
