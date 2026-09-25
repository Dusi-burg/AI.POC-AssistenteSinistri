namespace Dusiburg.AI.Sinistri.EmbeddingBench;

internal static class VectorMath
{
    /// <summary>Distanza coseno come <c>VECTOR_DISTANCE('cosine', …)</c> di SQL Server: 1 − similarità.</summary>
    public static double CosineDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b) => 1 - CosineSimilarity(a, b);

    public static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException($"Vettori di dimensione diversa: {a.Length} e {b.Length}.");
        }

        double dot = 0, normA = 0, normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public static float[] Normalize(ReadOnlySpan<float> vector)
    {
        double norm = 0;

        foreach (float value in vector)
        {
            norm += value * value;
        }

        float scale = (float)(1 / Math.Sqrt(norm));
        float[] result = new float[vector.Length];

        for (int i = 0; i < vector.Length; i++)
        {
            result[i] = vector[i] * scale;
        }

        return result;
    }

    public static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        double[] sorted = [.. values.Order()];
        double rank = percentile / 100 * (sorted.Length - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);

        return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
    }
}
