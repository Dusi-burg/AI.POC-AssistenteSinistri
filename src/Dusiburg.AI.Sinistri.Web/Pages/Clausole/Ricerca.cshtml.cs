using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dusiburg.AI.Sinistri.Web.Pages.Clausole;

/// <summary>
/// Pilastro A (fase-8.md §3.2): le clausole che il modello vedrebbe per un testo, con la distanza anche come barra. Form in GET: la
/// ricerca ha un indirizzo e si può rilanciare dalla cronologia.
/// </summary>
public sealed class RicercaModel(SinistriApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Testo { get; set; }

    [BindProperty(SupportsGet = true)]
    public Prodotto Prodotto { get; set; } = Prodotto.CasaFabbricati;

    [BindProperty(SupportsGet = true)]
    public int? Top { get; set; }

    public RisultatoRicercaClausole? Risultato { get; private set; }

    public string? Errore { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Testo))
        {
            return;
        }

        try
        {
            Risultato = await api.CercaClausoleAsync(new RichiestaRicercaClausole(Testo.Trim(), Prodotto, Top), cancellationToken);
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
