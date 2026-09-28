using System.Globalization;
using System.Text;
using Dusiburg.AI.Sinistri.EmbeddingBench.Dataset;
using Dusiburg.AI.Sinistri.EmbeddingBench.Measurement;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Report;

/// <summary>Report <c>eval/embedding-bench_yyyy-MM-dd.md</c> (fase-1b.md §4) con l'applicazione della regola di scelta del §5.</summary>
internal static class MarkdownReport
{
    /// <summary>Testi del POC da indicizzare: ~60 clausole + ~400 sinistri + margine (fase-1b.md §4).</summary>
    public const int PocTexts = 470;

    private const double MaxLatencyMs = 300;
    private const double MaxIndexingSeconds = 15 * 60;
    private const double MinNpuCosine = 0.99;
    private const double MrrTie = 0.02;

    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public static string Build(SystemInfo system, BenchDataset dataset, IReadOnlyList<BenchResult> results, DateTimeOffset date)
    {
        var md = new StringBuilder();

        md.AppendLine(CultureInfo.InvariantCulture, $"# Banco di prova degli embedding — {date:yyyy-MM-dd HH:mm}");
        md.AppendLine();
        md.AppendLine(CultureInfo.InvariantCulture, $"> Generato da `tools/Dusiburg.AI.Sinistri.EmbeddingBench` su `data/embedding_bench.json`: {dataset.Documenti.Count} clausole, {dataset.Casi.Count} casi, {dataset.CoppieDuplicate.Count} coppie riformulate e {dataset.CoppieStessoTema.Count} coppie \"stesso tema\". Distanze coseno (1 − similarità), come `VECTOR_DISTANCE` di SQL Server.");
        md.AppendLine();

        AppendSystem(md, system);
        AppendSummary(md, results);
        AppendDuplicates(md, results);
        AppendCoexistence(md, results);
        AppendRule(md, results);
        AppendCases(md, dataset, results);

        return md.ToString();
    }

    private static void AppendSystem(StringBuilder md, SystemInfo system)
    {
        md.AppendLine("## Sistema");
        md.AppendLine();
        md.AppendLine("| Voce | Valore |");
        md.AppendLine("|---|---|");

        foreach ((string voce, string valore) in system.Voci)
        {
            md.AppendLine(CultureInfo.InvariantCulture, $"| {voce} | {valore.ReplaceLineEndings(" ")} |");
        }

        md.AppendLine();
    }

    private static void AppendSummary(StringBuilder md, IReadOnlyList<BenchResult> results)
    {
        md.AppendLine("## Riepilogo");
        md.AppendLine();
        md.AppendLine("| Candidato | Percorso | Chip | Runtime | Modello | Dim. | hit@1 | MRR | recall@3 | Margine medio / min | Prima richiesta | Latenza p50 / p95 | Testi/s | Stima 470 testi | Correttezza vs CPU |");
        md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");

        foreach (BenchResult r in results)
        {
            var c = r.Candidate;

            if (r.Errore is not null)
            {
                md.AppendLine(CultureInfo.InvariantCulture, $"| `{c.Id}` | {c.Percorso} | {c.Chip} | {c.Runtime} | {c.Modello} | {c.Dimensioni} | ❌ {r.Errore.ReplaceLineEndings(" ")} ||||||||||");
                continue;
            }

            var q = r.Qualita!;
            var p = r.Prestazioni!;
            string correctness = r.Correttezza is { } k ? $"{F(k.CosenoMedio, "0.0000")} (min {F(k.CosenoMinimo, "0.0000")})" : "—";

            md.AppendLine(CultureInfo.InvariantCulture,
                $"| `{c.Id}` | {c.Percorso} | {c.Chip} | {c.Runtime} | {c.Modello} | {c.Dimensioni} | {F(q.HitAt1)} | {F(q.Mrr, "0.000")} | {F(q.RecallAt3)} | {F(q.MargineMedio, "0.000")} / {F(q.MargineMinimo, "0.000")} | {F(p.PrimaRichiestaMs, "0")} ms | {F(p.LatenzaP50Ms, "0")} / {F(p.LatenzaP95Ms, "0")} ms | {F(p.TestiAlSecondo, "0.0")} | {F(PocTexts / p.TestiAlSecondo, "0")} s | {correctness} |");
        }

        md.AppendLine();
        md.AppendLine("Note:");

        foreach (BenchResult r in results)
        {
            string memory = r.Prestazioni?.MemoriaProcessoMb is { } mb ? $"memoria del processo +{F(mb, "0")} MB" : "";
            string placement = r.Prestazioni?.PosizionamentoOllama is { } placed ? $"`ollama ps`: {placed}" : "";
            string determinism = r.Prestazioni is { } perf ? $"determinismo: distanza massima {perf.DeterminismoDistanzaMassima:E1}" : "";
            string[] parts = [.. new[] { determinism, memory, placement, r.Note ?? "" }.Where(s => s.Length > 0)];

            if (parts.Length > 0)
            {
                md.AppendLine(CultureInfo.InvariantCulture, $"- `{r.Candidate.Id}`: {string.Join("; ", parts)}.");
            }
        }

        md.AppendLine();
    }

