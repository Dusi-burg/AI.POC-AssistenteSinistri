using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dusiburg.AI.Sinistri.Ai.PreIstruttoria;
using Dusiburg.AI.Sinistri.Api.Problemi;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dusiburg.AI.Sinistri.Api.Endpoints;

/// <summary>
/// Pre-istruttoria (fase-8.md §2): risposta unica, stream SSE con un evento per passo (scelto in review al posto del solo indicatore
/// di attesa) e rendering Markdown della scheda per il download.
/// </summary>
public static class PreIstruttoriaEndpoints
{
    public static IEndpointRouteBuilder MapPreIstruttoriaEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/pre-istruttoria").WithTags("Pre-istruttoria");

        group.MapPost("/", GeneraAsync);
        group.MapPost("/stream", Stream);
        group.MapPost("/markdown", (EsitoPreIstruttoria esito) =>
            TypedResults.Text(SchedaMarkdownRenderer.Render(esito), "text/markdown; charset=utf-8"));

        return app;
    }

    /// <summary>Gli errori noti (polizza inesistente o non in vigore, servizi giù) li traduce <see cref="SinistriExceptionHandler"/>.</summary>
    private static async Task<Results<Ok<EsitoPreIstruttoria>, ValidationProblem>> GeneraAsync(
        RichiestaPreIstruttoria richiesta, PreIstruttoriaService service, CancellationToken cancellationToken)
    {
        if (Valida(richiesta) is { } errori)
        {
            return TypedResults.ValidationProblem(errori);
        }

        return TypedResults.Ok(await service.GeneraAsync(richiesta, avanzamento: null, tokenRaw: null, cancellationToken));
    }

    /// <summary>
    /// <c>text/event-stream</c>: eventi <c>passo</c>, poi <c>esito</c> oppure <c>errore</c> (vedi <see cref="EventiPreIstruttoria"/>).
    /// Una volta partito lo stream il codice HTTP è già 200: gli errori viaggiano come evento, con lo stesso <c>ProblemDetails</c>.
    /// </summary>
    private static Results<ServerSentEventsResult<object>, ValidationProblem> Stream(
        RichiestaPreIstruttoria richiesta, PreIstruttoriaService service, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (Valida(richiesta) is { } errori)
        {
            return TypedResults.ValidationProblem(errori);
        }

        ILogger logger = loggerFactory.CreateLogger(typeof(PreIstruttoriaEndpoints));

        return TypedResults.ServerSentEvents(Eventi(richiesta, service, logger, cancellationToken));
    }

    internal static async IAsyncEnumerable<SseItem<object>> Eventi(
        RichiestaPreIstruttoria richiesta, PreIstruttoriaService service, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Channel<SseItem<object>> canale = Channel.CreateUnbounded<SseItem<object>>(new UnboundedChannelOptions { SingleReader = true });

        Task generazione = Task.Run(async () =>
        {
            try
            {
                EsitoPreIstruttoria esito = await service.GeneraAsync(richiesta, new AvanzamentoSuCanale(canale.Writer), null, cancellationToken);
                canale.Writer.TryWrite(new SseItem<object>(esito, EventiPreIstruttoria.Esito));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Pre-istruttoria in streaming non completata per la polizza {NumeroPolizza}", richiesta.NumeroPolizza);
                canale.Writer.TryWrite(new SseItem<object>(ProblemiApi.Da(exception) ?? ProblemiApi.Inatteso(), EventiPreIstruttoria.Errore));
            }
            finally
            {
                canale.Writer.Complete();
            }
        }, CancellationToken.None);

        await foreach (SseItem<object> evento in canale.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evento;
        }

        await generazione;
    }

    internal static Dictionary<string, string[]>? Valida(RichiestaPreIstruttoria richiesta)
    {
        Dictionary<string, string[]> errori = [];

        if (string.IsNullOrWhiteSpace(richiesta.Denuncia))
        {
            errori[nameof(RichiestaPreIstruttoria.Denuncia)] = ["Il testo della denuncia è obbligatorio."];
        }

        if (string.IsNullOrWhiteSpace(richiesta.NumeroPolizza))
        {
            errori[nameof(RichiestaPreIstruttoria.NumeroPolizza)] = ["Il numero di polizza è obbligatorio."];
        }

        return errori.Count == 0 ? null : errori;
    }

    /// <summary>
    /// Scrive i passi nel canale nel thread del servizio: <see cref="Progress{T}"/> li pubblicherebbe sul thread pool, senza garanzia
    /// d'ordine rispetto all'evento finale.
    /// </summary>
    private sealed class AvanzamentoSuCanale(ChannelWriter<SseItem<object>> writer) : IProgress<AvanzamentoPreIstruttoria>
    {
        public void Report(AvanzamentoPreIstruttoria value) => writer.TryWrite(new SseItem<object>(value, EventiPreIstruttoria.Passo));
    }
}
