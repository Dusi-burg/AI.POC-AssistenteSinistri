using System.Text.Json;

namespace Dusiburg.AI.Sinistri.Core.PreIstruttoria;

public sealed record RisultatoParsing(SchedaPreIstruttoria? Scheda, string? Errore)
{
    public bool Riuscito => Scheda is not null;
}

/// <summary>
/// Lettura della scheda restituita dal modello (fase-6.md §5): toglie recinti Markdown e testo attorno all'oggetto (primo <c>{</c> …
/// ultimo <c>}</c>), deserializza in camelCase e controlla i campi obbligatori. Non lancia mai: l'errore torna nel risultato.
/// </summary>
public static class SchedaParser
{
    /// <summary>Stesse opzioni per lo schema chiesto al modello e per la lettura della risposta.</summary>
    public static readonly JsonSerializerOptions Opzioni = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    public static RisultatoParsing Parse(string? risposta)
    {
        if (string.IsNullOrWhiteSpace(risposta))
        {
            return new RisultatoParsing(null, "risposta vuota");
        }

        int inizio = risposta.IndexOf('{', StringComparison.Ordinal);
        int fine = risposta.LastIndexOf('}');

        if (inizio < 0 || fine <= inizio)
        {
            return new RisultatoParsing(null, "nessun oggetto JSON nella risposta");
        }

        SchedaPreIstruttoria? scheda;

        try
        {
            scheda = JsonSerializer.Deserialize<SchedaPreIstruttoria>(risposta[inizio..(fine + 1)], Opzioni);
        }
        catch (JsonException exception)
        {
            return new RisultatoParsing(null, exception.Message);
        }

        if (scheda is null || string.IsNullOrWhiteSpace(scheda.ValutazioneSintetica))
        {
            return new RisultatoParsing(null, "campo valutazioneSintetica mancante o vuoto");
        }

        // Liste mancanti trattate come vuote: il modello a volte omette le sezioni senza voci.
        return new RisultatoParsing(scheda with
        {
            GaranzieOperanti = scheda.GaranzieOperanti ?? [],
            EsclusioniDaVerificare = scheda.EsclusioniDaVerificare ?? [],
            PuntiDaChiarireConCliente = scheda.PuntiDaChiarireConCliente ?? []
        }, null);
    }
}
