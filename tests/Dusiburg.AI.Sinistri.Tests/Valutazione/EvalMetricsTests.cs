using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Options;
using Dusiburg.AI.Sinistri.Core.Retrieval;
using Dusiburg.AI.Sinistri.Core.Valutazione;

namespace Dusiburg.AI.Sinistri.Tests.Valutazione;

/// <summary>Metriche del retrieval (fase-9.md §2) e composizione del risultato per caso e del report, senza DB né modello.</summary>
public class EvalMetricsTests
{
    private static readonly string[] Ordinati = ["Art. 2.4", "Art. 1.5", "Art. 3.4", "Art. 2.5", "Art. 1.1", "Art. 4.3", "Art. 2.2"];

    [Test]
    public void RecallAtK_CasiLimite()
    {
        //SUT
        Assert.That(EvalMetrics.RecallAtK(["Art. 2.4", "Art. 2.5"], Ordinati, 5), Is.EqualTo(1.0));
        Assert.That(EvalMetrics.RecallAtK(["Art. 2.4", "Art. 4.3"], Ordinati, 5), Is.EqualTo(0.5), "Art. 4.3 è sesto");
        Assert.That(EvalMetrics.RecallAtK(["Art. 9.9"], Ordinati, 5), Is.Zero, "nessun rilevante trovato");
        Assert.That(EvalMetrics.RecallAtK([], Ordinati, 5), Is.Zero, "nessun rilevante atteso");
        Assert.That(EvalMetrics.RecallAtK(["Art. 2.4", "Art. 1.5", "Art. 3.4", "Art. 2.5", "Art. 1.1", "Art. 4.3"], Ordinati, 5),
            Is.EqualTo(5.0 / 6), "più rilevanti di k: il massimo è < 1");
        Assert.That(EvalMetrics.RecallAtK(["Art. 2.4", "Art. 2.2"], ["Art. 2.4"], 5), Is.EqualTo(0.5), "meno risultati di k");
    }

    [Test]
    public void RankEReciprocalRank()
    {
        //SUT
        Assert.That(EvalMetrics.RankPrimoRilevante(["Art. 3.4", "Art. 2.2"], Ordinati), Is.EqualTo(3));
        Assert.That(EvalMetrics.ReciprocalRank(["Art. 3.4", "Art. 2.2"], Ordinati), Is.EqualTo(1.0 / 3));
        Assert.That(EvalMetrics.RankPrimoRilevante(["Art. 2.2"], Ordinati, maxRank: 5), Is.Null, "oltre il rank massimo");
        Assert.That(EvalMetrics.ReciprocalRank(["Art. 9.9"], Ordinati), Is.Zero);
    }

    [Test]
    public void Copertura_SenzaAtteseNull()
    {
        //SUT
        Assert.That(EvalMetrics.Copertura(["Art. 3.4", "Art. 3.1"], ["Art. 2.4", "Art. 3.4"]), Is.EqualTo(0.5));
        Assert.That(EvalMetrics.Copertura([], ["Art. 2.4"]), Is.Null);
    }

    [Test]
    public void Valuta_CasoEMedie()
    {
        //SETUP
        var caso = new CasoGolden("G01", Prodotto.CasaFabbricati, "Tubo rotto.", new AtteseGolden(["Art. 2.4", "Art. 4.3"], ["Art. 3.4"]));
        var senzaEsclusioni = caso with { Id = "G02", Attese = new AtteseGolden(["Art. 9.9"], []) };
        IReadOnlyList<ClausolaTrovata> pura = [.. Ordinati.Select((a, i) => Clausola(a, i + 1))];
        IReadOnlyList<ClausolaTrovata> completa = [Clausola("Art. 2.4", 1), Clausola("Art. 3.4", 2)];

        //SUT
        RisultatoCaso primo = EvalService.Valuta(caso, pura, completa, k: 5);
        RisultatoCaso secondo = EvalService.Valuta(senzaEsclusioni, pura, completa, k: 5);
        var eval = new RisultatoEval(DateTimeOffset.Now, "embeddinggemma", "ollama", 768, "qwen3.5:9b", "Sinistri", 5, new RetrievalOptions(),
            [primo, secondo], TimeSpan.FromSeconds(1));

        Assert.That(primo, Has.Property(nameof(RisultatoCaso.RecallAtK)).EqualTo(0.5)
            .And.Property(nameof(RisultatoCaso.RankPrimoRilevante)).EqualTo(1)
            .And.Property(nameof(RisultatoCaso.RecallEsclusioni)).EqualTo(1.0));
        Assert.That(primo.Mancanti, Is.EqualTo(new[] { "Art. 4.3" }));
        Assert.That(secondo.RecallEsclusioni, Is.Null);
        Assert.That((eval.RecallMedia, eval.Mrr, eval.Hit1, eval.RecallEsclusioniMedia), Is.EqualTo((0.25, 0.5, 0.5, (double?)1.0)));
    }

    [Test]
    public void Report_MetricheCasiPeggioriENome()
    {
        //SETUP
        var caso = new CasoGolden("G08", Prodotto.CasaFabbricati, "La canna fumaria ha preso fuoco.", new AtteseGolden(["Art. 2.1", "Art. 2.11"], []));
        RisultatoCaso risultato = EvalService.Valuta(caso, [Clausola("Art. 2.1", 1), Clausola("Art. 1.3", 2), Clausola("Art. 2.11", 6)], [], k: 1);
        var eval = new RisultatoEval(new DateTimeOffset(2026, 9, 28, 16, 9, 0, TimeSpan.Zero), "bge-m3", "ollama", 1024, "qwen3.5:9b",
            "Sinistri_emb_ollama_bge_m3", 1, new RetrievalOptions(), [risultato], TimeSpan.FromSeconds(1));

        //SUT
        string report = EvalReportRenderer.Render(eval);

        Assert.That(EvalReportRenderer.NomeFile(eval), Is.EqualTo("report_2026-09-28_1609_bge-m3.md"));
        Assert.That(report, Does.Contain("| recall@1 | **0,50** | ≥ 0,70 ✗ |"));
        Assert.That(report, Does.Contain("### G08 — recall@1 0,50").And.Contain("Mancanti: Art. 2.11 al 3° posto"));
        Assert.That(report, Does.Contain("| G08 | CasaFabbricati | 0,50 | 1 | Art. 2.1 ✓ | Art. 2.11 | — |"));
    }

    private static ClausolaTrovata Clausola(string articolo, int rank) =>
        new(rank, articolo, TipoClausola.Garanzia, $"Titolo {articolo}", "-", 0.5 + rank / 100.0, rank, false);
}
