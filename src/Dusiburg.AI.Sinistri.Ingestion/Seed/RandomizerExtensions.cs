using Bogus;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

internal static class RandomizerExtensions
{
    /// <summary>Elemento casuale di una lista in sola lettura (i metodi di Bogus accettano solo <c>IList</c> e array).</summary>
    public static T Scegli<T>(this Randomizer random, IReadOnlyList<T> elementi) =>
        elementi.Count > 0
            ? elementi[random.Number(0, elementi.Count - 1)]
            : throw new InvalidOperationException("Nessun elemento tra cui scegliere.");

    /// <summary>Data casuale tra <paramref name="da"/> e <paramref name="a"/> inclusi.</summary>
    public static DateOnly Data(this Randomizer random, DateOnly da, DateOnly a) => da.AddDays(random.Number(0, a.DayNumber - da.DayNumber));

    /// <summary>Importo casuale arrotondato a 10 €, più denso verso il minimo (molti sinistri piccoli, pochi grandi).</summary>
    public static decimal Importo(this Randomizer random, decimal minimo, decimal massimo)
    {
        double quota = Math.Pow(random.Double(), 2);

        return ArrotondaA10(minimo + ((massimo - minimo) * (decimal)quota));
    }

    public static decimal ArrotondaA10(decimal importo) => Math.Round(importo / 10m, MidpointRounding.AwayFromZero) * 10m;
}
