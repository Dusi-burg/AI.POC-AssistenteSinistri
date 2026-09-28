using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Health;
using Dusiburg.AI.Sinistri.Web.Api;

namespace Dusiburg.AI.Sinistri.Web.Tests;

/// <summary>Formati italiani ed etichette della UI (fase-8.md §5).</summary>
public class PresentationTests
{
    [Test]
    public void Formati_Italiani()
    {
        //SUT
        Assert.That(SinistriPresentation.Euro(12_500m), Is.EqualTo("12.500 €"));
        Assert.That(SinistriPresentation.Euro(null), Is.EqualTo("—"));
        Assert.That(SinistriPresentation.Distanza(0.0415), Is.EqualTo("0,042"));
        Assert.That(SinistriPresentation.Secondi(TimeSpan.FromMilliseconds(14_250)), Is.EqualTo("14,3 s"));
        Assert.That(SinistriPresentation.Decimale(0.8181), Is.EqualTo("0,82"));
    }

    [Test]
    public void BarraVicinanza_CssConPuntoDecimale()
    {
        //SUT
        Assert.That(SinistriPresentation.BarraVicinanza(0), Is.EqualTo("100.0%"));
        Assert.That(SinistriPresentation.BarraVicinanza(0.42), Is.EqualTo("58.0%"));
        Assert.That(SinistriPresentation.BarraVicinanza(1.3), Is.EqualTo("0.0%"));
    }

    [Test]
    public void ClassiEEtichette()
    {
        //SUT
        Assert.That(SinistriPresentation.TipoClausolaClass(TipoClausola.Garanzia), Is.EqualTo("text-bg-success"));
        Assert.That(SinistriPresentation.TipoClausolaClass(TipoClausola.Esclusione), Is.EqualTo("text-bg-warning"));
        Assert.That(SinistriPresentation.TipoClausolaClass(TipoClausola.Franchigia), Is.EqualTo("text-bg-primary"));
        Assert.That(SinistriPresentation.StatoSinistroClass(StatoSinistro.Respinto), Is.EqualTo("text-bg-danger"));
        Assert.That(SinistriPresentation.ProbeEtichetta(ProbeStatus.Warning), Is.EqualTo("avviso"));
        Assert.That(SinistriPresentation.Complessivo(new ProbeReport(
        [
            new ProbeResult(1, "a", ProbeStatus.Ok, "", TimeSpan.Zero),
            new ProbeResult(2, "b", ProbeStatus.Warning, "", TimeSpan.Zero)
        ])), Is.EqualTo(ProbeStatus.Warning));
    }

    [Test]
    public void LinkTraccia_SoloConDashboardETraceId()
    {
        //SUT
        Assert.That(SinistriPresentation.LinkTraccia("https://localhost:17379/", "abc"), Is.EqualTo("https://localhost:17379/traces/detail/abc"));
        Assert.That(SinistriPresentation.LinkTraccia(null, "abc"), Is.Null);
        Assert.That(SinistriPresentation.LinkTraccia("https://localhost:17379", null), Is.Null);
    }
}
