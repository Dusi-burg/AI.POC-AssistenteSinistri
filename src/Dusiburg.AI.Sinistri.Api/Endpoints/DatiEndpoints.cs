using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Seed;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dusiburg.AI.Sinistri.Api.Endpoints;

/// <summary>Consultazione dei dati demo (fase-9b.md §2): catalogo delle clausole, elenchi paginati di polizze e sinistri, gemelli.</summary>
public static class DatiEndpoints
{
    public static IEndpointRouteBuilder MapDatiEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api").WithTags("Dati demo");

        group.MapGet("/clausole", async (Prodotto? prodotto, TipoClausola? tipo, string? testo, IConsultazioneRepository consultazione,
            CancellationToken cancellationToken) =>
            TypedResults.Ok(await consultazione.GetClausoleAsync(prodotto, tipo, testo, cancellationToken)));

        group.MapGet("/polizze/elenco", ElencaPolizzeAsync);
        group.MapGet("/sinistri", ElencaSinistriAsync);

        group.MapGet("/sinistri/{numero}/gemello", async Task<Results<Ok<string>, NotFound>> (string numero, ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
            CoppieAttese.Gemello(await CoppieAttese.LeggiAsync(loggerFactory.CreateLogger(typeof(DatiEndpoints)), cancellationToken), numero) is { } gemello
                ? TypedResults.Ok(gemello)
                : TypedResults.NotFound());

        return app;
    }

    private static async Task<Results<Ok<Pagina<PolizzaElenco>>, ValidationProblem>> ElencaPolizzeAsync(
        string? cerca, bool? soloDemo, int? pagina, int? dimensione, IConsultazioneRepository consultazione, CancellationToken cancellationToken)
    {
        if (ValidaPagina(pagina, dimensione) is { } errori)
        {
            return TypedResults.ValidationProblem(errori);
        }

        return TypedResults.Ok(await consultazione.ElencaPolizzeAsync(
            cerca, soloDemo ?? false, pagina ?? 1, dimensione ?? Paginazione.DimensioneDefault, cancellationToken));
    }

    /// <summary>Le righe si completano con il gemello tra le coppie attese, così lo storico le può marcare senza altre chiamate.</summary>
    private static async Task<Results<Ok<Pagina<SinistroElenco>>, ValidationProblem>> ElencaSinistriAsync(
        [AsParameters] ParametriElencoSinistri parametri, IConsultazioneRepository consultazione, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errori = ValidaPagina(parametri.Pagina, parametri.Dimensione) ?? [];

        if (parametri.Anno is < 1900 or > 2100)
        {
            errori["anno"] = ["anno non valido."];
        }

        if (parametri.Provincia is { Length: > 0 } provincia && (provincia.Trim().Length != 2 || !provincia.Trim().All(char.IsLetter)))
        {
            errori["provincia"] = ["la provincia è una sigla di due lettere."];
        }

        if (errori.Count > 0)
        {
            return TypedResults.ValidationProblem(errori);
        }

        var filtri = new FiltriElencoSinistri(parametri.Prodotto, parametri.Causa, parametri.Stato, parametri.Provincia, parametri.Anno,
            parametri.Riparatore, parametri.Polizza, parametri.Testo);
        Pagina<SinistroElenco> pagina = await consultazione.ElencaSinistriAsync(
            filtri, parametri.Pagina ?? 1, parametri.Dimensione ?? Paginazione.DimensioneDefault, cancellationToken);
        IReadOnlyList<CoppiaDuplicati>? coppie = await CoppieAttese.LeggiAsync(loggerFactory.CreateLogger(typeof(DatiEndpoints)), cancellationToken);

        return TypedResults.Ok(pagina with { Righe = [.. pagina.Righe.Select(s => s with { Gemello = CoppieAttese.Gemello(coppie, s.Numero) })] });
    }

    private static Dictionary<string, string[]>? ValidaPagina(int? pagina, int? dimensione)
    {
        Dictionary<string, string[]> errori = [];

        if (pagina is < 1)
        {
            errori["pagina"] = ["pagina parte da 1."];
        }

        if (dimensione is < 1 or > Paginazione.DimensioneMax)
        {
            errori["dimensione"] = [$"dimensione tra 1 e {Paginazione.DimensioneMax}."];
        }

        return errori.Count == 0 ? null : errori;
    }

    /// <summary>Parametri della query string di <c>GET /api/sinistri</c>.</summary>
    public sealed record ParametriElencoSinistri(
        Prodotto? Prodotto, CausaSinistro? Causa, StatoSinistro? Stato, string? Provincia, int? Anno, int? Riparatore, string? Polizza,
        string? Testo, int? Pagina, int? Dimensione);
}
