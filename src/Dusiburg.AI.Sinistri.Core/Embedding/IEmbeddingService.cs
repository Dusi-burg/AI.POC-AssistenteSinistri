namespace Dusiburg.AI.Sinistri.Core.Embedding;

/// <summary>
/// Embedding con il prefisso giusto (fase-4.md §1): la distinzione query/documento è nell'interfaccia, non lasciata al chiamante.
/// I vettori restituiti hanno sempre <c>EMBEDDING_DIMENSIONS</c> elementi.
/// </summary>
public interface IEmbeddingService
{
    EmbeddingProfile Profile { get; }

    /// <summary>Testo da cercare (denuncia, ricerca): prefisso "query" del profilo.</summary>
    Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken);

    /// <summary>Testi da indicizzare (clausole, sinistri) o da confrontare in modo simmetrico (antifrode): prefisso "documento".</summary>
    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
