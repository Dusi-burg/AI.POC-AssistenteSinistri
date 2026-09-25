using System.Globalization;
using Bogus;
using Dusiburg.AI.Sinistri.Core.Demo;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Seed;

namespace Dusiburg.AI.Sinistri.Ingestion.Seed;

internal sealed record Anagrafiche(
    IReadOnlyList<ContraenteSintetico> Contraenti, IReadOnlyList<RiparatoreSintetico> Riparatori, IReadOnlyList<PolizzaSintetica> Polizze);

/// <summary>Contraenti, riparatori e polizze (fase-3.md §3b): prima le polizze demo del <see cref="DemoCatalog"/>, poi la parte casuale.</summary>
internal sealed class AnagraficheGenerator(Faker faker, DateOnly oggi)
{
    /// <summary>Compresi i 3 contraenti delle polizze demo.</summary>
    public const int Contraenti = 150;

    public const int Riparatori = 12;

    /// <summary>Oltre alle polizze demo.</summary>
    public const int PolizzeCasuali = 200;

    public const int PolizzeRcCasuali = 60;

    private const float QuotaStudiTecnici = 0.20f;
    private const float QuotaInVigore = 0.80f;
    private const int AnniDecorrenza = 6;

    private static readonly decimal[] FranchigieCasa = [150m, 250m, 500m];
    private static readonly decimal[] MassimaliRc = [250_000m, 500_000m, 1_000_000m, 2_000_000m];
    private static readonly decimal[] FranchigieRc = [1_000m, 2_500m, 5_000m];
    private static readonly string[] AttivitaRiparatori = ["Idrotermica", "Termoidraulica", "Elettroimpianti", "Serramenti", "Edilizia", "Vetreria", "Impresa Edile", "Coperture"];
    private static readonly string[] FormeSocietarie = ["Snc", "S.r.l.", "& C. Sas", "S.r.l.s."];

    public Anagrafiche Genera()
    {
        List<ContraenteSintetico> contraenti = [];
        List<PolizzaSintetica> polizze = [];

        foreach (DemoPolizza demo in DemoCatalog.Polizze)
        {
            var contraente = new ContraenteSintetico(contraenti.Count + 1, demo.Contraente, demo.Provincia, demo.Prodotto == Prodotto.RcProfTecnici);
            contraenti.Add(contraente);
            polizze.Add(new PolizzaSintetica(polizze.Count + 1, demo.Numero, demo.Prodotto, contraente.Id,
                demo.Decorrenza(oggi), demo.Scadenza(oggi), demo.Massimale, demo.Franchigia));
        }

        List<ContraenteSintetico> casuali = [];

        while (contraenti.Count < Contraenti)
        {
            ContraenteSintetico contraente = NuovoContraente(contraenti.Count + 1);
            contraenti.Add(contraente);
            casuali.Add(contraente);
        }

        polizze.AddRange(PolizzePerContraenti(casuali, primoId: polizze.Count + 1));

        RiparatoreSintetico[] riparatori = [.. Enumerable.Range(1, Riparatori).Select(id => new RiparatoreSintetico(id, NuovoRiparatore()))];

        return new Anagrafiche(contraenti, riparatori, polizze);
    }

    private ContraenteSintetico NuovoContraente(int id)
    {
        bool studio = faker.Random.Bool(QuotaStudiTecnici);
        string nominativo = studio ? NomeStudioTecnico() : $"{faker.Name.FirstName()} {faker.Name.LastName()}";

        return new ContraenteSintetico(id, nominativo, Province.Pesata(faker.Random), studio);
    }

    private string NomeStudioTecnico() => faker.Random.Number(0, 3) switch
    {
        0 => $"Studio Tecnico Ing. {faker.Name.LastName()}",
        1 => $"Studio Associato {faker.Name.LastName()} e {faker.Name.LastName()}",
        2 => $"{faker.Name.LastName()} Ingegneria S.r.l.",
        _ => $"Studio di Architettura {faker.Name.LastName()}",
    };

    private string NuovoRiparatore() =>
        $"{faker.Random.ArrayElement(AttivitaRiparatori)} {faker.Name.LastName()} {faker.Random.ArrayElement(FormeSocietarie)}";

    /// <summary>
    /// Una polizza a testa, più una seconda per una parte dei contraenti fino a <see cref="PolizzeCasuali"/>. Le polizze RC vanno
    /// prima agli studi tecnici, poi a contraenti casuali. Numerate per prodotto in ordine di decorrenza.
    /// </summary>
    private IEnumerable<PolizzaSintetica> PolizzePerContraenti(List<ContraenteSintetico> contraenti, int primoId)
    {
        List<ContraenteSintetico> titolari = [.. contraenti, .. faker.Random.ListItems(contraenti, PolizzeCasuali - contraenti.Count)];
        List<ContraenteSintetico> ordinati = [.. titolari.Where(c => c.StudioTecnico), .. faker.Random.Shuffle(titolari.Where(c => !c.StudioTecnico))];

        var bozze = ordinati
            .Select((contraente, indice) =>
            {
                Prodotto prodotto = indice < PolizzeRcCasuali ? Prodotto.RcProfTecnici : Prodotto.CasaFabbricati;
                (DateOnly decorrenza, DateOnly scadenza) = Validita();

                return (Contraente: contraente, Prodotto: prodotto, Decorrenza: decorrenza, Scadenza: scadenza,
                    Massimale: Massimale(prodotto), Franchigia: Franchigia(prodotto));
            })
            .OrderBy(b => b.Decorrenza)
            .ToList();

        Dictionary<Prodotto, int> progressivi = [];

        for (int i = 0; i < bozze.Count; i++)
        {
            var bozza = bozze[i];
            int progressivo = progressivi[bozza.Prodotto] = progressivi.GetValueOrDefault(bozza.Prodotto) + 1;
            string prefisso = bozza.Prodotto == Prodotto.CasaFabbricati ? "CF" : "RP";
            string numero = string.Create(CultureInfo.InvariantCulture, $"{prefisso}-{bozza.Decorrenza.Year}-{progressivo:D6}");

            yield return new PolizzaSintetica(primoId + i, numero, bozza.Prodotto, bozza.Contraente.Id,
                bozza.Decorrenza, bozza.Scadenza, bozza.Massimale, bozza.Franchigia);
        }
    }

    /// <summary>Durata 1–3 anni, decorrenza negli ultimi 6 anni; circa l'80% delle polizze è in vigore oggi.</summary>
    private (DateOnly Decorrenza, DateOnly Scadenza) Validita()
    {
        int anni = faker.Random.Number(1, 3);
        DateOnly decorrenza = faker.Random.Bool(QuotaInVigore)
            ? oggi.AddDays(-faker.Random.Number(0, (anni * 365) - 1))
            : oggi.AddYears(-AnniDecorrenza).AddDays(faker.Random.Number(0, ((AnniDecorrenza - anni) * 365) - 1));

        return (decorrenza, decorrenza.AddYears(anni).AddDays(-1));
    }

    private decimal Massimale(Prodotto prodotto) => prodotto == Prodotto.CasaFabbricati
        ? faker.Random.Number(2, 12) * 50_000m
        : faker.Random.ArrayElement(MassimaliRc);

    private decimal Franchigia(Prodotto prodotto) =>
        faker.Random.ArrayElement(prodotto == Prodotto.CasaFabbricati ? FranchigieCasa : FranchigieRc);
}
