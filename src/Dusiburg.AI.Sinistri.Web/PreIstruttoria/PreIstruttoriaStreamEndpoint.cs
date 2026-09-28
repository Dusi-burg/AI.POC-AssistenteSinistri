using System.Diagnostics;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Web.Api;

namespace Dusiburg.AI.Sinistri.Web.PreIstruttoria;

/// <summary>
/// <c>POST /pre-istruttoria/stream</c> per lo script della pagina: il browser non vede l'API, quindi la Web inoltra gli eventi. Verso
/// il browser: <c>passo</c> {passo, secondi}, poi <c>fine</c> {url della scheda} oppure <c>errore</c> {titolo, dettaglio}.
/// La scheda non viaggia nello stream: la salva <see cref="EsitiRecenti"/> e la disegna la pagina Razor.
/// </summary>
public static class PreIstruttoriaStreamEndpoint
{
    public const string Percorso = "/pre-istruttoria/stream";

    public const string EventoPasso = "passo";

    public const string EventoFine = "fine";

    public const string EventoErrore = "errore";

    public sealed record PassoBrowser(string Passo, double? Secondi);

    public sealed record FineBrowser(string Url);

    public sealed record ErroreBrowser(string Titolo, string Dettaglio);

    public static IEndpointRouteBuilder MapPreIstruttoriaStream(this IEndpointRouteBuilder app)
    {
        app.MapPost(Percorso, (RichiestaPreIstruttoria richiesta, PreIstruttoriaApiClient api, EsitiRecenti esiti, ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
            TypedResults.ServerSentEvents(Eventi(richiesta, api, esiti, loggerFactory.CreateLogger(typeof(PreIstruttoriaStreamEndpoint)), cancellationToken)));

        return app;
    }

    internal static async IAsyncEnumerable<SseItem<object>> Eventi(
        RichiestaPreIstruttoria richiesta, PreIstruttoriaApiClient api, EsitiRecenti esiti, ILogger logger,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Il trace id della richiesta del browser è lo stesso della chiamata all'API (propagazione W3C): una sola traccia nel dashboard.
        string? traceId = Activity.Current?.TraceId.ToHexString();
        IAsyncEnumerator<object> eventi = api.StreamAsync(richiesta, cancellationToken).GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                SseItem<object>? daInviare;
                bool ultimo = false;

                try
                {
                    if (!await eventi.MoveNextAsync())
                    {
                        daInviare = Errore("Risposta incompleta", "Lo stream dell'API si è chiuso senza esito.");
                        ultimo = true;
                    }
                    else
                    {
                        (daInviare, ultimo) = eventi.Current switch
                        {
                            AvanzamentoPreIstruttoria passo => (new SseItem<object>(new PassoBrowser(passo.Passo, passo.Durata?.TotalSeconds), EventoPasso), false),
                            EsitoPreIstruttoria esito => (Fine(esiti.Salva(esito, traceId)), true),
                            ApiProblemaException problema => (Errore(problema.Titolo, problema.Dettaglio), true),
                            _ => ((SseItem<object>?)null, false)
                        };
                    }
                }
                catch (ApiProblemaException problema)
                {
                    daInviare = Errore(problema.Titolo, problema.Dettaglio);
                    ultimo = true;
                }
                catch (HttpRequestException exception)
                {
                    logger.LogWarning(exception, "API non raggiungibile durante la pre-istruttoria");
                    daInviare = Errore("API non raggiungibile", exception.Message);
                    ultimo = true;
                }

                if (daInviare is { } evento)
                {
                    yield return evento;
                }

                if (ultimo)
                {
                    yield break;
                }
            }
        }
        finally
        {
            await eventi.DisposeAsync();
        }
    }

    private static SseItem<object> Fine(EsitoSalvato salvato) => new(new FineBrowser($"/?esito={salvato.Id:N}"), EventoFine);

    private static SseItem<object> Errore(string titolo, string dettaglio) => new(new ErroreBrowser(titolo, dettaglio), EventoErrore);
}
