using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Demo;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dusiburg.AI.Sinistri.Web.Pages.Storico;

/// <summary>
/// Pilastro B (fase-8.md §3.3): sinistri simili con i filtri SQL nella stessa query della distanza, e statistiche calcolate in SQL.
/// <c>?scenario=5</c> compila testo e filtri dallo scenario demo.
/// </summary>
public sealed class RicercaModel(SinistriApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Testo { get; set; }

    [BindProperty(SupportsGet = true)]
    public Prodotto Prodotto { get; set; } = Prodotto.CasaFabbricati;

    [BindProperty(SupportsGet = true)]
    public string? Provincia { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? ImportoMin { get; set; }

    [BindProperty(SupportsGet = true)]
    public CausaSinistro? Causa { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Anni { get; set; }

    public DemoScenario Scenario5 { get; } = DemoCatalog.Scenari.Single(s => s.Tipo == TipoScenario.RicercaStorico);

    public RisultatoRicercaStorico? Risultato { get; private set; }

    public string? Errore { get; private set; }

    public async Task OnGetAsync(int? scenario, CancellationToken cancellationToken)
    {
        if (scenario == Scenario5.Numero)
        {
            Testo = Scenario5.Testo;
            Prodotto = Scenario5.Prodotto ?? Prodotto;
            Provincia = Scenario5.Provincia;
            ImportoMin = Scenario5.ImportoMinimo;
            Causa = Scenario5.Causa;
        }

        if (string.IsNullOrWhiteSpace(Testo))
        {
            return;
        }

        try
        {
            Risultato = await api.CercaSinistriAsync(
                new RichiestaRicercaSinistri(Testo.Trim(), Prodotto, Provincia, ImportoMin, Causa, Anni), cancellationToken);
        }
        catch (ApiProblemaException problema)
        {
            Errore = $"{problema.Titolo}: {problema.Dettaglio}";
        }
        catch (HttpRequestException exception)
        {
            Errore = $"API non raggiungibile: {exception.Message}";
        }
    }
}
