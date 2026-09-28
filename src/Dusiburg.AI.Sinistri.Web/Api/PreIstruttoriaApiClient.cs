using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dusiburg.AI.Sinistri.Core.Antifrode;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Microsoft.AspNetCore.Mvc;

namespace Dusiburg.AI.Sinistri.Web.Api;

/// <summary>
/// Operazioni lunghe verso l'API (fase-8.md §1): pre-istruttoria (15–40 s) e fraud-scan. Registrato <b>senza</b> resilience handler e
/// con timeout di <see cref="Timeout"/>: il handler standard taglierebbe il tentativo dopo pochi secondi e ripeterebbe la generazione.
/// </summary>
public sealed class PreIstruttoriaApiClient(HttpClient http)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public async Task<EsitoPreIstruttoria> GeneraAsync(RichiestaPreIstruttoria richiesta, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync("api/pre-istruttoria", richiesta, SinistriJson.Opzioni, cancellationToken);

        return await ApiProblemaException.ReadAsync<EsitoPreIstruttoria>(response, cancellationToken);
    }

    /// <summary>
    /// Eventi di <c>/api/pre-istruttoria/stream</c> man mano che arrivano: <see cref="AvanzamentoPreIstruttoria"/> per ogni passo, poi
    /// <see cref="EsitoPreIstruttoria"/> oppure <see cref="ApiProblemaException"/> (restituita, non lanciata: è l'ultimo evento).
    /// Un errore prima dello stream (400 di validazione) è invece lanciato.
    /// </summary>
    public async IAsyncEnumerable<object> StreamAsync(RichiestaPreIstruttoria richiesta, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/pre-istruttoria/stream")
        {
            Content = JsonContent.Create(richiesta, options: SinistriJson.Opzioni)
        };

        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await ApiProblemaException.ThrowIfErrorAsync(response, cancellationToken);

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (SseItem<object?> evento in SseParser.Create(stream, Leggi).EnumerateAsync(cancellationToken))
        {
            if (evento.Data is { } dato)
            {
                yield return dato;
            }
        }
    }

    public async Task<RisultatoFraudScan> FraudScanAsync(RichiestaFraudScan richiesta, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync("api/antifrode/scan", richiesta, SinistriJson.Opzioni, cancellationToken);

        return await ApiProblemaException.ReadAsync<RisultatoFraudScan>(response, cancellationToken);
    }

    /// <summary>Eventi sconosciuti ignorati: l'API può aggiungerne senza rompere la UI.</summary>
    internal static object? Leggi(string tipo, ReadOnlySpan<byte> dati) => tipo switch
    {
        EventiPreIstruttoria.Passo => JsonSerializer.Deserialize<AvanzamentoPreIstruttoria>(dati, SinistriJson.Opzioni),
        EventiPreIstruttoria.Esito => JsonSerializer.Deserialize<EsitoPreIstruttoria>(dati, SinistriJson.Opzioni),
        EventiPreIstruttoria.Errore => JsonSerializer.Deserialize<ProblemDetails>(dati, SinistriJson.Opzioni) is { } problema
            ? ApiProblemaException.Da(problema)
            : null,
        _ => null
    };
}
