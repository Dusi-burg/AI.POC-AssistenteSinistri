namespace Dusiburg.AI.Sinistri.Core.Embedding;

/// <summary>
/// Prefissi e dimensione nativa di un modello di embedding (fase-4.md §1). I modelli instruction-aware vogliono prefissi diversi
/// per query e documento: senza, la qualità cala in modo evidente (banco di prova della Fase 1b). Un modello non in tabella è un
/// errore: non si usa un modello instruction-aware "senza profilo".
/// </summary>
public sealed record EmbeddingProfile(string Modello, string QueryPrefix, string DocumentPrefix, int DimensioneNativa)
{
    /// <summary>Istruzione per i modelli "Instruct/Query" (Qwen3-Embedding, e5-instruct): da tarare nel banco se si passa a uno di loro.</summary>
    private const string Instruct =
        "Instruct: Dato il testo di una denuncia di sinistro, trova le clausole di polizza e i sinistri pertinenti\nQuery: ";

    /// <summary>Profili noti: la chiave è il nome del modello senza tag (<c>embeddinggemma:latest</c> → <c>embeddinggemma</c>).</summary>
    private static readonly IReadOnlyList<(string Nome, string? Tag, EmbeddingProfile Profilo)> Noti =
    [
        ("embeddinggemma", null, new("embeddinggemma", "task: search result | query: ", "title: none | text: ", 768)),
        ("bge-m3", null, new("bge-m3", "", "", 1024)),
        ("qwen3-embedding", "4b", new("qwen3-embedding:4b", Instruct, "", 2560)),
        ("qwen3-embedding", "8b", new("qwen3-embedding:8b", Instruct, "", 4096)),
        ("qwen3-embedding", null, new("qwen3-embedding:0.6b", Instruct, "", 1024)),
        ("multilingual-e5-large-instruct", null, new("multilingual-e5-large-instruct", Instruct, "", 1024)),
    ];

    public static EmbeddingProfile ForModel(string model)
    {
        string[] parti = model.Trim().Split(':', 2);
        string nome = parti[0].Split('/').Last();
        string? tag = parti.Length > 1 ? parti[1] : null;

        return Noti
            .Where(n => nome.Equals(n.Nome, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(n => n.Tag is null || string.Equals(n.Tag, tag, StringComparison.OrdinalIgnoreCase))
            .Profilo
            ?? throw new InvalidOperationException(
                $"Modello di embedding '{model}' senza profilo (prefissi e dimensione): modelli supportati " +
                $"{string.Join(", ", Noti.Select(n => n.Profilo.Modello).Distinct())}. Aggiungerlo a EmbeddingProfile.");
    }

    public string Query(string testo) => QueryPrefix + testo;

    public string Document(string testo) => DocumentPrefix + testo;
}
