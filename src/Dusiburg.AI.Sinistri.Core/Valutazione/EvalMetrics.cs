namespace Dusiburg.AI.Sinistri.Core.Valutazione;

/// <summary>
/// Metriche del retrieval (fase-9.md §2), pure. Gli articoli si confrontano per stringa esatta ("Art. 2.4"): il golden set usa la
/// forma canonica del DB, verificata dal test sul golden set.
/// </summary>
public static class EvalMetrics
{
    /// <summary>|rilevanti ∩ prime k| / |rilevanti|; 0 se non ci sono rilevanti.</summary>
    public static double RecallAtK(IReadOnlyCollection<string> rilevanti, IReadOnlyList<string> ordinati, int k)
    {
        if (rilevanti.Count == 0)
        {
            return 0;
        }

        HashSet<string> primi = [.. ordinati.Take(k)];

        return (double)rilevanti.Count(primi.Contains) / rilevanti.Count;
    }

    /// <summary>Posizione (da 1) del primo articolo rilevante entro le prime <paramref name="maxRank"/>; null se nessuno.</summary>
    public static int? RankPrimoRilevante(IReadOnlyCollection<string> rilevanti, IReadOnlyList<string> ordinati, int maxRank = 10)
    {
        for (int i = 0; i < Math.Min(maxRank, ordinati.Count); i++)
        {
            if (rilevanti.Contains(ordinati[i]))
            {
                return i + 1;
            }
        }

        return null;
    }

    /// <summary>1 / rank del primo rilevante, 0 se nessuno nelle prime <paramref name="maxRank"/>.</summary>
    public static double ReciprocalRank(IReadOnlyCollection<string> rilevanti, IReadOnlyList<string> ordinati, int maxRank = 10) =>
        RankPrimoRilevante(rilevanti, ordinati, maxRank) is { } rank ? 1.0 / rank : 0;

    /// <summary>Quota di <paramref name="attese"/> presenti nel risultato; null se il caso non ne ha (esclusa dalla media).</summary>
    public static double? Copertura(IReadOnlyCollection<string> attese, IReadOnlyCollection<string> trovati) =>
        attese.Count == 0 ? null : (double)attese.Count(trovati.Contains) / attese.Count;
}
