using System.Net.Http.Json;
using System.Text.Json;
using Dusiburg.AI.Sinistri.Core.Consultazione;
using Microsoft.AspNetCore.Mvc;

namespace Dusiburg.AI.Sinistri.Web.Api;

/// <summary>
/// Risposta di errore dell'API (<c>ProblemDetails</c>: 404 polizza inesistente, 422 non in vigore, 503 servizi giù, 400 dati non
/// validi) con titolo e dettaglio già in italiano, da mostrare così com'è nella pagina.
/// </summary>
public sealed class ApiProblemaException(int status, string titolo, string dettaglio)
    : Exception($"{titolo}: {dettaglio}")
{
    public int Status { get; } = status;

    public string Titolo { get; } = titolo;

    public string Dettaglio { get; } = dettaglio;

    public static ApiProblemaException Da(ProblemDetails problema) =>
        new(problema.Status ?? 500, problema.Title ?? "Errore dell'API", DettaglioDa(problema));

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await ThrowIfErrorAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<T>(SinistriJson.Opzioni, cancellationToken)
            ?? throw new ApiProblemaException((int)response.StatusCode, "Risposta vuota", $"L'API ha restituito un corpo vuoto per {response.RequestMessage?.RequestUri}.");
    }

    public static async Task ThrowIfErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ProblemDetails? problema = null;

        try
        {
            problema = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(SinistriJson.Opzioni, cancellationToken);
        }
        catch (JsonException)
        {
            // Corpo non JSON (proxy, errore del server prima dell'API): resta il codice HTTP.
        }

        throw problema is null
            ? new ApiProblemaException((int)response.StatusCode, "Errore dell'API", $"{(int)response.StatusCode} {response.ReasonPhrase}")
            : Da(problema);
    }

    /// <summary>Per un 400 di validazione il dettaglio è l'elenco degli errori per campo.</summary>
    private static string DettaglioDa(ProblemDetails problema) =>
        problema is HttpValidationProblemDetails { Errors.Count: > 0 } validazione
            ? string.Join(" ", validazione.Errors.SelectMany(e => e.Value))
            : problema.Detail ?? "";
}
