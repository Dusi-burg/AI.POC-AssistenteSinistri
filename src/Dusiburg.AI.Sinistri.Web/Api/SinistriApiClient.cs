using System.Net.Http.Json;
using Dusiburg.AI.Sinistri.Core.Health;

namespace Dusiburg.AI.Sinistri.Web.Api;

/// <summary>Client tipizzato verso l'API (D16): la UI non accede né al database né ai modelli.</summary>
public sealed class SinistriApiClient(HttpClient http)
{
    /// <summary>Indirizzo dell'API; sotto l'AppHost lo risolve il service discovery.</summary>
    public const string BaseAddressConfigurationKey = "SinistriApi:BaseAddress";

    public const string DefaultBaseAddress = "http://api";

    public async Task<ProbeReport> GetHealthAsync(CancellationToken cancellationToken) =>
        await http.GetFromJsonAsync<ProbeReport>("api/health", cancellationToken)
        ?? throw new InvalidOperationException("L'API ha restituito un esito vuoto per /api/health.");
}
