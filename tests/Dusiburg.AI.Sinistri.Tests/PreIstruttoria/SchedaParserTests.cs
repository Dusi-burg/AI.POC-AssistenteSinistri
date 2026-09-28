using Dusiburg.AI.Sinistri.Core.PreIstruttoria;

namespace Dusiburg.AI.Sinistri.Tests.PreIstruttoria;

public class SchedaParserTests
{
    private const string Json =
        """
        {"garanzieOperanti":[{"articolo":"Art. 2.4","motivazione":"Rottura accidentale."}],
         "esclusioniDaVerificare":[],"franchigiaApplicabile":null,
         "puntiDaChiarireConCliente":["Data della rottura"],"valutazioneSintetica":"Indennizzabile."}
        """;

    [Test]
    public void Parse_JsonConRecintoMarkdown()
    {
        //SETUP
        string risposta = $"Ecco la scheda:\n```json\n{Json}\n```\nFine.";

        //SUT
        RisultatoParsing risultato = SchedaParser.Parse(risposta);

        Assert.That(risultato.Riuscito, Is.True, risultato.Errore);
        Assert.That(risultato.Scheda!.GaranzieOperanti.Single(), Is.EqualTo(new GaranziaOperante("Art. 2.4", "Rottura accidentale.")));
        Assert.That(risultato.Scheda.FranchigiaApplicabile, Is.Null);
        Assert.That(risultato.Scheda.ValutazioneSintetica, Is.EqualTo("Indennizzabile."));
    }

    [Test]
    public void Parse_JsonNonValido_Fallisce()
    {
        //SUT
        RisultatoParsing troncato = SchedaParser.Parse("{\"garanzieOperanti\": [ {\"articolo\": \"Art. 2.4\"");
        RisultatoParsing senzaOggetto = SchedaParser.Parse("Non posso rispondere.");
        RisultatoParsing senzaValutazione = SchedaParser.Parse("{\"garanzieOperanti\": [], \"valutazioneSintetica\": \" \"}");
        RisultatoParsing vuoto = SchedaParser.Parse(null);

        Assert.That(new[] { troncato, senzaOggetto, senzaValutazione, vuoto }.Select(r => r.Riuscito), Is.All.False);
        Assert.That(senzaValutazione.Errore, Does.Contain("valutazioneSintetica"));
        Assert.That(troncato.Errore, Is.Not.Empty);
    }

    [Test]
    public void Parse_SezioniMancanti_ListeVuote()
    {
        //SUT
        RisultatoParsing risultato = SchedaParser.Parse("{\"valutazioneSintetica\": \"Da approfondire.\"}");

        Assert.That(risultato.Riuscito, Is.True);
        Assert.That(risultato.Scheda!.GaranzieOperanti, Is.Empty);
        Assert.That(risultato.Scheda.EsclusioniDaVerificare, Is.Empty);
        Assert.That(risultato.Scheda.PuntiDaChiarireConCliente, Is.Empty);
    }
}
