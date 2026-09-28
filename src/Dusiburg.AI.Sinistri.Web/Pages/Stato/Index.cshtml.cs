using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dusiburg.AI.Sinistri.Web.Pages.Stato;

/// <summary>
/// Stato del sistema (fase-8.md §3.5): controlli di health, conteggi del dataset, modello degli embedding salvati, parametri in uso.
/// Solo lettura: DbInit ed embed restano da riga di comando. Ogni riquadro si carica per conto suo, così un DB assente non nasconde
/// lo stato di Ollama.
/// </summary>
public sealed class IndexModel(SinistriApiClient api, ILogger<IndexModel> logger) : PageModel
{
    public ProbeReport? Report { get; private set; }

    public StatisticheDataset? Dataset { get; private set; }

    public ConfigurazioneDemo? Configurazione { get; private set; }

    public List<string> Errori { get; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Report = await CaricaAsync("controlli", () => api.GetHealthAsync(cancellationToken));
        Configurazione = await CaricaAsync("configurazione", () => api.GetConfigurazioneAsync(cancellationToken));
        Dataset = await CaricaAsync("dataset", () => api.GetStatisticheDatasetAsync(cancellationToken));
    }

    private async Task<T?> CaricaAsync<T>(string nome, Func<Task<T>> carica) where T : class
    {
        try
        {
            return await carica();
        }
        catch (Exception exception) when (exception is HttpRequestException or ApiProblemaException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Stato: {Riquadro} non disponibile", nome);
            Errori.Add(exception is HttpRequestException ? $"API non raggiungibile: {exception.Message}" : $"{nome}: {exception.Message}");

            return null;
        }
    }
}
