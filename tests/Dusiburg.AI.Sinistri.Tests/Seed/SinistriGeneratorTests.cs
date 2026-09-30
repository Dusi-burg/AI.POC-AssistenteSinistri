using System.Text.Json;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;

namespace Dusiburg.AI.Sinistri.Tests.Seed;

/// <summary>Generatore dei dati sintetici (fase-3.md): determinismo e regole comuni dei sinistri, su una data di riferimento fissa.</summary>
public class SinistriGeneratorTests
{
    internal static readonly DateOnly Oggi = new(2026, 9, 25);

    private static readonly DatiSintetici Dati = SyntheticDataGenerator.Genera(SyntheticDataGenerator.DefaultRandomSeed, Oggi);

    [Test]
    public void Generate_SameSeed_SameOutput()
    {
        //SETUP
        string atteso = JsonSerializer.Serialize(Dati);

        //SUT
        DatiSintetici stessoSeed = SyntheticDataGenerator.Genera(SyntheticDataGenerator.DefaultRandomSeed, Oggi);
        DatiSintetici altroSeed = SyntheticDataGenerator.Genera(SyntheticDataGenerator.DefaultRandomSeed + 1, Oggi);

        Assert.That(JsonSerializer.Serialize(stessoSeed), Is.EqualTo(atteso));
        Assert.That(JsonSerializer.Serialize(altroSeed), Is.Not.EqualTo(atteso));
    }

    [Test]
    public void Generate_ConteggiDellaSpecifica()
    {
        //SUT
        DatiSintetici dati = Dati;

        Assert.That(dati.Contraenti, Has.Count.EqualTo(AnagraficheGenerator.Contraenti));
        Assert.That(dati.Riparatori, Has.Count.EqualTo(AnagraficheGenerator.Riparatori));
        Assert.That(dati.Polizze, Has.Count.EqualTo(AnagraficheGenerator.PolizzeCasuali + 3));
        Assert.That(dati.Polizze.Count(p => p.Prodotto == Prodotto.RcProfTecnici && !p.Numero.Contains("DEMO", StringComparison.Ordinal)),
            Is.EqualTo(AnagraficheGenerator.PolizzeRcCasuali));
        Assert.That(dati.Sinistri, Has.Count.EqualTo(SinistriGenerator.SinistriDefault + dati.Coppie.Count));
        Assert.That(dati.Polizze.Select(p => p.Numero), Is.Unique);
        Assert.That(dati.Sinistri.Select(s => s.Numero), Is.Unique.And.All.Match(@"^SIN-\d{4}-\d{6}$"));
        Assert.That(dati.Sinistri.Select(s => s.Id), Is.EqualTo(Enumerable.Range(1, dati.Sinistri.Count)));
    }

    [Test]
    public void Generate_CausaCoerenteConProdotto()
    {
        //SETUP
        Dictionary<int, PolizzaSintetica> polizze = Dati.Polizze.ToDictionary(p => p.Id);

        //SUT
        SinistroSintetico[] incoerenti = [.. Dati.Sinistri.Where(s => s.Causa.ProdottoDellaCausa() != polizze[s.PolizzaId].Prodotto)];

        Assert.That(incoerenti, Is.Empty);
        Assert.That(Dati.Sinistri.Where(s => polizze[s.PolizzaId].Prodotto == Prodotto.RcProfTecnici).Select(s => s.RiparatoreId), Is.All.Null);
    }

    [Test]
    public void Generate_DateDentroValiditaPolizza()
    {
        //SETUP
        Dictionary<int, PolizzaSintetica> polizze = Dati.Polizze.ToDictionary(p => p.Id);

        //SUT
        SinistroSintetico[] sinistri = [.. Dati.Sinistri];

        Assert.That(sinistri.Where(s => !polizze[s.PolizzaId].ValidaIl(s.DataEvento)), Is.Empty);
        Assert.That(sinistri.Where(s => s.DataDenuncia < s.DataEvento || s.DataDenuncia > Oggi), Is.Empty);
        Assert.That(sinistri.Where(s => s.DataEvento < Oggi.AddYears(-5)), Is.Empty);
        Assert.That(Dati.Polizze.Where(p => p.Scadenza <= p.Decorrenza), Is.Empty);
    }

    [Test]
    public void Generate_StatoCoerenteConImporti()
    {
        //SETUP
        Dictionary<int, PolizzaSintetica> polizze = Dati.Polizze.ToDictionary(p => p.Id);

        //SUT
        ILookup<StatoSinistro, SinistroSintetico> perStato = Dati.Sinistri.ToLookup(s => s.Stato);

        Assert.That(perStato[StatoSinistro.Aperto].Select(s => s.ImportoLiquidato), Is.All.Null);
        Assert.That(perStato[StatoSinistro.Aperto].Select(s => s.ImportoRiservato), Is.All.Not.Null);
        Assert.That(perStato[StatoSinistro.Respinto].Select(s => s.ImportoLiquidato), Is.All.Null);
        Assert.That(perStato[StatoSinistro.Respinto].Select(s => s.EsitoPerizia), Is.All.Not.Null);
        Assert.That(perStato[StatoSinistro.Chiuso].Where(s => s.ImportoLiquidato is null or <= 0 || s.ImportoLiquidato > polizze[s.PolizzaId].Massimale), Is.Empty);
        Assert.That(perStato[StatoSinistro.Chiuso].Select(s => s.ImportoLiquidato!.Value % 10), Is.All.Zero);
        Assert.That(perStato[StatoSinistro.Chiuso].Where(s => s.ImportoLiquidato > CatalogoTemplate.Profilo(s.Causa).ImportoMassimo), Is.Empty);
    }

