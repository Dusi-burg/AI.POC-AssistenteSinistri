using System.Text;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;

namespace Dusiburg.AI.Sinistri.Core.PreIstruttoria;

/// <summary>Scheda in Markdown (fase-6.md §6): console, <c>--out</c> e download dell'API (Fase 8). Leggibile anche senza rendering.</summary>
public static class SchedaMarkdownRenderer
{
    /// <summary>
    /// I sinistri simili si mostrano tutti (<c>TopSinistri</c>), anche se nel prompt ne entrano solo i primi <c>SinistriNelPrompt</c>:
    /// le statistiche sono calcolate su questo insieme (D11) e la tabella deve corrispondere.
    /// </summary>
    public static string Render(EsitoPreIstruttoria esito)
    {
        var md = new StringBuilder();
        Dictionary<string, ClausolaTrovata> clausole = esito.Clausole.DistinctBy(c => c.Articolo).ToDictionary(c => c.Articolo);
        DatiPolizza polizza = esito.Polizza;

        Riga(md, $"# Scheda di pre-istruttoria — polizza {polizza.Numero}");
        Riga(md, $"> Generata il {esito.GenerataIl.ToString("yyyy-MM-dd HH:mm", Formati.Italiano)} · modello {esito.Modello} · tempo totale {Formati.Secondi(esito.Tempi.Totale)}");
        Riga(md);

        Riga(md, "## Dati polizza");
        Riga(md, "| Numero | Prodotto | Contraente | Validità | Massimale | Franchigia |");
        Riga(md, "|---|---|---|---|---|---|");
        Riga(md, $"| {polizza.Numero} | {polizza.Prodotto.Descrizione()} | {polizza.Contraente} ({polizza.Provincia}) | " +
            $"{polizza.Decorrenza:yyyy-MM-dd} → {polizza.Scadenza:yyyy-MM-dd} | {Formati.Euro(polizza.Massimale)} | {Formati.Euro(polizza.Franchigia)} |");
        Riga(md);

        Riga(md, "## Denuncia");
        Riga(md, $"> {esito.Richiesta.Denuncia}");
        if (esito.Richiesta.DataEvento is { } data)
        {
            Riga(md, $"> Data evento: {data:yyyy-MM-dd}");
        }
        Riga(md);

        if (esito.Scheda is { } scheda)
        {
            Scheda(md, scheda, clausole);
        }
        else
        {
            Riga(md, "## Scheda (testo libero, citazioni non validate)");
            Riga(md, esito.TestoLibero ?? "");
            Riga(md);
        }

        Simili(md, esito.Simili, esito.Statistiche);

        Riga(md, "## Possibili duplicati");
        if (esito.PossibiliDuplicati.Count == 0)
        {
            Riga(md, "Nessuna segnalazione.");
            Riga(md);
        }
        // Un riquadro per segnalazione: la riga vuota dopo ciascuna evita che il Markdown le fonda in un'unica citazione.
        foreach (SegnalazioneDuplicato s in esito.PossibiliDuplicati)
        {
            string riparatore = s.Riparatore is null ? "" : $" · riparatore {s.Riparatore}";
            Riga(md, $"> ⚠️ {s.NumeroSinistro} ({s.DataDenuncia:dd/MM/yyyy}, {s.Motivo.Descrizione()}) — distanza {Formati.Distanza(s.Distanza)} — " +
                $"contraente {s.Contraente}{riparatore} — «{Formati.Tronca(s.Descrizione, 120)}»");
            Riga(md);
        }

        Riga(md, "## Avvisi");
        if (esito.Avvisi.Count == 0)
        {
            Riga(md, "Nessun avviso.");
        }
        foreach (Avviso avviso in esito.Avvisi)
        {
            Riga(md, $"- ⚠️ {avviso.Messaggio}");
        }
        Riga(md);

        Riga(md, "## Clausole consultate");
        Riga(md, "| # | Articolo | Tipo | Titolo | Distanza | Integrativa |");
        Riga(md, "|---|---|---|---|---|---|");
        foreach (ClausolaTrovata c in esito.Clausole)
        {
            Riga(md, $"| {c.Rank} | {c.Articolo} | {c.Tipo.Descrizione()} | {c.Titolo} | {Formati.Distanza(c.Distanza)} | {(c.Integrativa ? "sì" : "")} |");
        }

        return md.ToString();
    }

    private static void Scheda(StringBuilder md, SchedaPreIstruttoria scheda, Dictionary<string, ClausolaTrovata> clausole)
    {
        Riga(md, "## Garanzie operanti");
        Elenco(md, scheda.GaranzieOperanti.Select(g => $"- **{Titolo(g.Articolo, clausole)}**: {g.Motivazione}"));

        Riga(md, "## Esclusioni da verificare");
        Elenco(md, scheda.EsclusioniDaVerificare.Select(e => $"- **{Titolo(e.Articolo, clausole)}**: {e.CosaVerificare}"));

        Riga(md, "## Franchigia applicabile");
        Riga(md, scheda.FranchigiaApplicabile is { } f ? $"**{Titolo(f.Articolo, clausole)}** — {f.Descrizione}" : "Nessuna indicata.");
        Riga(md);

        Riga(md, "## Punti da chiarire con il cliente");
        Elenco(md, scheda.PuntiDaChiarireConCliente.Select((p, i) => $"{i + 1}. {p}"));

        Riga(md, "## Valutazione sintetica");
        Riga(md, scheda.ValutazioneSintetica);
        Riga(md);
    }

    private static void Simili(StringBuilder md, IReadOnlyList<SinistroSimile> simili, StatisticheSimili statistiche)
    {
        Riga(md, $"## Sinistri simili ({simili.Count} più vicini)");
        if (simili.Count == 0)
        {
            Riga(md, "Nessun sinistro simile nello storico.");
        }
        else
        {
            Riga(md, "| Numero | Data | Causa | Stato | Liquidato | Distanza |");
            Riga(md, "|---|---|---|---|---|---|");
            foreach (SinistroSimile s in simili)
            {
                Riga(md, $"| {s.Numero} | {s.DataEvento:yyyy-MM-dd} | {s.Causa.Descrizione()} | {s.Stato} | {Formati.EuroIntero(s.ImportoLiquidato)} | {Formati.Distanza(s.Distanza)} |");
            }
        }
        Riga(md);

        Riga(md, "## Statistiche sui casi simili (da database)");
        Riga(md, "| Casi | Respinti | Liquidato min | Mediana | Max |");
        Riga(md, "|---|---|---|---|---|");
        Riga(md, $"| {statistiche.NumeroCasi} | {statistiche.Respinti} ({Formati.Percentuale(statistiche.PercentualeRespinti)}) | " +
            $"{Formati.EuroIntero(statistiche.LiquidatoMin)} | {Formati.EuroIntero(statistiche.LiquidatoMediana)} | {Formati.EuroIntero(statistiche.LiquidatoMax)} |");
        Riga(md);
    }

    private static string Titolo(string articolo, Dictionary<string, ClausolaTrovata> clausole) =>
        clausole.TryGetValue(articolo, out ClausolaTrovata? c) ? $"{articolo} — {c.Titolo}" : articolo;

    private static void Elenco(StringBuilder md, IEnumerable<string> righe)
    {
        bool vuoto = true;

        foreach (string riga in righe)
        {
            Riga(md, riga);
            vuoto = false;
        }

        if (vuoto)
        {
            Riga(md, "Nessuna voce.");
        }

        Riga(md);
    }

    private static void Riga(StringBuilder md, string testo = "") => md.Append(testo).Append('\n');
}
