using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dusiburg.AI.Sinistri.Web.Pages.Sinistri;

/// <summary>Dettaglio di un sinistro storico, dai link di simili, duplicati e fraud-scan.</summary>
public sealed class DettaglioModel(SinistriApiClient api) : PageModel
{
    public SinistroDettaglio? Sinistro { get; private set; }

    public string? Errore { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? numero, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return RedirectToPage("/Storico/Ricerca");
        }

        try
        {
            Sinistro = await api.GetSinistroAsync(numero, cancellationToken);

            if (Sinistro is null)
            {
                Errore = $"Il sinistro {numero} non esiste.";
                Response.StatusCode = StatusCodes.Status404NotFound;
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
