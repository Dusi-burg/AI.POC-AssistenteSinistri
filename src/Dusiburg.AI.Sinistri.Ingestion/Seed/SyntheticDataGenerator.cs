using System.Globalization;
using Bogus;
using Dusiburg.AI.Sinistri.Core.Seed;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

/// <summary>
/// Orchestratore dei dati sintetici (fase-3.md): anagrafiche, sinistri, quasi-duplicati. A parità di seed e di data di riferimento
/// il risultato è identico; le date sono relative a <c>oggi</c>, così "ultimi 5 anni" e "ultimi 12 mesi" restano sensati nel tempo.
/// </summary>
public static class SyntheticDataGenerator
{
    public const int DefaultRandomSeed = 20260924;

    public static DatiSintetici Genera(int randomSeed, DateOnly oggi)
    {
        var faker = new Faker("it") { Random = new Randomizer(randomSeed) };

        Anagrafiche anagrafiche = new AnagraficheGenerator(faker, oggi).Genera();

        var sinistriGenerator = new SinistriGenerator(faker.Random, oggi, anagrafiche);
        List<SinistroSintetico> sinistri = sinistriGenerator.Genera();

        IReadOnlyList<CoppiaProvvisoria> coppie = new QuasiDuplicatiGenerator(faker.Random, oggi, anagrafiche)
            .Genera(sinistri, sinistriGenerator.IdCoperturaDemo);

        (IReadOnlyList<SinistroSintetico> numerati, IReadOnlyDictionary<int, string> numeri) = Numera(sinistri);

        return new DatiSintetici(
            oggi,
            randomSeed,
            anagrafiche.Contraenti,
            anagrafiche.Riparatori,
            anagrafiche.Polizze,
            numerati,
            [.. coppie.Select(c => new CoppiaDuplicati(numeri[c.OriginaleId], numeri[c.DuplicatoId], c.Tipo))]);
    }

    /// <summary>Id definitivi in ordine di denuncia e <c>Numero</c> = <c>SIN-{anno denuncia}-{progressivo:D6}</c>, progressivo per anno.</summary>
    private static (IReadOnlyList<SinistroSintetico> Sinistri, IReadOnlyDictionary<int, string> NumeriPerIdProvvisorio) Numera(List<SinistroSintetico> sinistri)
    {
        List<SinistroSintetico> numerati = [];
        Dictionary<int, string> numeri = [];
        Dictionary<int, int> progressivi = [];

        foreach (SinistroSintetico sinistro in sinistri.OrderBy(s => s.DataDenuncia).ThenBy(s => s.Id))
        {
            int anno = sinistro.DataDenuncia.Year;
            int progressivo = progressivi[anno] = progressivi.GetValueOrDefault(anno) + 1;
            string numero = string.Create(CultureInfo.InvariantCulture, $"SIN-{anno}-{progressivo:D6}");

            numeri[sinistro.Id] = numero;
            numerati.Add(sinistro with { Id = numerati.Count + 1, Numero = numero });
        }

        return (numerati, numeri);
    }
}
