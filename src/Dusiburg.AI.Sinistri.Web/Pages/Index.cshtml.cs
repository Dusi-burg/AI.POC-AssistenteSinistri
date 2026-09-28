using System.ComponentModel.DataAnnotations;
using System.Text;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Demo;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Web.Api;
using Dusiburg.AI.Sinistri.Web.PreIstruttoria;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Web.Pages;

/// <summary>Campi del form della pre-istruttoria (fase-8.md §3.1).</summary>
public sealed class ModuloDenuncia
{
    [Required(ErrorMessage = "Indicare il numero di polizza.")]
    [Display(Name = "Polizza")]
    public string? NumeroPolizza { get; set; }

    [Required(ErrorMessage = "Scrivere il testo della denuncia.")]
    [Display(Name = "Denuncia")]
    public string? Denuncia { get; set; }

    [Display(Name = "Data evento")]
    public DateOnly? DataEvento { get; set; }

    [Display(Name = "Causa")]
    public CausaSinistro? Causa { get; set; }

    [Display(Name = "Riparatore")]
    public int? RiparatoreId { get; set; }

    public RichiestaPreIstruttoria ToRichiesta() =>
        new(Denuncia!.Trim(), NumeroPolizza!.Trim(), DataEvento, Causa, RiparatoreId);
}

/// <summary>Scheda da mostrare e link alla sua traccia nel dashboard.</summary>
public sealed record EsitoVista(EsitoSalvato Salvato, string? LinkTraccia);

/// <summary>
/// Pagina iniziale (fase-8.md §3.1): form, pulsanti degli scenari 1–4, risultato da <c>?esito={id}</c>. Con JavaScript il form usa lo
/// stream dei passi (<see cref="PreIstruttoriaStreamEndpoint"/>); senza, il POST classico genera la scheda e rimanda alla stessa
/// pagina (post-redirect-get), così la scheda ha sempre un indirizzo.
/// </summary>
public sealed class IndexModel(
    SinistriApiClient api, PreIstruttoriaApiClient preIstruttoria, EsitiRecenti esiti, IOptions<DashboardOptions> dashboard,
    ILogger<IndexModel> logger) : PageModel
{
    public const int PolizzeSuggerite = 60;

    [BindProperty]
    public ModuloDenuncia Modulo { get; set; } = new();

    public IReadOnlyList<PolizzaVoce> Polizze { get; private set; } = [];

    public IReadOnlyList<RiparatoreVoce> Riparatori { get; private set; } = [];

    public IReadOnlyList<DemoScenario> Scenari { get; } =
        [.. DemoCatalog.Scenari.Where(s => s.Tipo == TipoScenario.PreIstruttoria)];

    public EsitoVista? Esito { get; private set; }

    /// <summary>Anagrafiche non caricate: il form resta usabile scrivendo il numero di polizza a mano.</summary>
    public string? AvvisoApi { get; private set; }

    public string? Errore { get; private set; }

    /// <param name="polizza">Polizza precompilata, dal dettaglio della polizza nei dati demo (fase-9b.md §3.2).</param>
    public async Task OnGetAsync(int? scenario, Guid? esito, string? polizza, CancellationToken cancellationToken)
    {
        if (scenario is { } numero && Scenari.FirstOrDefault(s => s.Numero == numero) is { } demo)
        {
            Modulo = new ModuloDenuncia { NumeroPolizza = demo.NumeroPolizza, Denuncia = demo.Testo, Causa = demo.Causa };
        }
        else if (!string.IsNullOrWhiteSpace(polizza))
        {
            Modulo = new ModuloDenuncia { NumeroPolizza = polizza.Trim() };
        }

        if (esito is { } id)
        {
            if (esiti.Trova(id) is { } salvato)
            {
                Esito = new EsitoVista(salvato, SinistriPresentation.LinkTraccia(dashboard.Value.Url, salvato.TraceId));
                Modulo = Precompila(salvato.Esito.Richiesta);
            }
            else
            {
                Errore = "La scheda richiesta non è più disponibile (la Web è stata riavviata o sono passate più di due ore): rigenerarla.";
            }
        }

        await CaricaAnagraficheAsync(cancellationToken);
    }

    /// <summary>Senza JavaScript: generazione in un colpo solo, poi redirect alla scheda.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                EsitoPreIstruttoria esito = await preIstruttoria.GeneraAsync(Modulo.ToRichiesta(), cancellationToken);
                EsitoSalvato salvato = esiti.Salva(esito, System.Diagnostics.Activity.Current?.TraceId.ToHexString());

                return RedirectToPage(new { esito = salvato.Id.ToString("N") });
            }
            catch (ApiProblemaException problema)
            {
                Errore = $"{problema.Titolo}: {problema.Dettaglio}";
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning(exception, "API non raggiungibile durante la pre-istruttoria");
                Errore = $"API non raggiungibile: {exception.Message}";
            }
        }

        await CaricaAnagraficheAsync(cancellationToken);

        return Page();
    }

    /// <summary>Download della scheda in Markdown, reso dall'API con lo stesso renderer della CLI.</summary>
    public async Task<IActionResult> OnGetScaricaAsync(Guid esito, CancellationToken cancellationToken)
    {
        if (esiti.Trova(esito) is not { } salvato)
        {
            return NotFound();
        }

        string markdown = await api.GetMarkdownAsync(salvato.Esito, cancellationToken);
        string nome = $"scheda-{salvato.Esito.Polizza.Numero}-{salvato.Esito.GenerataIl:yyyyMMdd-HHmm}.md";

        return File(Encoding.UTF8.GetBytes(markdown), "text/markdown; charset=utf-8", nome);
    }

    public static IEnumerable<IGrouping<Prodotto, CausaSinistro>> CausePerProdotto() =>
        Enum.GetValues<CausaSinistro>().GroupBy(c => c.ProdottoDellaCausa());

    private static ModuloDenuncia Precompila(RichiestaPreIstruttoria richiesta) => new()
    {
        NumeroPolizza = richiesta.NumeroPolizza,
        Denuncia = richiesta.Denuncia,
        DataEvento = richiesta.DataEvento,
        Causa = richiesta.CausaIndicata,
        RiparatoreId = richiesta.RiparatoreId
    };

    private async Task CaricaAnagraficheAsync(CancellationToken cancellationToken)
    {
        try
        {
            Polizze = await api.CercaPolizzeAsync(cerca: null, PolizzeSuggerite, cancellationToken);
            Riparatori = await api.GetRiparatoriAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or ApiProblemaException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Anagrafiche non disponibili");
            AvvisoApi = $"Elenco di polizze e riparatori non disponibile ({exception.Message}): il numero di polizza si può scrivere a mano.";
        }
    }
}
