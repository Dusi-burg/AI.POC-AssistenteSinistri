using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dusiburg.AI.Sinistri.Api.Endpoints;

/// <summary>Anagrafiche per il form della pre-istruttoria: elenco suggerito delle polizze e dei riparatori.</summary>
public static class PolizzaEndpoints
{
    public const int TopPolizzeDefault = 20;

    public const int TopPolizzeMax = 200;

    public static IEndpointRouteBuilder MapPolizzaEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api").WithTags("Anagrafiche");

        group.MapGet("/polizze", async (string? cerca, int? top, IConsultazioneRepository consultazione, CancellationToken cancellationToken) =>
            TypedResults.Ok(await consultazione.CercaPolizzeAsync(cerca, Math.Clamp(top ?? TopPolizzeDefault, 1, TopPolizzeMax), cancellationToken)));

        group.MapGet("/polizze/{numero}", async Task<Results<Ok<DatiPolizza>, NotFound>> (
            string numero, IPolizzaRepository polizze, CancellationToken cancellationToken) =>
            await polizze.GetByNumeroAsync(numero, cancellationToken) is { } polizza ? TypedResults.Ok(polizza) : TypedResults.NotFound());

        group.MapGet("/riparatori", async (IConsultazioneRepository consultazione, CancellationToken cancellationToken) =>
            TypedResults.Ok(await consultazione.GetRiparatoriAsync(cancellationToken)));

        return app;
    }
}
