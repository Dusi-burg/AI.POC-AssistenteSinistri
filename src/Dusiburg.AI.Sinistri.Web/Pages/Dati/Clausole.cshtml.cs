using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dusiburg.AI.Sinistri.Web.Pages.Dati;

/// <summary>
/// Catalogo delle clausole (fase-9b.md §3.1): un prodotto alla volta, sezioni per tipo, testo integrale e ancora per articolo
/// (<see cref="OrdineArticoli.Ancora"/>), la stessa di <c>docs/clausole.md</c> e dei link dalla scheda.
/// </summary>
public sealed class ClausoleModel(SinistriApiClient api) : PageModel
{
    /// <summary>Stesso ordine delle sezioni di <c>docs/clausole.md</c>.</summary>
    public static IReadOnlyList<TipoClausola> OrdineTipi => ClausoleMarkdownRenderer.OrdineTipi;

    [BindProperty(SupportsGet = true)]
    public Prodotto Prodotto { get; set; } = Prodotto.CasaFabbricati;

    [BindProperty(SupportsGet = true)]
    public string? Testo { get; set; }

    public IReadOnlyList<ClausolaDettaglio> Clausole { get; private set; } = [];

    public string? Errore { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Clausole = await api.GetClausoleAsync(Prodotto, tipo: null, Testo, cancellationToken);
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
