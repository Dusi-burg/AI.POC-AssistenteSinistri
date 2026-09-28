using Dusiburg.AI.Sinistri.Api.Health;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Demo;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Core.Options;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Api.Endpoints;

/// <summary>Stato del sistema per la pagina Stato e l'indicatore del layout (fase-8.md §3.5), scenari demo per i pulsanti.</summary>
public static class SistemaEndpoints
{
    public static IEndpointRouteBuilder MapSistemaEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api").WithTags("Sistema");

        // Esito dettagliato dei controlli (come il comando health), con la stessa cache del health check di Aspire.
        group.MapGet("/health", async (HealthReportCache cache, CancellationToken cancellationToken) =>
            TypedResults.Ok(await cache.GetAsync(cancellationToken)));

        group.MapGet("/statistiche-dataset", async (IConsultazioneRepository consultazione, CancellationToken cancellationToken) =>
            TypedResults.Ok(await consultazione.GetStatisticheDatasetAsync(cancellationToken)));

        group.MapGet("/configurazione", (SinistriOptions opzioni, IOptions<RetrievalOptions> retrieval) =>
            TypedResults.Ok(new ConfigurazioneDemo(
                opzioni.OllamaChatModel, opzioni.EmbeddingModel, opzioni.EmbeddingProvider, opzioni.EmbeddingDimensions,
                opzioni.SogliaDuplicatoCosine, opzioni.SogliaDuplicatoDenuncia, retrieval.Value)));

        group.MapGet("/scenari-demo", () => TypedResults.Ok(DemoCatalog.Scenari));

        return app;
    }
}
