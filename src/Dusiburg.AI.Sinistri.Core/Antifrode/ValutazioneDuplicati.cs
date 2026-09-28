using Dusiburg.AI.Sinistri.Core.Seed;

namespace Dusiburg.AI.Sinistri.Core.Antifrode;

/// <summary>
/// Precision/recall del fraud-scan rispetto alle coppie attese (fase-7.md §3), senza I/O: le coppie trovate arrivano da una sola query
/// alla soglia più alta e ogni soglia minore si ottiene filtrandole in memoria. Le coppie sono non orientate: (A, B) = (B, A).
/// </summary>
public static class ValutazioneDuplicati
{
    public static readonly IReadOnlyList<double> SoglieStandard = [0.05, 0.08, 0.12, 0.15];

    public const double RecallMinima = 0.8;

    /// <summary>Le soglie standard più quella indicata, se diversa, in ordine crescente.</summary>
    public static IReadOnlyList<double> Soglie(double indicata) => [.. SoglieStandard.Append(indicata).Distinct().Order()];

    public static RigaValutazione Calcola(IEnumerable<CoppiaSospetta> trovate, IReadOnlyCollection<CoppiaDuplicati> attese, double soglia)
    {
        HashSet<(string, string)> chiaviAttese = [.. attese.Select(c => Chiave(c.Originale, c.Duplicato))];
        HashSet<(string, string)> sottoSoglia = [.. trovate.Where(c => c.Distanza < soglia).Select(c => Chiave(c.NumeroA, c.NumeroB))];

        int veriPositivi = sottoSoglia.Count(chiaviAttese.Contains);
        int falsiPositivi = sottoSoglia.Count - veriPositivi;
        int falsiNegativi = chiaviAttese.Count - veriPositivi;
        double precision = sottoSoglia.Count == 0 ? 0 : (double)veriPositivi / sottoSoglia.Count;
        double recall = chiaviAttese.Count == 0 ? 0 : (double)veriPositivi / chiaviAttese.Count;
        double f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);

        return new RigaValutazione(soglia, sottoSoglia.Count, veriPositivi, falsiPositivi, falsiNegativi, precision, recall, f1);
    }

    /// <summary>
    /// La soglia più bassa con recall ≥ <paramref name="recallMinima"/>: alzandola la recall non può che crescere e la precision
    /// scendere, quindi è anche quella con meno falsi positivi. Null se nessuna soglia raggiunge la recall minima.
    /// </summary>
    public static RigaValutazione? SuggerisciSoglia(IEnumerable<RigaValutazione> righe, double recallMinima = RecallMinima) =>
        righe.Where(r => r.Recall >= recallMinima).OrderBy(r => r.Soglia).ThenByDescending(r => r.F1).FirstOrDefault();

    public static bool Attesa(CoppiaSospetta coppia, IReadOnlyCollection<CoppiaDuplicati> attese) =>
        attese.Any(c => Chiave(c.Originale, c.Duplicato) == Chiave(coppia.NumeroA, coppia.NumeroB));

    /// <summary>Minimo, mediana e massimo delle distanze note; null se non ce n'è nessuna.</summary>
    public static (double Min, double Mediana, double Max)? Distribuzione(IEnumerable<double> distanze)
    {
        double[] ordinate = [.. distanze.Order()];

        if (ordinate.Length == 0)
        {
            return null;
        }

        int meta = ordinate.Length / 2;
        double mediana = ordinate.Length % 2 == 1 ? ordinate[meta] : (ordinate[meta - 1] + ordinate[meta]) / 2;

        return (ordinate[0], mediana, ordinate[^1]);
    }

    private static (string, string) Chiave(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
}
