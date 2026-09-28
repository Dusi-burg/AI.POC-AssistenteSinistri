using System.Text;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;

namespace Dusiburg.AI.Sinistri.Core.Valutazione;

/// <summary>Report Markdown di <c>eval</c> (fase-9.md §3): intestazione, metriche, tabella per caso, casi peggiori.</summary>
public static class EvalReportRenderer
{
    public const double ObiettivoRecall = 0.7;

    public const int CasiPeggiori = 3;

    /// <summary>Con il modello nel nome: due valutazioni di modelli diversi nello stesso minuto non si sovrascrivono.</summary>
    public static string NomeFile(RisultatoEval eval) =>
        $"report_{eval.Eseguita:yyyy-MM-dd_HHmm}_{System.Text.RegularExpressions.Regex.Replace(eval.EmbeddingModel.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-')}.md";

    public static string Render(RisultatoEval eval)
    {
        var md = new StringBuilder();
        Riga(md, $"# Valutazione del retrieval delle clausole — {eval.Eseguita.ToString("yyyy-MM-dd HH:mm", Formati.Italiano)}");
        Riga(md);
        Riga(md, "| Parametro | Valore |");
        Riga(md, "|---|---|");
        Riga(md, $"| Modello di embedding | `{eval.EmbeddingModel}` ({eval.EmbeddingProvider}, {eval.EmbeddingDimensions} dimensioni) |");
        Riga(md, $"| Database | {eval.Database ?? "—"} |");
        Riga(md, $"| Modello di chat (informativo) | `{eval.ChatModel}` |");
        Riga(md, $"| Casi del golden set | {eval.Casi.Count} |");
        Riga(md, $"| Ricerca pura | prime {eval.K} per recall, prime {EvalService.MaxRank} per il rank; nessuna clausola integrativa |");
        Riga(md, $"| Ricerca completa | top {eval.Retrieval.TopClausole}, integrative entro {Formati.Distanza(eval.Retrieval.DistanzaMaxClausolaIntegrativa)}, " +
            $"franchigia di base {eval.Retrieval.ArticoloFranchigiaBase ?? "—"} |");
        Riga(md, $"| Durata | {Formati.Secondi(eval.Durata)} |");
        Riga(md);

        Riga(md, "## Metriche");
        Riga(md);
        Riga(md, "| Metrica | Valore | Obiettivo |");
        Riga(md, "|---|---|---|");
        Riga(md, $"| recall@{eval.K} | **{Decimale(eval.RecallMedia)}** | ≥ {Decimale(ObiettivoRecall)} {(eval.RecallMedia >= ObiettivoRecall ? "✓" : "✗")} |");
        Riga(md, $"| MRR | {Decimale(eval.Mrr)} | informativa |");
        Riga(md, $"| hit@1 | {Decimale(eval.Hit1)} | informativa |");
        Riga(md, $"| recall esclusioni (ricerca completa) | {(eval.RecallEsclusioniMedia is { } r ? Decimale(r) : "—")} | informativa |");
        Riga(md);

        Riga(md, "## Per caso");
        Riga(md);
        Riga(md, $"| Caso | Prodotto | recall@{eval.K} | Rank 1° rilevante | Prime {eval.K} (✓ rilevante) | Mancanti | Esclusioni |");
        Riga(md, "|---|---|---|---|---|---|---|");

        foreach (RisultatoCaso caso in eval.Casi)
        {
            Riga(md, $"| {caso.Caso.Id} | {caso.Caso.Prodotto} | {Decimale(caso.RecallAtK)} | {caso.RankPrimoRilevante?.ToString() ?? "—"} | " +
                $"{PrimeK(caso, eval.K)} | {(caso.Mancanti.Count == 0 ? "—" : string.Join(", ", caso.Mancanti))} | " +
                $"{(caso.RecallEsclusioni is { } e ? Decimale(e) : "—")} |");
        }

        Riga(md);
        Riga(md, $"## Casi peggiori ({CasiPeggiori} recall più basse)");

        foreach (RisultatoCaso caso in eval.Casi.OrderBy(c => c.RecallAtK).ThenByDescending(c => c.RankPrimoRilevante ?? int.MaxValue)
                     .ThenBy(c => c.Caso.Id).Take(CasiPeggiori))
        {
            Riga(md);
            Riga(md, $"### {caso.Caso.Id} — recall@{eval.K} {Decimale(caso.RecallAtK)}");
            Riga(md);
            Riga(md, $"> {caso.Caso.Denuncia}");
            Riga(md);
            Riga(md, $"Attesi: {string.Join(", ", caso.Caso.Attese.Rilevanti)}{(caso.Caso.Note is { } note ? $" ({note})" : "")}.");
            Riga(md);
            Riga(md, "| # | Articolo | Tipo | Titolo | Distanza | Rilevante |");
            Riga(md, "|---|---|---|---|---|---|");

            int posizione = 0;

            foreach (ClausolaTrovata clausola in caso.Pura.Take(eval.K))
            {
                Riga(md, $"| {++posizione} | {clausola.Articolo} | {clausola.Tipo} | {clausola.Titolo} | {Formati.Distanza(clausola.Distanza)} | " +
                    $"{(caso.Caso.Attese.Rilevanti.Contains(clausola.Articolo) ? "✓" : "")} |");
            }

            string[] oltre = [.. caso.Mancanti.Select(a => caso.Pura.FirstOrDefault(c => c.Articolo == a) is { } c
                ? $"{a} al {caso.Pura.ToList().IndexOf(c) + 1}° posto ({Formati.Distanza(c.Distanza)})"
                : $"{a} oltre il {EvalService.MaxRank}° posto")];

            if (oltre.Length > 0)
            {
                Riga(md);
                Riga(md, $"Mancanti: {string.Join("; ", oltre)}.");
            }
        }

        return md.ToString();
    }

    private static string PrimeK(RisultatoCaso caso, int k) =>
        string.Join(", ", caso.Pura.Take(k).Select(c => caso.Caso.Attese.Rilevanti.Contains(c.Articolo) ? $"{c.Articolo} ✓" : c.Articolo));

    private static string Decimale(double valore) => valore.ToString("0.00", Formati.Italiano);

    private static void Riga(StringBuilder md, string testo = "") => md.Append(testo).Append('\n');
}
