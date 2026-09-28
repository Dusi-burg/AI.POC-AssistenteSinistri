using System.Text;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Microsoft.Extensions.AI;

namespace Dusiburg.AI.Sinistri.Ai.PreIstruttoria;

/// <summary>Contesto della denuncia passato al prompt: polizza, clausole recuperate, simili e statistiche dal DB.</summary>
public sealed record ContestoPrompt(
    RichiestaPreIstruttoria Richiesta,
    DatiPolizza Polizza,
    IReadOnlyList<ClausolaTrovata> Clausole,
    IReadOnlyList<SinistroSimile> Simili,
    StatisticheSimili Statistiche,
    int SinistriNelPrompt);

/// <summary>
/// Prompt della scheda (fase-6.md §3). Puro: input → messaggi, senza I/O. Nel prompt entrano solo i primi
/// <see cref="ContestoPrompt.SinistriNelPrompt"/> simili, con descrizione ed esito troncati.
/// </summary>
public static class PromptBuilder
{
    public const int MaxDescrizione = 300;
    public const int MaxEsito = 200;

    private const string Istruzioni =
        """
        Sei un assistente di pre-istruttoria sinistri per un ufficio liquidazione.
        Il tuo compito è preparare una scheda sintetica che aiuti il liquidatore a impostare la pratica.

        Regole:
        1. Usa SOLO le clausole fornite nella sezione CLAUSOLE. Non citare articoli che non compaiono lì.
        2. Ogni garanzia, esclusione o franchigia deve riportare l'articolo esattamente come scritto (es. "Art. 2.4").
        3. Ogni articolo compare in una sola sezione e una sola volta. Le clausole di tipo Franchigia vanno solo come franchigia applicabile, mai tra le garanzie.
        4. La motivazione di una garanzia deve riguardare il contenuto della clausola citata: non citare una clausola solo perché compare nell'elenco.
        5. Se un'informazione necessaria non è presente nella denuncia o nelle clausole, dichiaralo e inseriscila tra i punti da chiarire con il cliente.
        6. Non inventare importi. Gli unici numeri che puoi riportare sono quelli presenti in DATI POLIZZA, CLAUSOLE e STATISTICHE. Non fare calcoli con gli importi (percentuali, somme, limiti applicati).
        7. Indica solo le esclusioni che i fatti della denuncia potrebbero rendere applicabili, come "da verificare", spiegando quale circostanza di fatto le renderebbe applicabili.
        8. Tono professionale, frasi brevi, italiano.
        """;

    private const string RegolaJson = "9. Rispondi solo con un oggetto JSON conforme allo schema richiesto, senza testo prima o dopo.";

    private const string RegolaTesto =
        "9. Rispondi in testo semplice con queste sezioni, ciascuna con il suo titolo: Garanzie operanti, Esclusioni da verificare, " +
        "Franchigia applicabile, Punti da chiarire con il cliente, Valutazione sintetica.";

    public static string SystemPrompt(bool testoLibero = false) => $"{Istruzioni}\n{(testoLibero ? RegolaTesto : RegolaJson)}";

    /// <summary>System prompt e messaggio utente; <paramref name="testoLibero"/> per il fallback senza JSON.</summary>
    public static List<ChatMessage> Build(ContestoPrompt contesto, bool testoLibero = false) =>
    [
        new ChatMessage(ChatRole.System, SystemPrompt(testoLibero)),
        new ChatMessage(ChatRole.User, MessaggioUtente(contesto))
    ];

    public static string MessaggioUtente(ContestoPrompt contesto)
    {
        var testo = new StringBuilder();
        DatiPolizza polizza = contesto.Polizza;
        RichiestaPreIstruttoria richiesta = contesto.Richiesta;

        Riga(testo, "DATI POLIZZA");
        Riga(testo, $"Numero: {polizza.Numero} | Prodotto: {polizza.Prodotto.Descrizione()} | Contraente: {polizza.Contraente} ({polizza.Provincia})");
        Riga(testo, $"Validità: {polizza.Decorrenza:yyyy-MM-dd} → {polizza.Scadenza:yyyy-MM-dd} | Massimale: {Formati.Euro(polizza.Massimale)} | Franchigia: {Formati.Euro(polizza.Franchigia)}");
        Riga(testo);

        Riga(testo, "DENUNCIA");
        Riga(testo, richiesta.Denuncia.Trim());
        if (richiesta.DataEvento is { } data)
        {
            Riga(testo, $"Data evento: {data:yyyy-MM-dd}");
        }
        if (richiesta.CausaIndicata is { } causa)
        {
            Riga(testo, $"Causa indicata: {causa.Descrizione()}");
        }
        Riga(testo);

        Riga(testo, "CLAUSOLE");
        int n = 0;
        foreach (ClausolaTrovata clausola in contesto.Clausole)
        {
            Riga(testo, $"[C{++n}] {clausola.Articolo} ({clausola.Tipo.Descrizione()}) — {clausola.Titolo}");
            Riga(testo, clausola.Testo);
        }
        Riga(testo);

        SinistroSimile[] simili = [.. contesto.Simili.Take(contesto.SinistriNelPrompt)];
        Riga(testo, $"SINISTRI SIMILI (storico, i {simili.Length} più vicini)");
        if (simili.Length == 0)
        {
            Riga(testo, "Nessun sinistro simile nello storico.");
        }
        n = 0;
        foreach (SinistroSimile sinistro in simili)
        {
            string liquidato = sinistro.ImportoLiquidato is { } importo ? $"liquidato {Formati.Euro(importo)}" : "non liquidato";
            Riga(testo, $"[S{++n}] {sinistro.Numero} | {sinistro.DataEvento:yyyy-MM} | {sinistro.Causa.Descrizione()} | {sinistro.Stato} | {liquidato}");
            Riga(testo, $"     Descrizione: {Formati.Tronca(sinistro.Descrizione, MaxDescrizione)} | Esito perizia: {Formati.Tronca(sinistro.EsitoPerizia ?? "—", MaxEsito)}");
        }
        Riga(testo);

        StatisticheSimili statistiche = contesto.Statistiche;
        Riga(testo, "STATISTICHE (calcolate sul database, non ricalcolarle)");
        Riga(testo, $"Casi simili: {statistiche.NumeroCasi} | Respinti: {statistiche.Respinti} ({Formati.Percentuale(statistiche.PercentualeRespinti)}) | " +
            $"Liquidato min/mediana/max: {Formati.EuroIntero(statistiche.LiquidatoMin)} / {Formati.EuroIntero(statistiche.LiquidatoMediana)} / {Formati.EuroIntero(statistiche.LiquidatoMax)}");
        Riga(testo);

        testo.Append("Compila la scheda di pre-istruttoria.");

        return testo.ToString();
    }

    private static void Riga(StringBuilder testo, string riga = "") => testo.Append(riga).Append('\n');
}
