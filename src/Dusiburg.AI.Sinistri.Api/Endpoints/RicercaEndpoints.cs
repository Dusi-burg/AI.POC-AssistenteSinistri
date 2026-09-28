using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Api.Endpoints;

/// <summary>Pilastri A e B (fase-8.md §3.2–3.3): gli stessi servizi di <c>search-clausole</c> e <c>search-sinistri</c>.</summary>
public static class RicercaEndpoints
{
    public const int TopMax = 50;

    public static IEndpointRouteBuilder MapRicercaEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api").WithTags("Ricerca");

        group.MapPost("/clausole/ricerca", CercaClausoleAsync);

        group.MapGet("/clausole/{id:int}", async Task<Results<Ok<ClausolaDettaglio>, NotFound>> (
            int id, IConsultazioneRepository consultazione, CancellationToken cancellationToken) =>
            await consultazione.GetClausolaAsync(id, cancellationToken) is { } clausola ? TypedResults.Ok(clausola) : TypedResults.NotFound());

        group.MapPost("/sinistri/ricerca", CercaSinistriAsync);

        group.MapGet("/sinistri/{numero}", async Task<Results<Ok<SinistroDettaglio>, NotFound>> (
            string numero, IConsultazioneRepository consultazione, CancellationToken cancellationToken) =>
            await consultazione.GetSinistroAsync(numero, cancellationToken) is { } sinistro ? TypedResults.Ok(sinistro) : TypedResults.NotFound());

        return app;
    }

    private static async Task<Results<Ok<RisultatoRicercaClausole>, ValidationProblem>> CercaClausoleAsync(
        RichiestaRicercaClausole richiesta, RicercaService ricerca, CancellationToken cancellationToken)
    {
        if (ValidaTesto(richiesta.Testo, richiesta.Top) is { } errori)
        {
            return TypedResults.ValidationProblem(errori);
        }

        return TypedResults.Ok(await ricerca.CercaClausoleAsync(richiesta.Testo, richiesta.Prodotto, richiesta.Top, cancellationToken));
    }

    private static async Task<Results<Ok<RisultatoRicercaStorico>, ValidationProblem>> CercaSinistriAsync(
        RichiestaRicercaSinistri richiesta, RicercaService ricerca, IOptions<RetrievalOptions> retrieval, CancellationToken cancellationToken)
    {
        if (ValidaTesto(richiesta.Testo, richiesta.Top) is { } errori)
        {
            return TypedResults.ValidationProblem(errori);
        }

        var filtri = new FiltriStorico(
            richiesta.Prodotto,
            richiesta.AnniStorico ?? retrieval.Value.AnniStorico,
            string.IsNullOrWhiteSpace(richiesta.Provincia) ? null : richiesta.Provincia.Trim().ToUpperInvariant(),
            richiesta.ImportoMin,
            richiesta.Causa);

        return TypedResults.Ok(await ricerca.CercaStoricoAsync(richiesta.Testo, filtri, richiesta.Top, cancellationToken));
    }

    private static Dictionary<string, string[]>? ValidaTesto(string testo, int? top)
    {
        Dictionary<string, string[]> errori = [];

        if (string.IsNullOrWhiteSpace(testo))
        {
            errori["testo"] = ["Il testo da cercare è obbligatorio."];
        }

        if (top is < 1 or > TopMax)
        {
            errori["top"] = [$"top deve essere tra 1 e {TopMax}."];
        }

        return errori.Count == 0 ? null : errori;
    }
}
