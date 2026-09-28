using Dusiburg.AI.Sinistri.Core.Antifrode;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dusiburg.AI.Sinistri.Api.Endpoints;

/// <summary>fraud-scan dalla UI (fase-8.md §3.4), con la valutazione quando <c>data/duplicati_attesi.json</c> esiste.</summary>
public static class AntifrodeEndpoints
{
    public const int MesiMax = 120;

    public static IEndpointRouteBuilder MapAntifrodeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/antifrode/scan", ScanAsync).WithTags("Antifrode");

        return app;
    }

    private static async Task<Results<Ok<RisultatoFraudScan>, ValidationProblem>> ScanAsync(
        RichiestaFraudScan richiesta, AntifrodeService antifrode, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errori = [];

        if (richiesta.Mesi is < 1 or > MesiMax)
        {
            errori["mesi"] = [$"mesi deve essere tra 1 e {MesiMax}."];
        }

        if (richiesta.Soglia is <= 0 or > 2)
        {
            errori["soglia"] = ["soglia deve essere compresa tra 0 (escluso) e 2."];
        }

        if (errori.Count > 0)
        {
            return TypedResults.ValidationProblem(errori);
        }

        IReadOnlyList<CoppiaDuplicati>? attese = await LeggiAtteseAsync(loggerFactory.CreateLogger(typeof(AntifrodeEndpoints)), cancellationToken);

        return TypedResults.Ok(await antifrode.ScansionaAsync(richiesta.Mesi, richiesta.Soglia, attese, cancellationToken));
    }

    /// <summary>
    /// Il file si cerca nella cartella <c>data/</c> della radice del repository, come fa la CLI: l'API gira sempre dai sorgenti.
    /// Se manca, il fraud-scan va comunque, senza valutazione.
    /// </summary>
    private static async Task<IReadOnlyList<CoppiaDuplicati>?> LeggiAtteseAsync(ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            string percorso = Path.Combine(DuplicatiAttesiFile.CartellaDati(AppContext.BaseDirectory), DuplicatiAttesiFile.NomeFile);

            return File.Exists(percorso) ? (await DuplicatiAttesiFile.LeggiAsync(percorso, cancellationToken)).Coppie : null;
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Coppie attese non disponibili: fraud-scan senza valutazione");

            return null;
        }
    }
}
