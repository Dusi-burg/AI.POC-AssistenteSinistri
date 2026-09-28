using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;

namespace Dusiburg.AI.Sinistri.Tests.PreIstruttoria;

public class SchedaMarkdownRendererTests
{
    [Test]
    public void Render_SchedaEFallback()
    {
        //SETUP
        var tempi = new TempiEsecuzione(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(20),
            TimeSpan.Zero, TimeSpan.FromSeconds(21.4));
        var duplicato = new SegnalazioneDuplicato("SIN-2026-000123", new DateOnly(2026, 5, 12), CausaSinistro.AcquaCondotta, StatoSinistro.Chiuso,
            "Mario Bianchi", "Idraulica Rossi", 0.041, MotivoSegnalazione.StessoContraente, "Rottura del tubo nel bagno.");
        var esito = new EsitoPreIstruttoria(new RichiestaPreIstruttoria("Tubo rotto in bagno.", "CF-DEMO-000001"), DatiScheda.Polizza,
            DatiScheda.Scheda(), null, DatiScheda.Clausole, DatiScheda.Simili(2), DatiScheda.Statistiche,
            [new Avviso(TipoAvviso.Importo, "importo non verificato: 12.500 €")], [], tempi, "qwen3.5:9b",
            new DateTimeOffset(2026, 9, 24, 10, 32, 0, TimeSpan.Zero));

        //SUT
        string scheda = SchedaMarkdownRenderer.Render(esito);
        string fallback = SchedaMarkdownRenderer.Render(esito with { Scheda = null, TestoLibero = "Testo libero del modello." });
        string conDuplicato = SchedaMarkdownRenderer.Render(esito with { PossibiliDuplicati = [duplicato] });

        Assert.That(scheda, Does.StartWith("# Scheda di pre-istruttoria — polizza CF-DEMO-000001\n> Generata il 2026-09-24 10:32 · modello qwen3.5:9b · tempo totale 21,4 s"));
        Assert.That(scheda, Does.Contain("- **Art. 2.4 — Acqua condotta**: Rottura accidentale.")
            .And.Contain("**Art. 4.3 — Franchigia acqua condotta** — Franchigia di polizza.")
            .And.Contain("| 10 | 2 (20,0%) | 850 € | 3.100 € | 9.800 € |")
            .And.Contain("- ⚠️ importo non verificato: 12.500 €")
            .And.Contain("| 7 | Art. 4.3 | Franchigia / scoperto / limite | Franchigia acqua condotta | 0,650 | sì |"));
        Assert.That(fallback, Does.Contain("## Scheda (testo libero, citazioni non validate)\nTesto libero del modello.").And.Not.Contain("## Garanzie operanti"));
        Assert.That(scheda, Does.Contain("## Possibili duplicati\nNessuna segnalazione."));
        Assert.That(conDuplicato, Does.Contain("> ⚠️ SIN-2026-000123 (12/05/2026, stesso contraente) — distanza 0,041 — " +
            "contraente Mario Bianchi · riparatore Idraulica Rossi — «Rottura del tubo nel bagno.»"));
    }
}
