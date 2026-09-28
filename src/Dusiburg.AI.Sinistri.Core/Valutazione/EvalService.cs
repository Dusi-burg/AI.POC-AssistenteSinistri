using System.Diagnostics;
using Dusiburg.AI.Sinistri.Core.Embedding;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Microsoft.Extensions.Options;

namespace Dusiburg.AI.Sinistri.Core.Valutazione;

/// <param name="Pura">Ricerca vettoriale pura, prime <see cref="EvalService.MaxRank"/> (misura il modello di embedding).</param>
/// <param name="Completa">Ricerca della Fase 5 con le clausole integrative e la franchigia di base (quello che vede il modello).</param>
/// <param name="Mancanti">Rilevanti fuori dalle prime k della ricerca pura.</param>
public sealed record RisultatoCaso(
    CasoGolden Caso,
    IReadOnlyList<ClausolaTrovata> Pura,
    IReadOnlyList<ClausolaTrovata> Completa,
    double RecallAtK,
    int? RankPrimoRilevante,
    double ReciprocalRank,
    IReadOnlyList<string> Mancanti,
    double? RecallEsclusioni);

public sealed record RisultatoEval(
    DateTimeOffset Eseguita,
    string EmbeddingModel,
    string EmbeddingProvider,
    int EmbeddingDimensions,
    string ChatModel,
    string? Database,
    int K,
    RetrievalOptions Retrieval,
    IReadOnlyList<RisultatoCaso> Casi,
    TimeSpan Durata)
{
    public double RecallMedia => Casi.Count == 0 ? 0 : Casi.Average(c => c.RecallAtK);

    public double Mrr => Casi.Count == 0 ? 0 : Casi.Average(c => c.ReciprocalRank);

    public double Hit1 => Casi.Count == 0 ? 0 : (double)Casi.Count(c => c.RankPrimoRilevante == 1) / Casi.Count;

    /// <summary>Media sui soli casi con esclusioni da non perdere; null se nessun caso ne ha.</summary>
    public double? RecallEsclusioniMedia
    {
        get
        {
            double[] valori = [.. Casi.Where(c => c.RecallEsclusioni is not null).Select(c => c.RecallEsclusioni!.Value)];

            return valori.Length == 0 ? null : valori.Average();
        }
    }
}

/// <summary>
/// Valutazione del retrieval delle clausole sul golden set (fase-9.md §2–3). Per ogni caso un solo embedding della denuncia (come
/// query, senza causa) e due ricerche: quella pura per recall@k, MRR e hit@1, quella completa per la recall delle esclusioni.
/// </summary>
public sealed class EvalService(
    IEmbeddingService embeddingService,
    IClausolaRepository clausole,
    SinistriOptions opzioni,
    IOptions<RetrievalOptions> retrieval,
    TimeProvider timeProvider)
{
    /// <summary>Profondità della ricerca pura: il rank del primo rilevante si cerca nelle prime 10 (MRR 0 oltre).</summary>
    public const int MaxRank = 10;

    public const int KDefault = 5;

    /// <summary>Soglia integrativa negativa: nessuna clausola la soddisfa, quindi niente esclusioni e franchigie aggiunte.</summary>
    private const double NessunaIntegrativa = -1;

    public async Task<RisultatoEval> ValutaAsync(
        GoldenSet goldenSet, int k, string? database, IProgress<string>? avanzamento, CancellationToken cancellationToken)
    {
        var cronometro = Stopwatch.StartNew();
        RetrievalOptions parametri = retrieval.Value;
        List<RisultatoCaso> risultati = [];

        foreach (CasoGolden caso in goldenSet.Casi)
        {
            avanzamento?.Report(caso.Id);
            float[] vettore = await embeddingService.EmbedQueryAsync(EmbeddingTextBuilder.Denuncia(caso.Denuncia, causa: null), cancellationToken);

            IReadOnlyList<ClausolaTrovata> pura = [.. (await clausole.CercaPertinentiAsync(
                vettore, caso.Prodotto, Math.Max(k, MaxRank), NessunaIntegrativa, articoloFranchigiaBase: null, cancellationToken))
                .Where(c => !c.Integrativa)];
            IReadOnlyList<ClausolaTrovata> completa = await clausole.CercaPertinentiAsync(
                vettore, caso.Prodotto, parametri.TopClausole, parametri.DistanzaMaxClausolaIntegrativa, parametri.ArticoloFranchigiaBase,
                cancellationToken);

            risultati.Add(Valuta(caso, pura, completa, k));
        }

        return new RisultatoEval(
            timeProvider.GetLocalNow(), opzioni.EmbeddingModel, opzioni.EmbeddingProvider, opzioni.EmbeddingDimensions, opzioni.OllamaChatModel,
            database, k, parametri, risultati, cronometro.Elapsed);
    }

    internal static RisultatoCaso Valuta(CasoGolden caso, IReadOnlyList<ClausolaTrovata> pura, IReadOnlyList<ClausolaTrovata> completa, int k)
    {
        IReadOnlyList<string> rilevanti = caso.Attese.Rilevanti;
        string[] ordinati = [.. pura.Select(c => c.Articolo)];
        HashSet<string> primiK = [.. ordinati.Take(k)];

        return new RisultatoCaso(
            caso,
            pura,
            completa,
            EvalMetrics.RecallAtK(rilevanti, ordinati, k),
            EvalMetrics.RankPrimoRilevante(rilevanti, ordinati, MaxRank),
            EvalMetrics.ReciprocalRank(rilevanti, ordinati, MaxRank),
            [.. rilevanti.Where(a => !primiK.Contains(a))],
            EvalMetrics.Copertura(caso.Attese.EsclusioniDaNonPerdere, [.. completa.Select(c => c.Articolo)]));
    }
}
