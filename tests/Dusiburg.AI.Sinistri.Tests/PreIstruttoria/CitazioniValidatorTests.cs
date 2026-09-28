using Dusiburg.AI.Sinistri.Core.PreIstruttoria;

namespace Dusiburg.AI.Sinistri.Tests.PreIstruttoria;

public class CitazioniValidatorTests
{
    [Test]
    public void Valida_ArticoloInesistente_RimossoConAvviso()
    {
        //SETUP
        SchedaPreIstruttoria scheda = DatiScheda.Scheda(
            garanzie: [new("Art. 2.4", "ok"), new("Art. 9.9", "inventato")],
            franchigia: new FranchigiaApplicabile("Art. 4.9", "inventata"));

        //SUT
        SchedaValidata validata = CitazioniValidator.Valida(scheda, DatiScheda.Clausole, DatiScheda.Polizza, DatiScheda.Statistiche);

        Assert.That(validata.Scheda!.GaranzieOperanti.Select(g => g.Articolo), Is.EqualTo(new[] { "Art. 2.4" }));
        Assert.That(validata.Scheda.FranchigiaApplicabile, Is.Null);
        Assert.That(validata.Avvisi.Select(a => a.Messaggio), Has.Some.StartsWith("Art. 9.9 citato dal modello in garanzie operanti ma non tra le clausole recuperate: rimosso")
            .And.Some.StartsWith("Art. 4.9 citato dal modello in franchigia applicabile"));
        Assert.That(validata.Avvisi.Select(a => a.Tipo), Is.All.EqualTo(TipoAvviso.Citazione));
    }

    [Test]
    public void Valida_FormeDiverseDellArticolo()
    {
        //SETUP
        SchedaPreIstruttoria scheda = DatiScheda.Scheda(
            garanzie: [new("art 2.4", "a"), new("Articolo 2.5", "b")],
            esclusioni: [new("3.4", "c")],
            franchigia: new FranchigiaApplicabile("Art.4.3", "d"));

        //SUT
        SchedaValidata validata = CitazioniValidator.Valida(scheda, DatiScheda.Clausole, DatiScheda.Polizza, DatiScheda.Statistiche);

        Assert.That(validata.Scheda!.GaranzieOperanti.Select(g => g.Articolo), Is.EqualTo(new[] { "Art. 2.4", "Art. 2.5" }));
        Assert.That(validata.Scheda.EsclusioniDaVerificare.Single().Articolo, Is.EqualTo("Art. 3.4"));
        Assert.That(validata.Scheda.FranchigiaApplicabile!.Articolo, Is.EqualTo("Art. 4.3"));
        Assert.That(validata.Avvisi, Is.Empty);
    }

    [Test]
    public void Valida_TipoIncoerente_Avviso()
    {
        //SETUP
        SchedaPreIstruttoria scheda = DatiScheda.Scheda(garanzie: [new("Art. 3.4", "un'esclusione tra le garanzie")]);

        //SUT
        SchedaValidata validata = CitazioniValidator.Valida(scheda, DatiScheda.Clausole, DatiScheda.Polizza, DatiScheda.Statistiche);

        Assert.That(validata.Scheda!.GaranzieOperanti.Single().Articolo, Is.EqualTo("Art. 3.4"));
        Assert.That(validata.Avvisi.Single().Messaggio, Does.Contain("Art. 3.4 (Esclusione) indicato in garanzie operanti: tipo incoerente"));
    }

    [Test]
    public void Valida_Importi_SoloQuelliNonRiconducibiliSegnalati()
    {
        //SETUP
        SchedaPreIstruttoria scheda = DatiScheda.Scheda(valutazione:
            "Massimale 300.000,00 €, franchigia 250 euro, spese di ricerca fino a 5.000 € (Art. 4.3), mediana dei simili 3.100 €. Stima del danno 12.500 €.");

        //SUT
        SchedaValidata validata = CitazioniValidator.Valida(scheda, DatiScheda.Clausole, DatiScheda.Polizza, DatiScheda.Statistiche);

        Assert.That(validata.Avvisi.Select(a => (a.Tipo, a.Messaggio)),
            Is.EqualTo(new[] { (TipoAvviso.Importo, "importo non verificato: 12.500 € non compare tra dati di polizza, statistiche e clausole.") }));
    }

    [Test]
    public void Numero_EstraeIlNumeroDellArticolo()
    {
        //SUT
        string?[] numeri = [.. new[] { "Art. 2.10", "articolo 3.1", "4.3", "Art.", null }.Select(CitazioniValidator.Numero)];

        Assert.That(numeri, Is.EqualTo(new[] { "2.10", "3.1", "4.3", null, null }));
    }
}
