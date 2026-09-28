using Dusiburg.AI.Sinistri.Core.Consultazione;
using Dusiburg.AI.Sinistri.Core.Dominio;

namespace Dusiburg.AI.Sinistri.Tests.Consultazione;

/// <summary>Catalogo Markdown delle clausole e ordine degli articoli (fase-9b.md §4), senza DB.</summary>
public class ClausoleMarkdownRendererTests
{
    private static readonly IReadOnlyList<ClausolaDettaglio> Clausole =
    [
        new(3, Prodotto.CasaFabbricati, "Art. 2.10", TipoClausola.Garanzia, "Cristalli", "Testo cristalli."),
        new(1, Prodotto.CasaFabbricati, "Art. 2.9", TipoClausola.Garanzia, "RC della proprietà", "Testo RC."),
        new(2, Prodotto.CasaFabbricati, "Art. 1.1", TipoClausola.Definizione, "Fabbricato", "Testo fabbricato."),
        new(4, Prodotto.RcProfTecnici, "Art. 3.3", TipoClausola.Esclusione, "Multe", "Testo multe."),
    ];

    [Test]
    public void Render_PerProdottoETipo_ConAncoreEIndice()
    {
        //SUT
        string md = ClausoleMarkdownRenderer.Render(Clausole, new DateOnly(2026, 9, 28));

        Assert.That(md, Does.StartWith("# Catalogo delle clausole di polizza\n"));
        Assert.That(md, Does.Contain("> Generato con `export-clausole` il 2026-09-28 dal database"));
        Assert.That(md, Does.Contain("- [Casa e fabbricati](#casa-e-fabbricati): definizioni 1, garanzie 2")
            .And.Contain("- [RC professionale tecnici](#rc-professionale-tecnici): esclusioni 1"));
        Assert.That(md, Does.Contain("<a id=\"casafabbricati-art-2-10\"></a>\n\n#### Art. 2.10 — Cristalli\n\nTesto cristalli."));
        Assert.That(md.IndexOf("### Definizioni", StringComparison.Ordinal), Is.LessThan(md.IndexOf("### Garanzie", StringComparison.Ordinal)));
        Assert.That(md.IndexOf("Art. 2.9 —", StringComparison.Ordinal), Is.LessThan(md.IndexOf("Art. 2.10 —", StringComparison.Ordinal)), "ordine numerico");
    }

    [Test]
    public void SenzaData_IgnoraSoloLaRigaDellaGenerazione()
    {
        //SETUP
        string oggi = ClausoleMarkdownRenderer.Render(Clausole, new DateOnly(2026, 9, 28));
        string domani = ClausoleMarkdownRenderer.Render(Clausole, new DateOnly(2026, 9, 29));
        string cambiato = ClausoleMarkdownRenderer.Render([.. Clausole.Take(3)], new DateOnly(2026, 9, 28));

        //SUT
        Assert.That(ClausoleMarkdownRenderer.SenzaData(oggi), Is.EqualTo(ClausoleMarkdownRenderer.SenzaData(domani.Replace("\n", "\r\n"))));
        Assert.That(ClausoleMarkdownRenderer.SenzaData(oggi), Is.Not.EqualTo(ClausoleMarkdownRenderer.SenzaData(cambiato)));
    }

    [Test]
    public void OrdineArticoli_ChiaveEAncora()
    {
        //SUT
        Assert.That(OrdineArticoli.Chiave("Art. 2.10"), Is.GreaterThan(OrdineArticoli.Chiave("Art. 2.9")));
        Assert.That(OrdineArticoli.Chiave("Art. 3.1"), Is.GreaterThan(OrdineArticoli.Chiave("Art. 2.11")));
        Assert.That(OrdineArticoli.Ancora(Prodotto.RcProfTecnici, "Art. 4.4"), Is.EqualTo("rcproftecnici-art-4-4"));
    }
}
