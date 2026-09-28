using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dusiburg.AI.Sinistri.Web.Pages.Dati;

/// <summary>
/// Storico dei sinistri (fase-9b.md §3.3): filtri in GET, così l'indirizzo si salva e si riapre; senza filtro di stato, il riepilogo per
/// stato dei risultati (tre conteggi con una riga ciascuno, economici).
/// </summary>
public sealed class StoricoModel(SinistriApiClient api, ILogger<StoricoModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Prodotto? Prodotto { get; set; }
    [BindProperty(SupportsGet = true)] public CausaSinistro? Causa { get; set; }
    [BindProperty(SupportsGet = true)] public StatoSinistro? Stato { get; set; }
    [BindProperty(SupportsGet = true)] public string? Provincia { get; set; }
    [BindProperty(SupportsGet = true)] public int? Anno { get; set; }
    [BindProperty(SupportsGet = true)] public int? Riparatore { get; set; }
    [BindProperty(SupportsGet = true)] public string? Testo { get; set; }
    [BindProperty(SupportsGet = true)] public int Pagina { get; set; } = 1;

    public Pagina<SinistroElenco>? Elenco { get; private set; }

    public IReadOnlyDictionary<StatoSinistro, int> PerStato { get; private set; } = new Dictionary<StatoSinistro, int>();

    public IReadOnlyList<RiparatoreVoce> Riparatori { get; private set; } = [];

    public string? Errore { get; private set; }

    public FiltriElencoSinistri Filtri => new(Prodotto, Causa, Stato, Provincia, Anno, Riparatore, Testo: Testo);

    /// <summary>Anni di denuncia proposti nel filtro: gli ultimi dieci (lo storico del seed copre cinque anni).</summary>
    public static IEnumerable<int> Anni => Enumerable.Range(0, 10).Select(i => DateTime.Today.Year - i);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Elenco = await api.ElencaSinistriAsync(Filtri, Math.Max(1, Pagina), dimensione: null, cancellationToken);

            // Pagina oltre l'ultima (filtri ristretti partendo da una pagina avanzata): si mostra l'ultima.
            if (Elenco.Numero > Elenco.Pagine)
            {
                Elenco = await api.ElencaSinistriAsync(Filtri, Elenco.Pagine, dimensione: null, cancellationToken);
            }

            if (Stato is null && Elenco.Totale > 0)
            {
                Dictionary<StatoSinistro, int> perStato = [];

                foreach (StatoSinistro stato in Enum.GetValues<StatoSinistro>())
                {
                    perStato[stato] = (await api.ElencaSinistriAsync(Filtri with { Stato = stato }, 1, dimensione: 1, cancellationToken)).Totale;
                }

                PerStato = perStato;
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

        try
        {
            Riparatori = await api.GetRiparatoriAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or ApiProblemaException)
        {
            logger.LogWarning(exception, "Riparatori non disponibili");
        }
    }
}