    [Test]
    public void Generate_FenomenoElettricoEntroIlLimiteDellArt44()
    {
        //SETUP
        const decimal limiteArt44 = 6_000m;

        //SUT
        decimal[] liquidati = [.. Dati.Sinistri.Where(s => s.Causa == CausaSinistro.FenomenoElettrico && s.ImportoLiquidato is not null)
            .Select(s => s.ImportoLiquidato!.Value)];

        Assert.That(CatalogoTemplate.Profilo(CausaSinistro.FenomenoElettrico).ImportoMassimo, Is.EqualTo(limiteArt44));
        Assert.That(liquidati, Is.Not.Empty.And.All.LessThanOrEqualTo(limiteArt44));
    }

    [Test]
    public void Generate_DistribuzioneVicinaAiParametri()
    {
        //SETUP
        HashSet<string> gemelli = [.. Dati.Coppie.Select(c => c.Duplicato)];

        //SUT
        SinistroSintetico[] sinistri = [.. Dati.Sinistri.Where(s => !gemelli.Contains(s.Numero))];
        double aperti = (double)sinistri.Count(s => s.Stato == StatoSinistro.Aperto) / sinistri.Length;

        Assert.That(aperti, Is.EqualTo(0.10).Within(0.05));

        foreach (ProfiloCausa profilo in CatalogoTemplate.Profili)
        {
            SinistroSintetico[] causa = [.. sinistri.Where(s => s.Causa == profilo.Causa)];
            double quota = (double)causa.Length / sinistri.Length;
            double respinti = (double)causa.Count(s => s.Stato == StatoSinistro.Respinto) / causa.Length;

            Assert.That(quota, Is.EqualTo(profilo.Quota).Within(0.05), $"quota di {profilo.Causa}");
            Assert.That(respinti, Is.EqualTo(profilo.QuotaRespinti).Within(0.05), $"respinti di {profilo.Causa} ({causa.Length} sinistri)");
        }
    }

    [Test]
    public void Generate_ProvincePesateMaDistribuite()
    {
        //SUT
        Dictionary<string, int> perProvincia = Dati.Contraenti.GroupBy(c => c.Provincia).ToDictionary(g => g.Key, g => g.Count());

        Assert.That(perProvincia, Has.Count.GreaterThanOrEqualTo(15));
        Assert.That(perProvincia.Values.Max(), Is.LessThan(Dati.Contraenti.Count / 4));
        Assert.That(perProvincia.MaxBy(p => p.Value).Key, Is.EqualTo("MI"));
    }

    [Test]
    public void Generate_CoperturaDemoGarantita()
    {
        //SETUP
        Dictionary<int, PolizzaSintetica> polizze = Dati.Polizze.ToDictionary(p => p.Id);

        //SUT
        SinistroSintetico[] sinistri = [.. Dati.Sinistri];

        Assert.That(sinistri.Count(s => s.Causa == CausaSinistro.FenomenoElettrico && s.Provincia == "MI" && s.Stato == StatoSinistro.Chiuso
            && s.ImportoLiquidato > 5_000m && s.Descrizione.Contains("inverter", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(8));
        Assert.That(sinistri.Count(s => s.Causa == CausaSinistro.AcquaCondotta && s.Stato == StatoSinistro.Chiuso
            && s.Descrizione.Contains("parquet", StringComparison.Ordinal) && s.Descrizione.Contains("controsoffitto", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(6));

        SinistroSintetico[] pannelli = [.. sinistri.Where(s => s.Causa == CausaSinistro.EventoAtmosferico
            && s.Descrizione.Contains("grandin", StringComparison.OrdinalIgnoreCase) && s.Descrizione.Contains("pannelli", StringComparison.Ordinal))];
        Assert.That(pannelli, Has.Length.GreaterThanOrEqualTo(5));
        Assert.That(pannelli.Count(s => s.Stato == StatoSinistro.Respinto), Is.GreaterThan(pannelli.Length / 2));

        Assert.That(sinistri.Count(s => s.Causa == CausaSinistro.ErroreProgettuale
            && (s.Descrizione.Contains("solai", StringComparison.Ordinal) || s.Descrizione.Contains("struttur", StringComparison.Ordinal))), Is.GreaterThanOrEqualTo(5));
        Assert.That(sinistri.Where(s => polizze[s.PolizzaId].Numero == "CF-DEMO-000001"), Is.Not.Empty);
    }
}