    private static void AppendDuplicates(StringBuilder md, IReadOnlyList<BenchResult> results)
    {
        md.AppendLine("## Separazione dei quasi-duplicati (antifrode, Fase 7)");
        md.AppendLine();
        md.AppendLine("| Candidato | Riformulate: media / max | Stesso tema: media / min | Gap (min tema − max riformulate) | Soglia migliore | Accuratezza |");
        md.AppendLine("|---|---|---|---|---|---|");

        foreach (BenchResult r in results.Where(r => r.Duplicati is not null))
        {
            var d = r.Duplicati!;
            md.AppendLine(CultureInfo.InvariantCulture,
                $"| `{r.Candidate.Id}` | {F(d.DuplicateMedia, "0.000")} / {F(d.DuplicateMassima, "0.000")} | {F(d.StessoTemaMedia, "0.000")} / {F(d.StessoTemaMinima, "0.000")} | {F(d.Gap, "0.000")} | {F(d.SogliaMigliore, "0.000")} | {F(d.AccuratezzaSoglia)} |");
        }

        md.AppendLine();
    }

    private static void AppendCoexistence(StringBuilder md, IReadOnlyList<BenchResult> results)
    {
        BenchResult[] measured = [.. results.Where(r => r.Convivenza is not null)];

        if (measured.Length == 0)
        {
            return;
        }

        md.AppendLine("## Convivenza con la chat (`qwen3.5:9b` su GPU)");
        md.AppendLine();
        md.AppendLine("| Candidato | Embedding p50 sotto carico | Chat da sola | Chat con embedding | Chat ricaricata | `ollama ps` dopo |");
        md.AppendLine("|---|---|---|---|---|---|");

        foreach (BenchResult r in measured)
        {
            var c = r.Convivenza!;
            md.AppendLine(CultureInfo.InvariantCulture,
                $"| `{r.Candidate.Id}` | {F(c.EmbeddingP50SottoCaricoMs, "0")} ms | {F(c.ChatTokenAlSecondoDaSola, "0.0")} token/s | {F(c.ChatTokenAlSecondoConEmbedding, "0.0")} token/s | {(c.ChatRicaricata ? "⚠️ sì" : "no")} | {c.PosizionamentoDopo} |");
        }

        md.AppendLine();
    }

