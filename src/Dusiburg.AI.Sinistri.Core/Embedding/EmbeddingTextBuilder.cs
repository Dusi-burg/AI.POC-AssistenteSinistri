using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Core.Embedding;

/// <summary>
/// Contenuto da vettorizzare (fase-4.md §2), uguale per la pipeline e per il retrieval. Etichette leggibili ("Acqua condotta") e non
/// i nomi dei membri: il modello lavora meglio su testo naturale. Il prefisso del profilo lo aggiunge <see cref="IEmbeddingService"/>.
/// </summary>
public static class EmbeddingTextBuilder
{
    public static string Clausola(TipoClausola tipo, string titolo, string testo) => $"{tipo.Descrizione()} - {titolo}. {testo}";

    /// <summary>La parte "Esito perizia" si omette se l'esito non c'è (sinistri aperti).</summary>
    public static string Sinistro(CausaSinistro causa, string descrizione, string? esitoPerizia) =>
        string.IsNullOrWhiteSpace(esitoPerizia)
            ? $"Causa: {causa.Descrizione()}. {descrizione}"
            : $"Causa: {causa.Descrizione()}. {descrizione} Esito perizia: {esitoPerizia}";

    /// <summary>Denuncia nuova (Fasi 5–6): la causa si antepone solo se nota.</summary>
    public static string Denuncia(string testo, CausaSinistro? causa) =>
        causa is { } nota ? $"Causa: {nota.Descrizione()}. {testo}" : testo;

    /// <summary>
    /// Antifrode (fase-7.md §2 bis): per il sinistro indicizzato e per la denuncia nuova il solo racconto, senza causa né esito di
    /// perizia. L'esito allontana un sinistro dalla sua riformulazione (0,165 contro 0,057 misurato); la causa indicata o meno cambierebbe
    /// la distanza a seconda di come è stata compilata la richiesta.
    /// </summary>
    public static string Antifrode(string descrizione) => descrizione.Trim();
}
