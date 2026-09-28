using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Microsoft.Extensions.Caching.Memory;

namespace Dusiburg.AI.Sinistri.Web.PreIstruttoria;

/// <summary>Esito salvato: la pagina lo mostra da <c>/?esito={Id}</c>; il trace id collega la scheda alla traccia nel dashboard.</summary>
public sealed record EsitoSalvato(Guid Id, EsitoPreIstruttoria Esito, string? TraceId);

/// <summary>
/// Esiti delle ultime pre-istruttorie, in memoria per <see cref="Durata"/> dall'ultimo accesso. Serve allo stream (il browser riceve
/// l'indirizzo della scheda e la carica con una GET, così la scheda la disegna Razor) e al download del Markdown. Nessuna persistenza:
/// è una demo, al riavvio della Web le schede si rigenerano.
/// </summary>
public sealed class EsitiRecenti(IMemoryCache cache)
{
    public static readonly TimeSpan Durata = TimeSpan.FromHours(2);

    public EsitoSalvato Salva(EsitoPreIstruttoria esito, string? traceId)
    {
        var salvato = new EsitoSalvato(Guid.NewGuid(), esito, traceId);
        cache.Set(Chiave(salvato.Id), salvato, new MemoryCacheEntryOptions { SlidingExpiration = Durata });

        return salvato;
    }

    public EsitoSalvato? Trova(Guid id) => cache.TryGetValue(Chiave(id), out EsitoSalvato? salvato) ? salvato : null;

    private static string Chiave(Guid id) => $"esito:{id:N}";
}
