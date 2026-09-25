using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Web.Api;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dusiburg.AI.Sinistri.Web.Pages;

/// <summary>Home provvisoria (Fase 1): stato dei controlli dell'API; le pagine della demo arrivano in Fase 8.</summary>
public sealed class IndexModel(SinistriApiClient api, ILogger<IndexModel> logger) : PageModel
{
    public ProbeReport? Report { get; private set; }

    public string? ApiError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Report = await api.GetHealthAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "API non raggiungibile");
            ApiError = exception.Message;
        }
    }

    public static string BadgeClass(ProbeStatus stato) => stato switch
    {
        ProbeStatus.Ok => "bg-success",
        ProbeStatus.Info => "bg-info text-dark",
        ProbeStatus.Warning => "bg-warning text-dark",
        _ => "bg-danger"
    };
}
