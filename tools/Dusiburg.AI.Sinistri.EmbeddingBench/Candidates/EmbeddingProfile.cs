namespace Dusiburg.AI.Sinistri.EmbeddingBench.Candidates;

/// <summary>
/// Prefissi richiesti dal modello (fase-1b.md §6): senza, i modelli instruction-aware perdono qualità in modo evidente.
/// Nel banco vive qui; se il modello scelto li richiede, in Fase 4 diventa <c>EmbeddingProfile</c> di Core.
/// </summary>
internal sealed record EmbeddingProfile(string Nome, string QueryPrefix, string DocumentPrefix, string SimilarityPrefix)
{
    public static readonly EmbeddingProfile None = new("nessun prefisso", "", "", "");

    /// <summary>Prompt ufficiali di EmbeddingGemma (sentence-transformers): ricerca e somiglianza tra frasi.</summary>
    public static readonly EmbeddingProfile EmbeddingGemma = new(
        "EmbeddingGemma",
        "task: search result | query: ",
        "title: none | text: ",
        "task: sentence similarity | query: ");

    public static EmbeddingProfile ForModel(string model) =>
        model.Contains("gemma", StringComparison.OrdinalIgnoreCase) ? EmbeddingGemma : None;

    public string Query(string text) => QueryPrefix + text;

    public string Document(string text) => DocumentPrefix + text;

    public string Similarity(string text) => SimilarityPrefix + text;
}
