using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dusiburg.AI.Sinistri.Web.Pages.Dati;

/// <summary>Dettaglio di una polizza (fase-9b.md §3.2): dati, sinistri denunciati e link alla pre-istruttoria precompilata.</summary>
public sealed class PolizzaModel(SinistriApiClient api) : PageModel
{
    /// <summary>Una polizza ha al più qualche decina di sinistri: una sola pagina, con il limite massimo dell'API.</summary>
    public const int SinistriPerPagina = Paginazione.DimensioneMax;

    public DatiPolizza? Polizza { get; private set; }

    public Pagina<SinistroElenco>? Sinistri { get; private set; }

    public string? Errore { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? numero, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return RedirectToPage("/Dati/Polizze");
        }

        try
        {
            Polizza = await api.GetPolizzaAsync(numero, cancellationToken);

            if (Polizza is null)
            {
                Errore = $"La polizza {numero} non esiste.";
                Response.StatusCode = StatusCodes.Status404NotFound;
            }
            else
            {
                Sinistri = await api.ElencaSinistriAsync(new FiltriElencoSinistri(NumeroPolizza: Polizza.Numero), 1, SinistriPerPagina, cancellationToken);
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

        return Page();
    }
}
