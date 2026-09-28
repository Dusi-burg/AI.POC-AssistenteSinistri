using Dusiburg.AI.Sinistri.EmbeddingBench.Dataset;

namespace Dusiburg.AI.Sinistri.EmbeddingBench.Measurement;

/// <summary>Metriche di qualità calcolate sui vettori già ottenuti: nessuna chiamata al modello.</summary>
internal static class QualityMetrics
{
    public static RetrievalQuality Retrieval(
        BenchDataset dataset, IReadOnlyDictionary<string, float[]> documentVectors, IReadOnlyList<float[]> queryVectors)
    {
        List<CaseOutcome> outcomes = [];
        double hits = 0, reciprocalRanks = 0, recall = 0;

        for (int i = 0; i < dataset.Casi.Count; i++)
        {
            BenchCase caso = dataset.Casi[i];
            (string Id, double Distance)[] ranking =
            [
                .. dataset.Documenti
                    .Select(d => (d.Id, VectorMath.CosineDistance(queryVectors[i], documentVectors[d.Id])))
                    .OrderBy(r => r.Item2)
            ];

            int firstRelevant = Array.FindIndex(ranking, r => caso.Rilevanti.Contains(r.Id)) + 1;
            string[] top3 = [.. ranking.Take(3).Select(r => r.Id)];
            double nearestRelevant = ranking.Where(r => caso.Rilevanti.Contains(r.Id)).Min(r => r.Distance);
            double nearestDistractor = ranking.Where(r => caso.Distrattori.Contains(r.Id)).Min(r => r.Distance);

            hits += firstRelevant == 1 ? 1 : 0;
            reciprocalRanks += 1.0 / firstRelevant;
            recall += (double)top3.Count(caso.Rilevanti.Contains) / caso.Rilevanti.Count;
            outcomes.Add(new CaseOutcome(caso.Query, top3, firstRelevant, nearestDistractor - nearestRelevant));
        }

        int count = dataset.Casi.Count;

        return new RetrievalQuality(
            hits / count, reciprocalRanks / count, recall / count,
            outcomes.Average(o => o.Margine), outcomes.Min(o => o.Margine), outcomes);
    }

    public static DuplicateSeparation Duplicates(IReadOnlyList<double> duplicateDistances, IReadOnlyList<double> sameThemeDistances)
    {
        // Soglia migliore: si provano tutti i punti medi tra distanze consecutive e si tiene quella con più coppie classificate bene.
        double[] candidates = [.. duplicateDistances.Concat(sameThemeDistances).Order()];
        (double Threshold, double Accuracy) best = (0, 0);

        for (int i = 0; i < candidates.Length - 1; i++)
        {
            double threshold = (candidates[i] + candidates[i + 1]) / 2;
            int correct = duplicateDistances.Count(d => d < threshold) + sameThemeDistances.Count(d => d >= threshold);
            double accuracy = (double)correct / candidates.Length;

            if (accuracy > best.Accuracy)
            {
                best = (threshold, accuracy);
            }
        }

        return new DuplicateSeparation(
            duplicateDistances.Average(), duplicateDistances.Max(),
            sameThemeDistances.Average(), sameThemeDistances.Min(),
            best.Threshold, best.Accuracy);
    }
}
