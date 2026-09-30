using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dusiburg.AI.Sinistri.Web.Pages.Dati;

/// <summary>Elenco delle polizze (fase-9b.md §3.2) con il numero di sinistri; le polizze demo per prime.</summary>
public sealed class PolizzeModel(SinistriApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Cerca { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool SoloDemo { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Pagina { get; set; } = 1;

    public Pagina<PolizzaElenco>? Elenco { get; private set; }

    public string? Errore { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Elenco = await api.ElencaPolizzeAsync(Cerca, SoloDemo, Math.Max(1, Pagina), cancellationToken);

            // Pagina oltre l'ultima (ricerca ristretta partendo da una pagina avanzata): si mostra l'ultima.
            if (Elenco.Numero > Elenco.Pagine)
            {
                Elenco = await api.ElencaPolizzeAsync(Cerca, SoloDemo, Elenco.Pagine, cancellationToken);
            }
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