    private static void AppendRule(StringBuilder md, IReadOnlyList<BenchResult> results)
    {
        md.AppendLine("## Regola di scelta (fase-1b.md §5)");
        md.AppendLine();

        List<BenchResult> eligible = [];

        foreach (BenchResult r in results)
        {
            string? excluded = ExclusionReason(r);
            md.AppendLine(CultureInfo.InvariantCulture, $"- `{r.Candidate.Id}`: {(excluded is null ? "ammesso" : $"escluso — {excluded}")}");

            if (excluded is null)
            {
                eligible.Add(r);
            }
        }

        md.AppendLine();

        if (eligible.Count == 0)
        {
            md.AppendLine("Nessun candidato ammesso.");
            md.AppendLine();
            return;
        }

        // Qualità prima (MRR, poi margine); a parità sostanziale di MRR si preferisce la NPU, poi la CPU.
        BenchResult best = eligible.OrderByDescending(r => r.Qualita!.Mrr).ThenByDescending(r => r.Qualita!.MargineMedio).First();
        BenchResult? npuTie = eligible
            .Where(r => r.Candidate.Chip == "NPU" && best.Qualita!.Mrr - r.Qualita!.Mrr <= MrrTie)
            .OrderByDescending(r => r.Qualita!.Mrr)
            .FirstOrDefault();
        BenchResult chosen = npuTie ?? best;

        md.AppendLine(CultureInfo.InvariantCulture, $"Migliore per qualità: `{best.Candidate.Id}` (MRR {F(best.Qualita!.Mrr, "0.000")}).");
        md.AppendLine(CultureInfo.InvariantCulture, $"Applicando la preferenza per la NPU entro {F(MrrTie, "0.00")} di MRR: **`{chosen.Candidate.Id}`** — {chosen.Candidate.Modello} su {chosen.Candidate.Chip} ({chosen.Candidate.Runtime}), {chosen.Candidate.Dimensioni} dimensioni.");
        md.AppendLine();
    }

    private static string? ExclusionReason(BenchResult r)
    {
        if (r.Errore is not null)
        {
            return "errore";
        }

        if (r.Candidate.Chip == "GPU")
        {
            return "solo riferimento di velocità: l'embedding non va in VRAM (D18)";
        }

        if (r.Correttezza is { CosenoMedio: < MinNpuCosine } k)
        {
            return $"correttezza {F(k.CosenoMedio, "0.000")} < {MinNpuCosine} rispetto a `{k.Riferimento}`";
        }

        if (r.Convivenza is { ChatRicaricata: true })
        {
            return "la chat è stata ricaricata in VRAM";
        }

        if (r.Prestazioni!.LatenzaP50Ms > MaxLatencyMs)
        {
            return $"latenza p50 {F(r.Prestazioni.LatenzaP50Ms, "0")} ms > {MaxLatencyMs} ms";
        }

        double indexing = PocTexts / r.Prestazioni.TestiAlSecondo;

        return indexing > MaxIndexingSeconds ? $"indicizzazione stimata {F(indexing, "0")} s > 15 minuti" : null;
    }

    private static void AppendCases(StringBuilder md, BenchDataset dataset, IReadOnlyList<BenchResult> results)
    {
        BenchResult[] measured = [.. results.Where(r => r.Qualita is not null)];

        md.AppendLine("## Dettaglio per caso (posizione della prima clausola rilevante; 1 = corretta in cima)");
        md.AppendLine();
        md.AppendLine("| # | Denuncia | Rilevanti | " + string.Join(" | ", measured.Select(r => $"`{r.Candidate.Id}`")) + " |");
        md.AppendLine("|---|---|---|" + string.Concat(measured.Select(_ => "---|")));

        for (int i = 0; i < dataset.Casi.Count; i++)
        {
            string ranks = string.Join(" | ", measured.Select(r =>
            {
                CaseOutcome o = r.Qualita!.Casi[i];

                return o.RankPrimoRilevante == 1 ? "1" : $"**{o.RankPrimoRilevante}** ({o.Top3[0]})";
            }));

            md.AppendLine(CultureInfo.InvariantCulture, $"| {i + 1} | {dataset.Casi[i].Query} | {string.Join(", ", dataset.Casi[i].Rilevanti)} | {ranks} |");
        }

        md.AppendLine();
    }

    private static string F(double value, string format = "0.00") => value.ToString(format, It);
}
