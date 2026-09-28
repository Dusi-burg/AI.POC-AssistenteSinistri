using Bogus;
using Dusiburg.AI.Sinistri.Core.Dominio;
using Dusiburg.AI.Sinistri.Core.Seed;
using Dusiburg.AI.Sinistri.Ingestion.Seed;

namespace Dusiburg.AI.Sinistri.Tests.Seed;

public class QuasiDuplicatiGeneratorTests
{
    [Test]
    public void Generate_DieciCoppieNegliUltimiDieciMesi()
    {
        //SETUP
        DateOnly oggi = SinistriGeneratorTests.Oggi;

        //SUT
        DatiSintetici dati = SyntheticDataGenerator.Genera(SyntheticDataGenerator.DefaultRandomSeed, oggi);

        Dictionary<string, SinistroSintetico> sinistri = dati.Sinistri.ToDictionary(s => s.Numero);
        Dictionary<int, PolizzaSintetica> polizze = dati.Polizze.ToDictionary(p => p.Id);

        Assert.That(dati.Coppie.Count(c => c.Tipo == TipoCoppiaDuplicati.StessoContraente), Is.EqualTo(QuasiDuplicatiGenerator.CoppieStessoContraente));
        Assert.That(dati.Coppie.Count(c => c.Tipo == TipoCoppiaDuplicati.StessoRiparatore), Is.EqualTo(QuasiDuplicatiGenerator.CoppieStessoRiparatore));
        Assert.That(dati.Coppie.SelectMany(c => new[] { c.Originale, c.Duplicato }), Is.Unique);

        foreach (CoppiaDuplicati coppia in dati.Coppie)
        {
            SinistroSintetico originale = sinistri[coppia.Originale];
            SinistroSintetico duplicato = sinistri[coppia.Duplicato];
            int contraenteOriginale = polizze[originale.PolizzaId].ContraenteId;
            int contraenteDuplicato = polizze[duplicato.PolizzaId].ContraenteId;
            int giorni = duplicato.DataDenuncia.DayNumber - originale.DataDenuncia.DayNumber;

            Assert.That(originale.DataDenuncia, Is.GreaterThanOrEqualTo(oggi.AddDays(-QuasiDuplicatiGenerator.GiorniFinestra)), coppia.ToString());
            Assert.That(duplicato.DataDenuncia, Is.LessThanOrEqualTo(oggi), coppia.ToString());
            Assert.That(giorni, Is.InRange(30, 120), coppia.ToString());
            Assert.That(duplicato.Causa, Is.EqualTo(originale.Causa));
            Assert.That(duplicato.Descrizione, Is.Not.EqualTo(originale.Descrizione));
            Assert.That(polizze[duplicato.PolizzaId].ValidaIl(duplicato.DataEvento), Is.True, coppia.ToString());

            if (coppia.Tipo == TipoCoppiaDuplicati.StessoContraente)
            {
                Assert.That(contraenteDuplicato, Is.EqualTo(contraenteOriginale), coppia.ToString());
            }
            else
            {
                Assert.That(contraenteDuplicato, Is.Not.EqualTo(contraenteOriginale), coppia.ToString());
                Assert.That(duplicato.RiparatoreId, Is.Not.Null.And.EqualTo(originale.RiparatoreId), coppia.ToString());
            }
        }
    }

    [Test]
    public void Riformula_SinonimiStanzaEOrdineDelleFrasi()
    {
        //SETUP
        const string testo = "Durante la notte si è rotto il tubo di mandata in bagno, danneggiando il parquet. Ho scattato alcune fotografie dei danni.";

        //SUT
        string riformulato = QuasiDuplicatiGenerator.Riformula(testo, new Randomizer(1));

        Assert.That(riformulato, Does.StartWith("Ho scattato alcune foto dei danni."));
        Assert.That(riformulato, Does.Contain("rovinando il parquet"));
        Assert.That(riformulato, Does.Not.Contain("in bagno"));
        Assert.That(riformulato, Does.Contain(" in ").And.EndsWith("."));
    }

    [Test]
    public void Riformula_NienteDaCambiare_TestoComunqueDiverso()
    {
        //SETUP
        const string conCircostanza = "Di prima mattina una raffica di vento ha rovesciato l'antenna televisiva.";
        const string senzaAppigli = "Una tromba d'aria ha sradicato un albero del giardino condominiale, che è caduto sulla recinzione.";

        //SUT
        string circostanzaCambiata = QuasiDuplicatiGenerator.Riformula(conCircostanza, new Randomizer(1));
        string conApertura = QuasiDuplicatiGenerator.Riformula(senzaAppigli, new Randomizer(1));

        Assert.That(circostanzaCambiata, Does.Not.StartWith("Di prima mattina").And.EndWith("una raffica di vento ha rovesciato l'antenna televisiva."));
        Assert.That(conApertura, Is.EqualTo($"Segnalo il danno per l'apertura della pratica. {senzaAppigli}"));
    }

    [Test]
    public void Riformula_UnaSolaFrase_ScambiaLeParti()
    {
        //SETUP
        const string testo = "Perdita d'acqua dal sifone del lavello in cucina: l'acqua è uscita per diverse ore.";

        //SUT
        string riformulato = QuasiDuplicatiGenerator.Riformula(testo, new Randomizer(1));

        Assert.That(riformulato, Does.StartWith("L'acqua è uscita per diverse ore. Perdita d'acqua dal sifone del lavello in "));
        Assert.That(riformulato, Does.Not.Contain("in cucina"));
    }
}
