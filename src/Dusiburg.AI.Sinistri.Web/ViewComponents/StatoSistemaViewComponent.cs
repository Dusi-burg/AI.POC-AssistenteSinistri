using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc;

namespace Dusiburg.AI.Sinistri.Web.ViewComponents;

/// <summary>Stato nella barra di navigazione: esito complessivo dei controlli e posizionamento dei modelli (GPU/CPU).</summary>
public sealed record StatoSistema(ProbeStatus Stato, string Etichetta, string? Dettaglio);

/// <summary>
/// Indicatore del layout (fase-8.md §3): legge <c>/api/health</c>, che l'API tiene in cache 30 s. Se l'API non risponde l'indicatore è
/// rosso: la pagina si disegna comunque.
/// </summary>
public sealed class StatoSistemaViewComponent(SinistriApiClient api, ILogger<StatoSistemaViewComponent> logger) : ViewComponent
{
    /// <summary>Numero del controllo "Posizionamento modelli" in <c>fase-1.md</c> §6.</summary>
    public const int ControlloPosizionamento = 10;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        StatoSistema stato;

        try
        {
            ProbeReport report = await api.GetHealthAsync(HttpContext.RequestAborted);
            ProbeStatus complessivo = SinistriPresentation.Complessivo(report);
            string? posizionamento = report.Controlli.FirstOrDefault(c => c.Numero == ControlloPosizionamento)?.Dettaglio;
            string? errori = complessivo == ProbeStatus.Error
                ? string.Join("; ", report.Controlli.Where(c => c.Stato == ProbeStatus.Error).Select(c => $"{c.Nome}: {c.Dettaglio}"))
                : null;

            stato = new StatoSistema(complessivo, complessivo == ProbeStatus.Error ? "sistema non pronto" : "sistema pronto",
                errori ?? posizionamento);
        }
        catch (Exception exception) when (exception is HttpRequestException or ApiProblemaException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Stato del sistema non disponibile");
            stato = new StatoSistema(ProbeStatus.Error, "API non raggiungibile", exception.Message);
        }

        return View(stato);
    }
}
