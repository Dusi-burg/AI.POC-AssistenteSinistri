using System.Text.Json;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Dataset;

/// <summary>Contenuto di <c>data/embedding_bench.json</c> (fase-1b.md §4).</summary>
internal sealed record BenchDataset(
    IReadOnlyList<BenchDocument> Documenti,
    IReadOnlyList<BenchCase> Casi,
    IReadOnlyList<BenchPair> CoppieDuplicate,
    IReadOnlyList<BenchPair> CoppieStessoTema)
{
    public static BenchDataset Load(string path)
    {
        BenchDataset dataset = JsonSerializer.Deserialize<BenchDataset>(File.ReadAllText(path), JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"{path} è vuoto.");

        HashSet<string> ids = [.. dataset.Documenti.Select(d => d.Id)];
        string[] unknown = [.. dataset.Casi.SelectMany(c => c.Rilevanti.Concat(c.Distrattori)).Where(id => !ids.Contains(id)).Distinct()];

        return unknown.Length == 0
            ? dataset
            : throw new InvalidOperationException($"{path}: casi che citano documenti inesistenti: {string.Join(", ", unknown)}.");
    }

    /// <summary>Tutti i testi del banco, per throughput, determinismo e confronto tra percorsi.</summary>
    public IEnumerable<string> AllTexts() =>
        Documenti.Select(d => d.Testo)
            .Concat(Casi.Select(c => c.Query))
            .Concat(CoppieDuplicate.Concat(CoppieStessoTema).SelectMany(p => new[] { p.A, p.B }))
            .Distinct();
}

internal sealed record BenchDocument(string Id, string Testo);

internal sealed record BenchCase(string Query, IReadOnlyList<string> Rilevanti, IReadOnlyList<string> Distrattori);

internal sealed record BenchPair(string A, string B);
