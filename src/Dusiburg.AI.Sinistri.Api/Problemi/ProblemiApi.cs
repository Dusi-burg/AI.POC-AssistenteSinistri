using System.Data.Common;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Dusiburg.AI.Sinistri.Api.Problemi;

/// <summary>
/// Errori dei servizi come <c>ProblemDetails</c> (fase-8.md §2): 404 polizza inesistente, 422 polizza non in vigore, 503 SQL Server o
/// Ollama non raggiungibili o embedding assenti. Lo stesso esito va nelle risposte normali e nell'evento <c>errore</c> dello stream.
/// </summary>
public static class ProblemiApi
{
    public static ProblemDetails? Da(Exception exception) => exception switch
    {
        BadHttpRequestException richiesta => Problema(richiesta.StatusCode, "Richiesta non valida", exception.Message),
        PolizzaNonTrovataException => Problema(StatusCodes.Status404NotFound, "Polizza inesistente", exception.Message),
        PolizzaNonInVigoreException => Problema(StatusCodes.Status422UnprocessableEntity, "Polizza non in vigore", exception.Message),
        ClausoleNonDisponibiliException => Problema(StatusCodes.Status503ServiceUnavailable, "Embedding non calcolati", exception.Message),
        HttpRequestException => Problema(StatusCodes.Status503ServiceUnavailable, "Modello non raggiungibile",
            $"Ollama non risponde: {exception.Message}"),
        DbException => Problema(StatusCodes.Status503ServiceUnavailable, "Database non raggiungibile",
            $"SQL Server non risponde: {exception.Message}"),
        _ => null
    };

    /// <summary>Qualsiasi altro errore: 500 con un messaggio generico, il dettaglio resta nei log e nella traccia.</summary>
    public static ProblemDetails Inatteso() =>
        Problema(StatusCodes.Status500InternalServerError, "Errore interno", "Errore inatteso: vedere i log dell'API nel dashboard.");

    public static ProblemDetails Problema(int status, string title, string detail) => new() { Status = status, Title = title, Detail = detail };
}

/// <summary>
/// Le eccezioni note diventano <c>ProblemDetails</c> con il codice di <see cref="ProblemiApi"/>; le altre restano 500. In .NET 10 il
/// middleware non registra più le eccezioni gestite da un <see cref="IExceptionHandler"/>: il log lo scrive questa classe.
/// </summary>
public sealed class SinistriExceptionHandler(IProblemDetailsService problemDetails, ILogger<SinistriExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problema = ProblemiApi.Da(exception) ?? ProblemiApi.Inatteso();

        if (problema.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "{Metodo} {Percorso}: {Titolo}", httpContext.Request.Method, httpContext.Request.Path, problema.Title);
        }
        else
        {
            logger.LogInformation("{Metodo} {Percorso}: {Status} {Titolo} ({Dettaglio})",
                httpContext.Request.Method, httpContext.Request.Path, problema.Status, problema.Title, problema.Detail);
        }

        httpContext.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
            Exception = exception
        });
    }
}
