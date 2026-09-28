using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Core.Embedding;

/// <summary>
/// Modello e runtime con cui sono stati calcolati i vettori salvati (tabella <c>EmbeddingInfo</c>). In Core perché arriva anche alla
/// UI, nella pagina di stato (Fase 8).
/// </summary>
public sealed record EmbeddingInfo(string Modello, EmbeddingProvider Provider, int Dimensioni, DateTime AggiornatoIl);
