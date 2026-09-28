using Dusiburg.AI.Sinistri.Core.Antifrode;
using Dusiburg.AI.Sinistri.Core.PreIstruttoria;
using Dusiburg.AI.Sinistri.Core.Seed;

namespace Dusiburg.AI.Sinistri.Tests.Antifrode;

/// <summary>Precision/recall del fraud-scan (fase-7.md §3) e scelta della soglia, senza DB.</summary>
public class ValutazioneDuplicatiTests
{
    private static readonly IReadOnlyList<CoppiaDuplicati> Attese =
    [
        new("S-1", "S-2", TipoCoppiaDuplicati.StessoContraente),
        new("S-3", "S-4", TipoCoppiaDuplicati.StessoRiparatore),
        new("S-5", "S-6", TipoCoppiaDuplicati.StessoRiparatore),
    ];

    [Test]
    public void Calcola_PrecisionRecall()
    {
        //SETUP
        IReadOnlyList<CoppiaSospetta> trovate =
        [
            Coppia("S-2", "S-1", 0.01),                                      // attesa, orientata al contrario
            Coppia("S-1", "S-2", 0.01),                                      // stessa coppia ripetuta: conta una volta
            Coppia("S-7", "S-8", 0.03, MotivoSegnalazione.SoloTestoSimile),  // non attesa
            Coppia("S-3", "S-4", 0.06),                                      // attesa, sopra 0,05
        ];

        //SUT
        RigaValutazione a005 = ValutazioneDuplicati.Calcola(trovate, Attese, 0.05);
        RigaValutazione a008 = ValutazioneDuplicati.Calcola(trovate, Attese, 0.08);
        RigaValutazione nessuna = ValutazioneDuplicati.Calcola([], Attese, 0.08);

        Assert.That(a005, Is.EqualTo(new RigaValutazione(0.05, 2, 1, 1, 2, 0.5, 1.0 / 3, 0.4)).Using<RigaValutazione>(Uguali));
        Assert.That(a008, Is.EqualTo(new RigaValutazione(0.08, 3, 2, 1, 1, 2.0 / 3, 2.0 / 3, 2.0 / 3)).Using<RigaValutazione>(Uguali));
        Assert.That(nessuna, Is.EqualTo(new RigaValutazione(0.08, 0, 0, 0, 3, 0, 0, 0)));
        Assert.That(ValutazioneDuplicati.Attesa(Coppia("S-4", "S-3", 0.1), Attese), Is.True);
    }

    [Test]
    public void SuggerisciSoglia_RecallMinima()
    {
        //SETUP
        IReadOnlyList<RigaValutazione> righe =
        [
            new(0.15, 60, 10, 50, 0, 0.17, 1.0, 0.29),
            new(0.05, 7, 6, 1, 4, 0.86, 0.6, 0.71),
            new(0.08, 11, 9, 2, 1, 0.82, 0.9, 0.86),
            new(0.12, 25, 10, 15, 0, 0.4, 1.0, 0.57),
        ];

        //SUT
        RigaValutazione? suggerita = ValutazioneDuplicati.SuggerisciSoglia(righe);
        RigaValutazione? irraggiungibile = ValutazioneDuplicati.SuggerisciSoglia(righe.Where(r => r.Recall < 0.8));

        Assert.That(suggerita?.Soglia, Is.EqualTo(0.08));
        Assert.That(irraggiungibile, Is.Null);
        Assert.That(ValutazioneDuplicati.Soglie(0.08), Is.EqualTo(new[] { 0.05, 0.08, 0.12, 0.15 }));
        Assert.That(ValutazioneDuplicati.Soglie(0.03), Is.EqualTo(new[] { 0.03, 0.05, 0.08, 0.12, 0.15 }));
    }

    [Test]
    public void Distribuzione_MinMedianaMax()
    {
        //SUT
        var dispari = ValutazioneDuplicati.Distribuzione([0.09, 0.01, 0.03]);
        var pari = ValutazioneDuplicati.Distribuzione([0.04, 0.01, 0.02, 0.09]);

        Assert.That(dispari, Is.EqualTo((0.01, 0.03, 0.09)));
        Assert.That(pari!.Value.Mediana, Is.EqualTo(0.03).Within(1e-12));
        Assert.That(ValutazioneDuplicati.Distribuzione([]), Is.Null);
    }

    [Test]
    public void Valuta_TutteEConLegame()
    {
        //SETUP
        IReadOnlyList<CoppiaSospetta> trovate =
        [
            Coppia("S-9", "S-10", 0.0, MotivoSegnalazione.SoloTestoSimile),
            Coppia("S-1", "S-2", 0.01),
            Coppia("S-3", "S-4", 0.02),
            Coppia("S-11", "S-12", 0.04, MotivoSegnalazione.StessoRiparatore),
            Coppia("S-5", "S-6", 0.10),
        ];

        //SUT
        ValutazioneFraudScan valutazione = AntifrodeService.Valuta(trovate, Attese, ValutazioneDuplicati.Soglie(0.05), []);

        Assert.That(valutazione.Tutte.Select(r => r.FalsiPositivi), Is.EqualTo(new[] { 2, 2, 2, 2 }));
        Assert.That(valutazione.ConLegame.Select(r => r.FalsiPositivi), Is.EqualTo(new[] { 1, 1, 1, 1 }));
        Assert.That(valutazione.SuggeritaConLegame?.Soglia, Is.EqualTo(0.12));
        Assert.That(valutazione.NonAttesaPiuVicina?.NumeroA, Is.EqualTo("S-9"));
        Assert.That(valutazione.NonAttesaConLegamePiuVicina?.NumeroA, Is.EqualTo("S-11"));
    }

    private static CoppiaSospetta Coppia(string a, string b, double distanza, MotivoSegnalazione motivo = MotivoSegnalazione.StessoContraente) =>
        new(a, b, distanza, motivo, GiorniTraDenunce: 30, "descrizione A", "descrizione B");

    private static bool Uguali(RigaValutazione x, RigaValutazione y) =>
        x with { Precision = 0, Recall = 0, F1 = 0 } == y with { Precision = 0, Recall = 0, F1 = 0 }
        && Math.Abs(x.Precision - y.Precision) < 1e-9 && Math.Abs(x.Recall - y.Recall) < 1e-9 && Math.Abs(x.F1 - y.F1) < 1e-9;
}
