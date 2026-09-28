namespace Dusiburg.AI.Sinistri.Core.Embedding;

/// <summary>Colonne vettoriali da calcolare: valori di <c>embed --solo</c>.</summary>
public enum TabellaEmbedding
{
    /// <summary><c>Clausola.Embedding</c>.</summary>
    Clausole = 1,

    /// <summary><c>Sinistro.Embedding</c>: causa, descrizione ed esito di perizia (ricerca nello storico e fraud-scan).</summary>
    Sinistri = 2,

    /// <summary><c>Sinistro.EmbeddingAntifrode</c>: la sola descrizione, confrontata con la nuova denuncia (fase-7.md §2 bis).</summary>
    SinistriAntifrode = 3,
}
