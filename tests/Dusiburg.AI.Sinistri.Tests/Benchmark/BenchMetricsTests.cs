using Dusiburg.AI.Sinistri.Core.Benchmark;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Retrieval;

namespace Dusiburg.AI.Sinistri.Tests.Benchmark;

/// <summary>Metriche e report del banco di prova della ricerca (fase-10.md §10.1), senza DB.</summary>
public class BenchMetricsTests
{
    private static readonly InterrogazioneBench Base = new("G01", "Tubo rotto.", new FiltriStorico(Prodotto.CasaFabbricati, 5), Selettiva: false);
    private static readonly InterrogazioneBench Selettiva = Base with { Id = "G01-MI", Filtri = Base.Filtri with { Provincia = "MI" }, Selettiva = true };

    [Test]
    public void Overlap_EPercentile()
    {
        //SUT
        Assert.That(BenchMetrics.Overlap([1, 2, 3, 4], [4, 3, 9]), Is.EqualTo(0.5));
        Assert.That(BenchMetrics.Overlap([], [7]), Is.EqualTo(1), "nessun risultato esatto: niente da perdere");
        Assert.That(BenchMetrics.Percentile(Enumerable.Range(1, 20).Select(i => (double)i), 95), Is.EqualTo(19));
        Assert.That(BenchMetrics.Percentile([4, 1, 3, 2], 50), Is.EqualTo(2));
        Assert.That(BenchMetrics.Percentile([], 95), Is.Zero);
        Assert.That(BenchMetrics.OverlapDistanza([0.1, 0.2, 0.3], [0.1, 0.3, 0.3000004]), Is.EqualTo(1), "pari merito alla distanza del k-esimo");
        Assert.That(BenchMetrics.OverlapDistanza([0.1, 0.2, 0.3], [0.1, 0.35]), Is.EqualTo(1.0 / 3));
        Assert.That(BenchMetrics.OverlapDistanza([0.1, 0.2], [0.05, 0.1, 0.15, 0.2]), Is.EqualTo(1), "mai oltre 1");
        Assert.That(BenchMetrics.OverlapDistanza([], [0.1]), Is.EqualTo(1));
    }

    [Test]
    public void Riepiloga_PerMetodoEGruppo_OverlapUnaVoltaPerInterrogazione()
    {
        //SETUP
        MisuraBench[] misure =
        [
            Misura(MetodoBench.Esatta, Base, 8, 10, 1),
            Misura(MetodoBench.Esatta, Base, 12, 10, 1),
            Misura(MetodoBench.Indice(5), Base, 4, 10, 0.9),
            Misura(MetodoBench.Indice(5), Base, 6, 10, 0.9),
            Misura(MetodoBench.Indice(5), Selettiva, 5, 3, 0.3),
        ];

        //SUT
        IReadOnlyList<RigaBench> righe = BenchMetrics.Riepiloga(misure, k: 10);

        Assert.That(righe.Select(r => (r.Metodo.Nome, r.Selettive)), Is.EqualTo(new[]
        {
            ("esatta", false), ("indice, TOP_N = 5×k", false), ("indice, TOP_N = 5×k", true)
        }));
        Assert.That(righe[0], Has.Property(nameof(RigaBench.MediaMs)).EqualTo(10).And.Property(nameof(RigaBench.P95Ms)).EqualTo(12));
        Assert.That(righe[1], Has.Property(nameof(RigaBench.Interrogazioni)).EqualTo(1).And.Property(nameof(RigaBench.OverlapMedio)).EqualTo(0.9));
        Assert.That(righe[2], Has.Property(nameof(RigaBench.SottoK)).EqualTo(1).And.Property(nameof(RigaBench.RisultatiMedi)).EqualTo(3));
    }

    [Test]
    public void Report_TabellePerGruppoENome()
    {
        //SETUP
        var bench = new RisultatoBench(new DateTimeOffset(2026, 9, 28, 17, 5, 0, TimeSpan.Zero),
            new DatasetBench("Sinistri_bench_50000", 50_010, 26_115, TimeSpan.FromMinutes(21), TimeSpan.FromSeconds(40)), "embeddinggemma", 10, 50,
            BenchMetrics.Riepiloga([Misura(MetodoBench.Esatta, Base, 80, 10, 1), Misura(MetodoBench.Indice(5), Selettiva, 5, 3, 0.3)], 10),
            TimeSpan.FromMinutes(3));

        //SUT
        string report = BenchReportRenderer.Render(bench);

        Assert.That(BenchReportRenderer.NomeFile(bench), Is.EqualTo("bench_2026-09-28_1705_50010.md"));
        Assert.That(report, Does.Contain("| Sinistri | 50.010 (26.115 testi distinti vettorizzati) |")
            .And.Contain("| Creazione dell'indice | 00:00:40 |"));
        Assert.That(report, Does.Contain("## Filtri di base (1 interrogazioni").And.Contain("| esatta | 80,0 | 80,0 | 80,0 | 1,00 / 1,00 | 1,00 / 1,00 | 10,0 | 0 |"));
        Assert.That(report, Does.Contain("## Filtri selettivi").And.Contain("| indice, TOP_N = 5×k | 5,0 | 5,0 | 5,0 | 0,30 / 0,30 | 0,35 / 0,35 | 3,0 | 1 |"));
    }

    private static MisuraBench Misura(MetodoBench metodo, InterrogazioneBench interrogazione, double ms, int risultati, double overlap) =>
        new(metodo, interrogazione, TimeSpan.FromMilliseconds(ms), risultati, overlap, Math.Min(1, overlap + 0.05));
}
