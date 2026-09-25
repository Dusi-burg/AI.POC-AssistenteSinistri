using Dusiburg.AI.Sinistri.EmbeddingBench.Candidates;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Measurement;

/// <summary>Esito di un caso di retrieval: dove sta la prima clausola rilevante e quanto è lontano il distrattore.</summary>
internal sealed record CaseOutcome(string Query, IReadOnlyList<string> Top3, int RankPrimoRilevante, double Margine);

internal sealed record RetrievalQuality(double HitAt1, double Mrr, double RecallAt3, double MargineMedio, double MargineMinimo, IReadOnlyList<CaseOutcome> Casi);

/// <summary>Distanze delle coppie riformulate contro quelle "stesso tema": serve a capire se la soglia antifrode è praticabile.</summary>
internal sealed record DuplicateSeparation(
    double DuplicateMedia,
    double DuplicateMassima,
    double StessoTemaMedia,
    double StessoTemaMinima,
    double SogliaMigliore,
    double AccuratezzaSoglia)
{
    /// <summary>Positivo: esiste una soglia che separa perfettamente le due popolazioni.</summary>
    public double Gap => StessoTemaMinima - DuplicateMassima;
}

internal sealed record Performance(
    double PrimaRichiestaMs,
    double LatenzaP50Ms,
    double LatenzaP95Ms,
    double TestiAlSecondo,
    double DeterminismoDistanzaMassima,
    double? MemoriaProcessoMb,
    string? PosizionamentoOllama);

/// <summary>Stesso modello su due percorsi (NPU contro CPU): similarità coseno tra i vettori degli stessi testi.</summary>
internal sealed record Correctness(string Riferimento, double CosenoMedio, double CosenoMinimo, int Testi);

internal sealed record Coexistence(
    double EmbeddingP50SottoCaricoMs,
    double ChatTokenAlSecondoDaSola,
    double ChatTokenAlSecondoConEmbedding,
    bool ChatRicaricata,
    string PosizionamentoDopo);

internal sealed record BenchResult(BenchCandidate Candidate)
{
    public string? Errore { get; init; }

    public RetrievalQuality? Qualita { get; init; }

    public DuplicateSeparation? Duplicati { get; init; }

    public Performance? Prestazioni { get; init; }

    public Correctness? Correttezza { get; set; }

    public Coexistence? Convivenza { get; set; }

    public string? Note { get; init; }

    /// <summary>Vettori dei testi del banco con il prefisso "documento": servono al confronto tra percorsi.</summary>
    public IReadOnlyDictionary<string, float[]> Vettori { get; init; } = new Dictionary<string, float[]>();
}
