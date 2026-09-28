using System.Net.Http.Json;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Demo;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Retrieval;

namespace Dusiburg.AI.Sinistri.Web.Api;

/// <summary>
/// Client tipizzato verso l'API per le letture e le ricerche (D16): la UI non accede né al database né ai modelli. Operazioni brevi e
/// idempotenti, quindi con il resilience handler standard di ServiceDefaults (retry compresi), come <c>ErpApiClient</c> di O2C.
/// </summary>
public sealed class SinistriApiClient(HttpClient http)
{
    /// <summary>Indirizzo dell'API; sotto l'AppHost lo risolve il service discovery.</summary>
    public const string BaseAddressConfigurationKey = "SinistriApi:BaseAddress";

    public const string DefaultBaseAddress = "http://api";

    public Task<ProbeReport> GetHealthAsync(CancellationToken cancellationToken) => GetAsync<ProbeReport>("api/health", cancellationToken);

    public Task<StatisticheDataset> GetStatisticheDatasetAsync(CancellationToken cancellationToken) =>
        GetAsync<StatisticheDataset>("api/statistiche-dataset", cancellationToken);

    public Task<ConfigurazioneDemo> GetConfigurazioneAsync(CancellationToken cancellationToken) =>
        GetAsync<ConfigurazioneDemo>("api/configurazione", cancellationToken);

    public Task<IReadOnlyList<DemoScenario>> GetScenariAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<DemoScenario>>("api/scenari-demo", cancellationToken);

    public Task<IReadOnlyList<PolizzaVoce>> CercaPolizzeAsync(string? cerca, int top, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<PolizzaVoce>>($"api/polizze?cerca={Uri.EscapeDataString(cerca ?? "")}&top={top}", cancellationToken);

    public Task<IReadOnlyList<RiparatoreVoce>> GetRiparatoriAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<RiparatoreVoce>>("api/riparatori", cancellationToken);

    public Task<SinistroDettaglio?> GetSinistroAsync(string numero, CancellationToken cancellationToken) =>
        GetOrNullAsync<SinistroDettaglio>($"api/sinistri/{Uri.EscapeDataString(numero)}", cancellationToken);

    public Task<RisultatoRicercaClausole> CercaClausoleAsync(RichiestaRicercaClausole richiesta, CancellationToken cancellationToken) =>
        PostAsync<RichiestaRicercaClausole, RisultatoRicercaClausole>("api/clausole/ricerca", richiesta, cancellationToken);

    public Task<RisultatoRicercaStorico> CercaSinistriAsync(RichiestaRicercaSinistri richiesta, CancellationToken cancellationToken) =>
        PostAsync<RichiestaRicercaSinistri, RisultatoRicercaStorico>("api/sinistri/ricerca", richiesta, cancellationToken);

    /// <summary>Markdown della scheda per il download: rendering unico, lo stesso della CLI.</summary>
    public async Task<string> GetMarkdownAsync(EsitoPreIstruttoria esito, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync("api/pre-istruttoria/markdown", esito, SinistriJson.Opzioni, cancellationToken);
        await ApiProblemaException.ThrowIfErrorAsync(response, cancellationToken);

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(uri, cancellationToken);

        return await ApiProblemaException.ReadAsync<T>(response, cancellationToken);
    }

    private async Task<T?> GetOrNullAsync<T>(string uri, CancellationToken cancellationToken) where T : class
    {
        using HttpResponseMessage response = await http.GetAsync(uri, cancellationToken);

        return response.StatusCode == System.Net.HttpStatusCode.NotFound ? null : await ApiProblemaException.ReadAsync<T>(response, cancellationToken);
    }

    private async Task<TRisposta> PostAsync<TRichiesta, TRisposta>(string uri, TRichiesta richiesta, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync(uri, richiesta, SinistriJson.Opzioni, cancellationToken);

        return await ApiProblemaException.ReadAsync<TRisposta>(response, cancellationToken);
    }
}
