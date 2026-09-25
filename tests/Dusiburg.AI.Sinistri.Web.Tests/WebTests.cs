using System.Net;
using System.Text;
using System.Text.Json;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dusiburg.AI.Sinistri.Web.Tests;

/// <summary>UI Razor Pages in-process, con l'API sostituita da un handler HTTP finto.</summary>
public class WebTests
{
    private static CancellationToken CancellationToken => TestContext.CurrentContext.CancellationToken;

    [Test]
    public async Task Home_MostraIControlliDellApi()
    {
        //SETUP
        var report = new ProbeReport(
        [
            new ProbeResult(1, "Connessione SQL", ProbeStatus.Ok, "(localdb)\\localdev", TimeSpan.FromMilliseconds(12)),
            new ProbeResult(8, "Dimensione embedding", ProbeStatus.Error, "1024 ma EMBEDDING_DIMENSIONS=768", TimeSpan.Zero)
        ]);
        await using WebApplicationFactory<WebEntryPoint> factory = Factory(new StubApiHandler(JsonSerializer.Serialize(report, JsonSerializerOptions.Web)));
        using HttpClient client = factory.CreateClient();

        //SUT
        string page = await client.GetStringAsync("/", CancellationToken);

        Assert.That(page, Does.Contain("Connessione SQL").And.Contain("badge bg-success"));
        Assert.That(page, Does.Contain("Dimensione embedding").And.Contain("badge bg-danger").And.Contain("EMBEDDING_DIMENSIONS=768"));
    }

    [Test]
    public async Task Home_ApiNonRaggiungibile_MostraLAvviso()
    {
        //SETUP
        await using WebApplicationFactory<WebEntryPoint> factory = Factory(new StubApiHandler(json: null));
        using HttpClient client = factory.CreateClient();

        //SUT
        string page = await client.GetStringAsync("/", CancellationToken);

        Assert.That(page, Does.Contain("API non raggiungibile"));
    }

    [Test]
    public async Task Health_Raggiungibile()
    {
        //SETUP
        await using WebApplicationFactory<WebEntryPoint> factory = Factory(new StubApiHandler(json: null));
        using HttpClient client = factory.CreateClient();

        //SUT
        HttpResponseMessage response = await client.GetAsync("/health", CancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static WebApplicationFactory<WebEntryPoint> Factory(StubApiHandler handler) =>
        new WebApplicationFactory<WebEntryPoint>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(SinistriApiClient.BaseAddressConfigurationKey, "http://api.test");
            builder.ConfigureServices(services =>
                services.AddHttpClient(nameof(SinistriApiClient)).ConfigurePrimaryHttpMessageHandler(() => handler));
        });

    /// <summary>Risponde a <c>/api/health</c> con il JSON dato; senza JSON risponde 404 (non transitorio: nessun retry).</summary>
    private sealed class StubApiHandler(string? json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(json is not null && request.RequestUri?.AbsolutePath == "/api/health"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
