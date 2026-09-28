using Dusiburg.AI.Sinistri.Core.Demo;
using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Tests.Seed;

public class DemoCatalogTests
{
    [Test]
    public void ScenariCoerenti()
    {
        //SETUP
        DemoScenario[] preIstruttorie = [.. DemoCatalog.Scenari.Where(s => s.Tipo == TipoScenario.PreIstruttoria)];

        //SUT
        DemoPolizza[] polizze = [.. preIstruttorie.Select(s => DemoCatalog.Polizza(s.NumeroPolizza!))];

        Assert.That(DemoCatalog.Scenari.Select(s => s.Numero), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
        Assert.That(DemoCatalog.Polizze.Select(p => p.Numero), Is.Unique);
        Assert.That(polizze.Select(p => p.Prodotto), Is.EqualTo(preIstruttorie.Select(s => s.Prodotto)));
        Assert.That(DemoCatalog.Scenari.Where(s => s.Causa is not null).Select(s => s.Causa!.Value.ProdottoDellaCausa()),
            Is.EqualTo(DemoCatalog.Scenari.Where(s => s.Causa is not null).Select(s => s.Prodotto)));
        Assert.That(DemoCatalog.Scenari.Single(s => s.Tipo == TipoScenario.FraudScan).Mesi, Is.EqualTo(12));
    }

    [Test]
    public void Polizze_InVigoreAllaDataDelSeed()
    {
        //SETUP
        var oggi = new DateOnly(2026, 9, 25);

        //SUT
        DemoPolizza[] polizze = [.. DemoCatalog.Polizze];

        Assert.That(polizze.Where(p => p.Decorrenza(oggi) > oggi || p.Scadenza(oggi) <= oggi), Is.Empty);
        Assert.That(polizze.Select(p => p.Provincia), Is.All.Length.EqualTo(2));
    }
}
