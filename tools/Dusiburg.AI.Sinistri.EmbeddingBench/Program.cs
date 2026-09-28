using Dusiburg.AI.Sinistri.EmbeddingBench;
using Dusiburg.AI.Sinistri.EmbeddingBench.Candidates;
using Dusiburg.AI.Sinistri.EmbeddingBench.Dataset;
using Dusiburg.AI.Sinistri.EmbeddingBench.Measurement;
using Dusiburg.AI.Sinistri.EmbeddingBench.Report;

// Uso: dotnet run --project tools/Dusiburg.AI.Sinistri.EmbeddingBench [-- --candidates p1-bge-m3,p3a-bge-m3-npu] [--repeat 50] [--skip-coexistence] [--report <file.md>]
// Prerequisiti per percorso: Ollama (P0, P1), "flm serve qwen3:0.6b --embed 1" (P2), modello ed EP VitisAI locali (P3a): vedi docs/fase-1b.md.
BenchArguments arguments = BenchArguments.Parse(args);
string repositoryRoot = BenchArguments.FindRepositoryRoot();
BenchDataset dataset = BenchDataset.Load(Path.Combine(repositoryRoot, "data", "embedding_bench.json"));
string reportDirectory = Path.Combine(repositoryRoot, "eval");
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
CancellationToken cancellationToken = cancellation.Token;

using var ollama = new OllamaAdmin();
SystemInfo system = await SystemInfo.CollectAsync(ollama, cancellationToken);
IReadOnlyList<BenchCandidate> candidates = await SelectCandidatesAsync(arguments, ollama, cancellationToken);

var runner = new BenchRunner(dataset, ollama, arguments.Repeat, Console.Out);
List<BenchResult> results = [];

foreach (BenchCandidate candidate in candidates)
{
    Console.WriteLine($"== {candidate.Id} ({candidate.Modello}, {candidate.Chip}, {candidate.Runtime})");
    results.Add(await runner.RunAsync(candidate, cancellationToken));
}

foreach (BenchResult result in results.Where(r => r.Errore is null && r.Candidate.Riferimento is not null))
{
    if (results.FirstOrDefault(r => r.Candidate.Id == result.Candidate.Riferimento && r.Errore is null) is { } reference)
    {
        result.Correttezza = CorrectnessCheck.Compare(result, reference);
    }
}

if (!arguments.SkipCoexistence)
{
    var coexistence = new CoexistenceProbe(dataset, ollama, Console.Out);
    double baseline = await coexistence.ChatBaselineAsync(cancellationToken);

    foreach (BenchResult result in results.Where(r => r.Errore is null))
    {
        Console.WriteLine($"== convivenza {result.Candidate.Id}");
        result.Convivenza = await coexistence.RunAsync(result.Candidate, baseline, cancellationToken);
    }
}

DateTimeOffset now = DateTimeOffset.Now;
Directory.CreateDirectory(reportDirectory);
string reportPath = arguments.ReportPath ?? Path.Combine(reportDirectory, $"embedding-bench_{now:yyyy-MM-dd}.md");
await File.WriteAllTextAsync(reportPath, MarkdownReport.Build(system, dataset, results, now), cancellationToken);
Console.WriteLine($"Report: {reportPath}");

return results.Any(r => r.Errore is null) ? 0 : 1;

static async Task<IReadOnlyList<BenchCandidate>> SelectCandidatesAsync(BenchArguments arguments, OllamaAdmin ollama, CancellationToken cancellationToken)
{
    List<BenchCandidate> selected = [];

    foreach (BenchCandidate candidate in BenchCandidates.All().Where(c => arguments.Candidates is null || arguments.Candidates.Contains(c.Id)))
    {
        // Un modello Ollama non installato si salta con un avviso: i pull li decide l'utente.
        if (candidate.OllamaModel is { } model && !await ollama.IsInstalledAsync(model, cancellationToken))
        {
            Console.WriteLine($"-- {candidate.Id}: '{model}' non installato in Ollama, saltato");
            continue;
        }

        selected.Add(candidate);
    }

    return selected;
}
