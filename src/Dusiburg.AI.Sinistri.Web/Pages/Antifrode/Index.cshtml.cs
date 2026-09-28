using Dusiburg.AI.Sinistri.Core.Antifrode;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Demo;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dusiburg.AI.Sinistri.Web.Pages.Antifrode;

/// <summary>Una tabella precision/recall (tutte le coppie o solo quelle con un legame) con la soglia suggerita evidenziata.</summary>
public sealed record TabellaValutazioneVista(string Titolo, IReadOnlyList<RigaValutazione> Righe, RigaValutazione? Suggerita);

/// <summary>
/// fraud-scan dalla UI (fase-8.md §3.4): coppie con le due descrizioni affiancate e, se esistono le coppie attese, la tabella
/// precision/recall. La scansione parte solo con <c>?mesi=</c> (o lo scenario 6): aprire la pagina non costa nulla.
/// </summary>
public sealed class IndexModel(PreIstruttoriaApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Mesi { get; set; }

    [BindProperty(SupportsGet = true)]
    public double? Soglia { get; set; }

    /// <summary>Tutte le coppie o solo quelle con stesso contraente o riparatore (per lo più sono testi ripetuti dai template).</summary>
    [BindProperty(SupportsGet = true)]
    public bool SoloTesto { get; set; }

    public DemoScenario Scenario6 { get; } = DemoCatalog.Scenari.Single(s => s.Tipo == TipoScenario.FraudScan);

    public RisultatoFraudScan? Risultato { get; private set; }

    public string? Errore { get; private set; }

    public IEnumerable<CoppiaSospetta> CoppieMostrate =>
        Risultato?.Coppie.Where(c => SoloTesto ? !c.ConLegame : c.ConLegame) ?? [];

    public async Task OnGetAsync(int? scenario, CancellationToken cancellationToken)
    {
        if (scenario == Scenario6.Numero)
        {
            Mesi = Scenario6.Mesi;
        }

        if (Mesi is not { } mesi)
        {
            return;
        }

        try
        {
            Risultato = await api.FraudScanAsync(new RichiestaFraudScan(mesi, Soglia), cancellationToken);
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

    public bool Attesa(CoppiaSospetta coppia) =>
        Risultato?.Valutazione?.DistanzeAttese is { } attese && ValutazioneDuplicati.Attesa(coppia, [.. attese.Select(a => a.Coppia)]);
}
