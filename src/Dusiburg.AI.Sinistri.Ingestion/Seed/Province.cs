using Bogus;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

/// <summary>Province di Lombardia, Veneto ed Emilia-Romagna, con peso maggiore alle città più popolose (filtri per provincia della demo).</summary>
internal static class Province
{
    private static readonly IReadOnlyList<string[]> Regioni =
    [
        ["MI", "BG", "BS", "MB", "VA", "CO", "MN", "PV", "CR", "LC", "LO", "SO"],
        ["VE", "VR", "PD", "VI", "TV", "RO", "BL"],
        ["BO", "MO", "RE", "PR", "FE", "RA", "FC", "RN", "PC"],
    ];

    private static readonly IReadOnlyDictionary<string, float> Pesi = new Dictionary<string, float>
    {
        ["MI"] = 6f, ["BG"] = 3f, ["BS"] = 3f, ["VR"] = 3f, ["PD"] = 3f, ["BO"] = 3f,
    };

    private static readonly string[] Tutte = [.. Regioni.SelectMany(r => r)];

    /// <summary>Normalizzati: <c>WeightedRandom</c> di Bogus richiede pesi che sommano a 1, altrimenti restituisce quasi sempre il primo.</summary>
    private static readonly float[] PesiTutte = Normalizza([.. Tutte.Select(p => Pesi.GetValueOrDefault(p, 1f))]);

    public static string Pesata(Randomizer random) => random.WeightedRandom(Tutte, PesiTutte);

    private static float[] Normalizza(float[] pesi)
    {
        float somma = pesi.Sum();

        return [.. pesi.Select(p => p / somma)];
    }

    /// <summary>Un'altra provincia della stessa regione.</summary>
    public static string Vicina(string provincia, Randomizer random) =>
        random.ArrayElement(Regioni.Single(r => r.Contains(provincia)).Where(p => p != provincia).ToArray());
}
